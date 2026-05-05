using System.Numerics;
using Content.Client.Shuttles.UI;
using Content.Shared._Starlight.Weapons.Gunnery;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Collections;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;
namespace Content.Client._Starlight.Weapons.Gunnery;

/// <summary>
/// Radar control for the gunnery console.  Renders the standard shuttle radar
/// (grids, IFF labels, blips, laser traces) plus an additional overlay showing:
/// • Orange diamond blips for each shuttle-mounted cannon.
/// • A yellow aim-line from the selected cannon to the cursor.
/// • Visual indication when a guided projectile is being tracked.
///
/// Input:
/// • Click on cannon diamond → select that cannon.
/// • Click on open space (with cannon selected) → fire at cursor.
/// • Hold LMB while guided projectile is active → steer rocket toward cursor.
/// </summary>
public sealed class GunneryRadarControl : BaseShuttleControl
{
    [Dependency] private readonly IMapManager _mapManager = default!;

    private readonly SharedShuttleSystem  _shuttles;
    private readonly SharedTransformSystem _transform;

    // ── State received from server ─────────────────────────────────────────

    private EntityCoordinates?                       _coordinates;
    private Angle?                                   _rotation;
    private bool                                     _rotateWithEntity = true;
    private Dictionary<NetEntity, List<DockingPortState>> _docks = new();
    private List<RadarBlipData>                      _blips  = new();
    private List<RadarLaserData>                     _lasers = new();
    private List<CannonBlipData>                     _cannons = new();
    private NetEntity?                               _trackedGuidedProjectile;

    // ── UI state ───────────────────────────────────────────────────────────

    /// <summary>All cannons currently selected via click or sidebar.</summary>
    public HashSet<NetEntity> SelectedCannons = new();

    private Vector2? _cursorRelativePos;  // control-local pixel position
    private bool     _lmbHeld;

    private List<Entity<MapGridComponent>> _grids = new();

    // Selection radius in pixels (how close to a blip a click must land).
    private const float CannonSelectRadius = 14f;

    // ── Callbacks ──────────────────────────────────────────────────────────

    /// <summary>Invoked when the player clicks to fire a cannon. Args: (cannon NetEntity, target EntityCoordinates).</summary>
    public Action<NetEntity, EntityCoordinates>? OnFireRequested;

    /// <summary>Invoked continuously while LMB is held with an active guided projectile.</summary>
    public Action<EntityCoordinates>? OnGuidanceUpdate;

    // ── Cannon click feedback ──────────────────────────────────────────────

    /// <summary>Invoked whenever the cannon selection changes (click on blip or cleared).</summary>
    public Action? OnSelectionChanged;

    // ── Constructor ────────────────────────────────────────────────────────

    public GunneryRadarControl() : base(64f, 512f, 256f)
    {
        RobustXamlLoader.Load(this);
        _shuttles  = EntManager.System<SharedShuttleSystem>();
        _transform = EntManager.System<SharedTransformSystem>();
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void UpdateState(GunneryConsoleBoundUserInterfaceState state)
    {
        var nav = state.NavState;

        _coordinates      = EntManager.GetCoordinates(nav.Coordinates);
        _rotation         = nav.Angle;
        _rotateWithEntity = nav.RotateWithEntity;

        WorldMaxRange = nav.MaxRange;
        if (WorldMaxRange < WorldRange)
            ActualRadarRange = WorldMaxRange;
        if (WorldMaxRange < WorldMinRange)
            WorldMinRange = WorldMaxRange;
        ActualRadarRange = Math.Clamp(ActualRadarRange, WorldMinRange, WorldMaxRange);

        _docks   = state.DockPortStates.Docks;
        _blips   = nav.Blips;
        _lasers  = nav.Lasers;
        _cannons = state.Cannons;
        _trackedGuidedProjectile = state.TrackedGuidedProjectile;
    }

    // ── Input ──────────────────────────────────────────────────────────────

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function == EngineKeyFunctions.UIClick)
            _lmbHeld = true;
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _lmbHeld = false;

        // Don't fire if we can't resolve world coords.
        if (_coordinates == null || _rotation == null)
            return;

        var clickPos  = args.RelativePixelPosition;
        var worldPos  = ScreenToWorld(clickPos);

        // Check if click landed on a cannon blip — if so, select it.
        if (TrySelectCannonAt(clickPos))
            return;

        // Otherwise: fire all selected cannons toward click position.
        if (SelectedCannons.Count == 0)
            return;

        foreach (var selected in SelectedCannons)
            OnFireRequested?.Invoke(selected, worldPos);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        _cursorRelativePos = args.RelativePixelPosition;

        // While LMB is held and a guided projectile is active, send guidance.
        if (_lmbHeld && _trackedGuidedProjectile != null && _coordinates != null && _rotation != null)
        {
            var worldPos = ScreenToWorld(args.RelativePixelPosition);
            OnGuidanceUpdate?.Invoke(worldPos);
        }
    }

    // ── Drawing ────────────────────────────────────────────────────────────

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        DrawBacking(handle);
        DrawCircles(handle);

        if (_coordinates == null || _rotation == null)
            return;

        var xformQuery    = EntManager.GetEntityQuery<TransformComponent>();
        var fixturesQuery = EntManager.GetEntityQuery<FixturesComponent>();
        var bodyQuery     = EntManager.GetEntityQuery<PhysicsComponent>();

        if (!xformQuery.TryGetComponent(_coordinates.Value.EntityId, out var xform)
            || xform.MapID == MapId.Nullspace)
            return;

        var mapPos      = _transform.ToMapCoordinates(_coordinates.Value);
        var ourEntRot   = _rotateWithEntity ? _transform.GetWorldRotation(xform) : _rotation.Value;
        var ourEntMatrix = Matrix3Helpers.CreateTransform(_transform.GetWorldPosition(xform), ourEntRot);
        var posMatrix    = Matrix3Helpers.CreateTransform(_coordinates.Value.Position, _rotation.Value);
        var shuttleToWorld = Matrix3x2.Multiply(posMatrix, ourEntMatrix);
        Matrix3x2.Invert(shuttleToWorld, out var worldToShuttle);
        var shuttleToView  = Matrix3x2.CreateScale(new Vector2(MinimapScale, -MinimapScale))
                             * Matrix3x2.CreateTranslation(MidPointVector);

        // ── Draw own grid ──────────────────────────────────────────────────
        var ourGridId = xform.GridUid;
        if (ourGridId != null
            && EntManager.TryGetComponent<MapGridComponent>(ourGridId, out var ourGrid)
            && fixturesQuery.HasComponent(ourGridId.Value))
        {
            var ourGridToWorld   = _transform.GetWorldMatrix(ourGridId.Value);
            var ourGridToShuttle = Matrix3x2.Multiply(ourGridToWorld, worldToShuttle);
            var ourGridToView    = ourGridToShuttle * shuttleToView;
            var color = _shuttles.GetIFFColor(ourGridId.Value, self: true);
            DrawGrid(handle, ourGridToView, (ourGridId.Value, ourGrid), color);
        }

        // ── Radar centre dot ───────────────────────────────────────────────
        const float RV = 2f;
        var radarVerts = new Vector2[]
        {
            ScalePosition(new Vector2(0f, -RV)),
            ScalePosition(new Vector2(RV / 2f, 0f)),
            ScalePosition(new Vector2(0f, RV)),
            ScalePosition(new Vector2(RV / -2f, 0f)),
        };
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, radarVerts, Color.Lime);

        var rot        = ourEntRot + _rotation.Value;
        var viewBounds = new Box2Rotated(
            new Box2(-WorldRange, -WorldRange, WorldRange, WorldRange).Translated(mapPos.Position),
            rot, mapPos.Position);
        var viewAABB = viewBounds.CalcBoundingBox();

        _grids.Clear();
        _mapManager.FindGridsIntersecting(
            xform.MapID,
            new Box2(mapPos.Position - MaxRadarRangeVector, mapPos.Position + MaxRadarRangeVector),
            ref _grids, approx: true, includeMap: false);

        // ── Draw other grids ───────────────────────────────────────────────
        foreach (var grid in _grids)
        {
            var gUid = grid.Owner;
            if (gUid == ourGridId || !fixturesQuery.HasComponent(gUid))
                continue;

            var gridBody = bodyQuery.GetComponent(gUid);
            EntManager.TryGetComponent<IFFComponent>(gUid, out var iff);
            if (!_shuttles.CanDraw(gUid, gridBody, iff))
                continue;

            var curGridToWorld = _transform.GetWorldMatrix(gUid);
            var curGridToView  = curGridToWorld * worldToShuttle * shuttleToView;
            var labelColor     = _shuttles.GetIFFColor(grid, self: false, iff);
            var labelName      = _shuttles.GetIFFLabel(grid, self: false, iff);

            var gridAABB = curGridToWorld.TransformBox(grid.Comp.LocalAABB);
            if (!gridAABB.Intersects(viewAABB))
                continue;

            DrawGrid(handle, curGridToView, grid, labelColor);

            if (labelName != null)
            {
                var gridBody2    = bodyQuery.GetComponent(gUid);
                var gridCentre   = Vector2.Transform(gridBody2.LocalCenter, curGridToView);
                handle.DrawString(Font, gridCentre, labelName, labelColor);
            }
        }

        // ── Common world-to-view matrix ────────────────────────────────────
        var blipWorldToView = worldToShuttle * shuttleToView;

        // ── Standard radar blips ───────────────────────────────────────────
        foreach (var blip in _blips)
        {
            var blipCoords    = EntManager.GetCoordinates(blip.Coordinates);
            var blipMapCoords = _transform.ToMapCoordinates(blipCoords);
            if (blipMapCoords.MapId != mapPos.MapId)
                continue;
            if ((blipMapCoords.Position - mapPos.Position).LengthSquared() > WorldRange * WorldRange)
                continue;

            var blipScreen = Vector2.Transform(blipMapCoords.Position, blipWorldToView);
            switch (blip.Shape)
            {
                case BlipShape.Circle:
                    DrawBlipCircle(handle, blipScreen, blip.Color, blip.Scale);
                    break;
                case BlipShape.Square:
                    DrawBlipSquare(handle, blipScreen, blip.Color, blip.Scale);
                    break;
                default:
                    DrawBlipTriangle(handle, blipScreen, blip.Color, blip.Scale);
                    break;
            }
        }

        // ── Laser traces ───────────────────────────────────────────────────
        foreach (var laser in _lasers)
        {
            var originCoords    = EntManager.GetCoordinates(laser.Origin);
            var originMapCoords = _transform.ToMapCoordinates(originCoords);
            if (originMapCoords.MapId != mapPos.MapId)
                continue;

            var originScreen = Vector2.Transform(originMapCoords.Position, blipWorldToView);
            var endScreen    = Vector2.Transform(originMapCoords.Position + laser.Direction * laser.Length, blipWorldToView);
            handle.DrawLine(originScreen, endScreen, laser.Color.WithAlpha(0.9f));
            handle.DrawLine(originScreen, endScreen, laser.Color.WithAlpha(0.35f));
        }

        // ── Cannon blips (orange diamonds) ────────────────────────────────
        foreach (var cannon in _cannons)
        {
            var cannonCoords    = EntManager.GetCoordinates(cannon.Coordinates);
            var cannonMapCoords = _transform.ToMapCoordinates(cannonCoords);
            if (cannonMapCoords.MapId != mapPos.MapId)
                continue;

            var cannonScreen = Vector2.Transform(cannonMapCoords.Position, blipWorldToView);
            var isSelected   = SelectedCannons.Contains(cannon.Entity);
            var blipColor    = isSelected ? Color.Yellow : new Color(1f, 0.6f, 0.1f);
            DrawBlipDiamond(handle, cannonScreen, blipColor, isSelected ? 1.4f : 1.0f);

            // Label cannon name below the blip (short name).
            var shortName = cannon.Name.Length > 10 ? cannon.Name[..10] + "…" : cannon.Name;
            var labelDim  = handle.GetDimensions(Font, shortName, 0.8f);
            handle.DrawString(Font, cannonScreen + new Vector2(-labelDim.X / 2f, 10f), shortName, 0.8f, blipColor);
        }

        // ── Aim lines + coordinate readout: all selected cannons → cursor ──
        if (SelectedCannons.Count > 0 && _cursorRelativePos != null)
        {
            var cursor   = _cursorRelativePos.Value;
            var aimColor = _trackedGuidedProjectile != null
                ? new Color(0.3f, 1f, 0.3f)   // green = guidance active
                : new Color(1f, 0.85f, 0.1f);  // gold = normal aim

            foreach (var cannon in _cannons)
            {
                if (!SelectedCannons.Contains(cannon.Entity))
                    continue;

                var cannonCoords    = EntManager.GetCoordinates(cannon.Coordinates);
                var cannonMapCoords = _transform.ToMapCoordinates(cannonCoords);
                if (cannonMapCoords.MapId != mapPos.MapId)
                    continue;

                var cannonScreen = Vector2.Transform(cannonMapCoords.Position, blipWorldToView);
                handle.DrawLine(cannonScreen, cursor, aimColor.WithAlpha(0.85f));
            }

            // Crosshair at cursor.
            const float CH = 6f;
            handle.DrawLine(cursor - new Vector2(CH, 0), cursor + new Vector2(CH, 0), aimColor);
            handle.DrawLine(cursor - new Vector2(0, CH), cursor + new Vector2(0, CH), aimColor);
            handle.DrawCircle(cursor, CH, aimColor.WithAlpha(0.25f));

            // Coordinate readout just above the crosshair.
            var worldTarget = ScreenToWorld(cursor);
            var coordText   = $"({worldTarget.X:F1}, {worldTarget.Y:F1})";
            var coordDim    = handle.GetDimensions(Font, coordText, 0.75f);
            handle.DrawString(Font,
                cursor + new Vector2(-coordDim.X / 2f, -CH - coordDim.Y - 2f),
                coordText, 0.75f, aimColor);
        }

        // ── Guidance indicator ─────────────────────────────────────────────
        if (_trackedGuidedProjectile != null)
        {
            const string GuidanceText = "GUIDANCE ACTIVE — hold LMB to steer";
            var dim = handle.GetDimensions(Font, GuidanceText, 1f);
            handle.DrawString(Font,
                new Vector2(PixelWidth / 2f - dim.X / 2f, PixelHeight - dim.Y - 8f),
                GuidanceText, Color.LimeGreen);
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>Converts a control-local pixel position to EntityCoordinates.</summary>
    private EntityCoordinates ScreenToWorld(Vector2 relativePos)
    {
        if (_coordinates == null || _rotation == null)
            return default;

        var xformQuery = EntManager.GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryGetComponent(_coordinates.Value.EntityId, out var xform)
            || xform.MapID == MapId.Nullspace)
            return default;

        var ourEntRot    = _rotateWithEntity ? _transform.GetWorldRotation(xform) : _rotation.Value;
        var ourEntMatrix = Matrix3Helpers.CreateTransform(_transform.GetWorldPosition(xform), ourEntRot);
        var posMatrix    = Matrix3Helpers.CreateTransform(_coordinates.Value.Position, _rotation.Value);
        var shuttleToWorld = Matrix3x2.Multiply(posMatrix, ourEntMatrix);
        Matrix3x2.Invert(shuttleToWorld, out var worldToShuttle);
        var shuttleToView  = Matrix3x2.CreateScale(new Vector2(MinimapScale, -MinimapScale))
                             * Matrix3x2.CreateTranslation(MidPointVector);

        // Build view→world matrix by inverting worldToShuttle*shuttleToView, then
        // map the pixel position all the way back to map-space.
        var worldToView = worldToShuttle * shuttleToView;
        Matrix3x2.Invert(worldToView, out var viewToWorld);

        var mapWorldPos = Vector2.Transform(relativePos, viewToWorld);

        // Express as EntityCoordinates on the console's parent (the grid).
        // Convert map-space → grid-local using the grid's world→local matrix.
        var gridUid = xform.GridUid ?? _coordinates.Value.EntityId;
        var gridWorldMatrix = _transform.GetWorldMatrix(gridUid);
        Matrix3x2.Invert(gridWorldMatrix, out var worldToGrid);
        var gridLocalPos = Vector2.Transform(mapWorldPos, worldToGrid);
        return new EntityCoordinates(gridUid, gridLocalPos);
    }

    private Vector2 InverseScalePosition(Vector2 value)
    {
        return (value - MidPointVector) / MinimapScale;
    }

    /// <summary>
    /// Checks whether the given control-local click position is close enough to a
    /// cannon blip to select it.  Returns true (and sets SelectedCannon) if a
    /// cannon was hit.
    /// </summary>
    private bool TrySelectCannonAt(Vector2 clickRelativePos)
    {
        if (_coordinates == null || _rotation == null)
            return false;

        var xformQuery = EntManager.GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryGetComponent(_coordinates.Value.EntityId, out var xform)
            || xform.MapID == MapId.Nullspace)
            return false;

        var mapPos      = _transform.ToMapCoordinates(_coordinates.Value);
        var ourEntRot   = _rotateWithEntity ? _transform.GetWorldRotation(xform) : _rotation.Value;
        var ourEntMatrix = Matrix3Helpers.CreateTransform(_transform.GetWorldPosition(xform), ourEntRot);
        var posMatrix   = Matrix3Helpers.CreateTransform(_coordinates.Value.Position, _rotation.Value);
        var shuttleToWorld = Matrix3x2.Multiply(posMatrix, ourEntMatrix);
        Matrix3x2.Invert(shuttleToWorld, out var worldToShuttle);
        var shuttleToView = Matrix3x2.CreateScale(new Vector2(MinimapScale, -MinimapScale))
                            * Matrix3x2.CreateTranslation(MidPointVector);
        var blipWorldToView = worldToShuttle * shuttleToView;

        foreach (var cannon in _cannons)
        {
            var cannonCoords    = EntManager.GetCoordinates(cannon.Coordinates);
            var cannonMapCoords = _transform.ToMapCoordinates(cannonCoords);
            if (cannonMapCoords.MapId != mapPos.MapId)
                continue;

            var cannonScreen = Vector2.Transform(cannonMapCoords.Position, blipWorldToView);
            if ((clickRelativePos - cannonScreen).Length() <= CannonSelectRadius)
            {
                // Toggle: clicking a selected cannon deselects it; clicking
                // an unselected cannon adds it to the selection.
                if (!SelectedCannons.Remove(cannon.Entity))
                    SelectedCannons.Add(cannon.Entity);
                OnSelectionChanged?.Invoke();
                return true;
            }
        }

        return false;
    }

    // ── Blip drawing helpers ───────────────────────────────────────────────

    private static void DrawBlipTriangle(DrawingHandleScreen handle, Vector2 center, Color color, float scale)
    {
        const float S = 7f;
        var s = S * scale;
        var verts = new Vector2[] { center + new Vector2(0, -s), center + new Vector2(-s * 0.65f, s * 0.5f), center + new Vector2(s * 0.65f, s * 0.5f) };
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, (ReadOnlySpan<Vector2>)verts, color.WithAlpha(0.85f));
        handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, (ReadOnlySpan<Vector2>)new Vector2[] { verts[0], verts[1], verts[2], verts[0] }, color);
    }

    private static void DrawBlipCircle(DrawingHandleScreen handle, Vector2 center, Color color, float scale)
    {
        var r = 5f * scale;
        handle.DrawCircle(center, r, color.WithAlpha(0.85f));
        handle.DrawCircle(center, r, color, false);
    }

    private static void DrawBlipSquare(DrawingHandleScreen handle, Vector2 center, Color color, float scale)
    {
        var h = 5f * scale;
        var verts = new Vector2[] { center + new Vector2(-h, -h), center + new Vector2(h, -h), center + new Vector2(h, h), center + new Vector2(-h, h) };
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, (ReadOnlySpan<Vector2>)verts, color.WithAlpha(0.85f));
        handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, (ReadOnlySpan<Vector2>)new Vector2[] { verts[0], verts[1], verts[2], verts[3], verts[0] }, color);
    }

    /// <summary>Draws a filled diamond (rotated square) — used for cannon blips.</summary>
    private static void DrawBlipDiamond(DrawingHandleScreen handle, Vector2 center, Color color, float scale)
    {
        var h = 8f * scale;
        var top    = center + new Vector2(0, -h);
        var right  = center + new Vector2(h, 0);
        var bottom = center + new Vector2(0, h);
        var left   = center + new Vector2(-h, 0);
        var verts  = new Vector2[] { top, right, bottom, left };
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, (ReadOnlySpan<Vector2>)verts, color.WithAlpha(0.80f));
        handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, (ReadOnlySpan<Vector2>)new Vector2[] { top, right, bottom, left, top }, color);
    }
}
