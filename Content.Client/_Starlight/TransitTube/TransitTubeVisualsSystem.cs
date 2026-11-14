using System.Numerics;
using Content.Shared._Starlight.TransitTube;
using Content.Shared.Disposal.Tube;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.TransitTube;

/// <summary>
/// Decorations of transit tubes: the corner pieces that fill in diagonal joints.
/// Also hides the direction arrow on real pods: it is only meant for the placement ghost, which is client-side.
/// </summary>
public sealed partial class TransitTubeVisualsSystem : EntitySystem
{
    [Dependency] private DisposalTubeSystem _tube = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ResPath _rsi = new("/Textures/_Starlight/Structures/Piping/TransitTube/tube.rsi");
    private const string PodArrowKey = "direction";

    /// <summary>
    /// Sprite layer holding a piece's glass, tinted by its material. Details stay untinted.
    /// </summary>
    private const string GlassKey = "glass";

    /// <summary>
    /// Tubes whose decorations need redoing. Updated a tick later, so a tube that is being deleted
    /// is already gone from its neighbours' point of view.
    /// </summary>
    private readonly HashSet<EntityUid> _dirty = [];

    [SubscribeLocalEvent]
    private void OnStartup(Entity<TransitTubeComponent> ent, ref ComponentStartup args)
        => MarkWithNeighbours(ent);

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<TransitTubeComponent> ent, ref ComponentShutdown args)
        => MarkWithNeighbours(ent);

    [SubscribeLocalEvent]
    private void OnMove(Entity<TransitTubeComponent> ent, ref MoveEvent args)
    {
        // A moved tube leaves neighbours behind and may join new ones.
        if (args.OldPosition.EntityId.IsValid() && args.OldPosition != args.NewPosition)
            MarkNeighbours(args.OldPosition);

        MarkWithNeighbours(ent);
    }

    [SubscribeLocalEvent]
    private void OnAnchorChanged(Entity<TransitTubeComponent> ent, ref AnchorStateChangedEvent args)
        => MarkWithNeighbours(ent);

    [SubscribeLocalEvent]
    private void OnGlassStartup(Entity<TransitTubeGlassComponent> ent, ref ComponentStartup args)
        => _dirty.Add(ent);

    [SubscribeLocalEvent]
    private void OnGlassState(Entity<TransitTubeGlassComponent> ent, ref AfterAutoHandleStateEvent args)
        => _dirty.Add(ent);

    [SubscribeLocalEvent]
    private void OnPodStartup(Entity<TransitPodComponent> ent, ref ComponentStartup args)
    {
        if (IsClientSide(ent) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (_sprite.LayerMapTryGet((ent, sprite), PodArrowKey, out var index, false))
            _sprite.LayerSetVisible((ent, sprite), index, false);
    }

    [SubscribeLocalEvent]
    private void OnPodAppearance(Entity<TransitPodComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        _appearance.TryGetData<TransitTubeLevel>(ent, TransitPodVisuals.Level, out var level, args.Component);
        _appearance.TryGetData<bool>(ent, TransitPodVisuals.Hidden, out var hidden, args.Component);

        var depth = level switch
        {
            TransitTubeLevel.Under => Shared.DrawDepth.DrawDepth.ThinWire,
            TransitTubeLevel.Over => Shared.DrawDepth.DrawDepth.Overdoors,
            _ => Shared.DrawDepth.DrawDepth.Mobs,
        };

        _sprite.SetDrawDepth((ent, sprite), (int)depth);
        _sprite.SetVisible((ent, sprite), !hidden);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_dirty.Count == 0)
            return;

        foreach (var uid in _dirty)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (TryComp<TransitTubeComponent>(uid, out var comp))
                UpdateDecorations((uid, comp));

            UpdateGlass(uid);
        }

        _dirty.Clear();
    }

    private void MarkWithNeighbours(EntityUid uid)
    {
        _dirty.Add(uid);
        MarkNeighbours(Transform(uid).Coordinates);
    }

    private void MarkNeighbours(EntityCoordinates coordinates)
    {
        if (_xform.GetGrid(coordinates) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var center = _map.TileIndicesFor(gridUid, grid, coordinates);
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                var anchored = _map.GetAnchoredEntities(gridUid, grid, center + new Vector2i(x, y));
                while (anchored.MoveNext(out var neighbour))
                {
                    if (HasComp<TransitTubeComponent>(neighbour))
                        _dirty.Add(neighbour.Value);
                }
            }
        }
    }

    private void UpdateDecorations(Entity<TransitTubeComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var spriteComp) || !TryComp<DisposalTubeComponent>(ent, out var tube))
            return;

        var sprite = new Entity<SpriteComponent?>(ent, spriteComp);
        var xform = Transform(ent);
        var rotation = xform.LocalRotation;
        var corner = 0;

        if (xform.Anchored && xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
        {
            var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            var level = ent.Comp.Level;

            // Diagonal joints running past this tile, between the tubes beside it: the corner piece goes
            // under this tube, so it never covers it.
            foreach (var dx in new[] { -1, 1 })
            {
                foreach (var dy in new[] { -1, 1 })
                {
                    if (JointBetween((gridUid, grid), tile + new Vector2i(dx, 0), new Vector2i(-dx, dy), level))
                        SetCorner(sprite, corner++, Vector2.Zero, CornerState(ToLocal(new Vector2i(dx, dy), rotation)));
                }
            }

            // Joints of this tube with one up north: it fills in the corners beside them itself,
            // but only where no tube on the same level stands to draw them under itself.
            foreach (var exit in _tube.GetTubeConnectableDirections((ent, tube)))
            {
                if (exit is not Direction.NorthEast and not Direction.NorthWest
                    || _tube.GetTubeInDirection((ent, tube), exit) == null)
                    continue;

                var world = exit.ToIntVec();
                var side = new Vector2i(world.X, 0);
                var above = new Vector2i(0, world.Y);

                if (!HasTubeOnLevel((gridUid, grid), tile + side, level))
                    SetCorner(sprite, corner++, ToLocal(side, rotation), CornerState(ToLocal(new Vector2i(-world.X, world.Y), rotation)));

                if (!HasTubeOnLevel((gridUid, grid), tile + above, level))
                    SetCorner(sprite, corner++, ToLocal(above, rotation), CornerState(ToLocal(new Vector2i(world.X, -world.Y), rotation)));
            }
        }

        // Hide whatever corners are left over from before.
        for (; ; corner++)
        {
            var found = false;
            foreach (var key in new[] { CornerKey(corner), CornerDetailsKey(corner) })
            {
                if (!_sprite.LayerMapTryGet(sprite, key, out var index, false))
                    continue;

                found = true;
                _sprite.LayerSetVisible(sprite, index, false);
            }

            if (!found)
                break;
        }
    }

    /// <summary>
    /// Whether a tube on <paramref name="level"/> at <paramref name="from"/> joins one diagonally,
    /// going <paramref name="direction"/>.
    /// </summary>
    private bool JointBetween(Entity<MapGridComponent> grid, Vector2i from, Vector2i direction, TransitTubeLevel level)
    {
        var exit = new Angle(new Vector2(direction.X, direction.Y)).GetDir();
        var anchored = _map.GetAnchoredEntities(grid, grid.Comp, from);
        while (anchored.MoveNext(out var uid))
        {
            if (!TryComp<TransitTubeComponent>(uid, out var transit)
                || transit.Level != level
                || !TryComp<DisposalTubeComponent>(uid, out var tube))
                continue;

            if (Array.IndexOf(_tube.GetTubeConnectableDirections((uid.Value, tube)), exit) >= 0
                && _tube.GetTubeInDirection((uid.Value, tube), exit) != null)
                return true;
        }

        return false;
    }

    private bool HasTubeOnLevel(Entity<MapGridComponent> grid, Vector2i tile, TransitTubeLevel level)
    {
        var anchored = _map.GetAnchoredEntities(grid, grid.Comp, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (TryComp<TransitTubeComponent>(uid, out var transit) && transit.Level == level && HasComp<SpriteComponent>(uid))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Turns a grid offset into the tube's own frame: layers rotate with the tube.
    /// </summary>
    private static Vector2i ToLocal(Vector2i world, Angle rotation)
    {
        var local = (-rotation).RotateVec(new Vector2(world.X, world.Y));
        return new Vector2i((int)MathF.Round(local.X), (int)MathF.Round(local.Y));
    }

    private void SetCorner(Entity<SpriteComponent?> sprite, int corner, Vector2 offset, string state)
    {
        // Glass and its frame, like the pieces themselves, under the piece's own layers.
        var glass = SetLayer(sprite, CornerKey(corner), state, offset, corner * 2);
        SetLayer(sprite, CornerDetailsKey(corner), state + "_details", offset, corner * 2 + 1);
        _sprite.LayerSetColor(sprite, glass, GetGlassColor(sprite.Owner));
    }

    private int SetLayer(Entity<SpriteComponent?> sprite, string key, string state, Vector2 offset, int insertAt)
    {
        if (!_sprite.LayerMapTryGet(sprite, key, out var index, false))
        {
            index = _sprite.AddRsiLayer(sprite, state, _rsi, insertAt);
            _sprite.LayerMapSet(sprite, key, index);
        }
        else
        {
            _sprite.LayerSetRsiState(sprite, index, state);
        }

        _sprite.LayerSetOffset(sprite, index, offset);
        _sprite.LayerSetVisible(sprite, index, true);
        return index;
    }

    /// <summary>
    /// Tints the glass layer, and the glass of corner pieces, with the tube's material.
    /// </summary>
    private void UpdateGlass(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var spriteComp))
            return;

        var sprite = new Entity<SpriteComponent?>(uid, spriteComp);
        var color = GetGlassColor(uid);

        if (_sprite.LayerMapTryGet(sprite, GlassKey, out var glass, false))
            _sprite.LayerSetColor(sprite, glass, color);

        for (var corner = 0; _sprite.LayerMapTryGet(sprite, CornerKey(corner), out var index, false); corner++)
        {
            _sprite.LayerSetColor(sprite, index, color);
        }
    }

    private Color GetGlassColor(EntityUid uid)
        => TryComp<TransitTubeGlassComponent>(uid, out var glass) && _proto.Resolve(glass.Material, out var material)
        ? material.Color
        : Color.White;

    private static string CornerKey(int corner) => $"transit-corner-{corner}";

    private static string CornerDetailsKey(int corner) => $"transit-corner-{corner}-details";

    private static string CornerState(Vector2i towards) => (towards.X, towards.Y) switch
    {
        (> 0, > 0) => "corner_ne",
        (< 0, > 0) => "corner_nw",
        (> 0, _) => "corner_se",
        _ => "corner_sw",
    };
}
