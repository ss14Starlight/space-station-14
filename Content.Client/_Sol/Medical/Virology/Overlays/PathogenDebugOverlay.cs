using System.Globalization;
using System.Numerics;
using Content.Client.Resources;
using Content.Shared.Atmos;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using DebugMessage = Content.Shared._Sol.Medical.Virology.Components.PathogenDebugOverlayMessage;

namespace Content.Client._Sol.Medical.Virology.Overlays;

public sealed class PathogenDebugOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    private readonly SharedTransformSystem _transform;
    private readonly PathogenDebugOverlaySystem _system;
    private readonly SharedMapSystem _map;
    private readonly Font _font;
    private List<(Entity<MapGridComponent>, DebugMessage)> _grids = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    public PathogenDebugOverlay(PathogenDebugOverlaySystem system)
    {
        IoCManager.InjectDependencies(this);
        _system = system;
        _transform = _entManager.System<SharedTransformSystem>();
        _map = _entManager.System<SharedMapSystem>();
        _font = _cache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 12);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Space == OverlaySpace.ScreenSpace)
        {
            DrawTooltip(args);
            return;
        }

        var handle = args.WorldHandle;
        GetGrids(args.MapId, args.WorldBounds);

        foreach (var (grid, msg) in _grids)
        {
            handle.SetTransform(_transform.GetWorldMatrix(grid));
            foreach (var data in msg.OverlayData)
            {
                if (data != null)
                    DrawTile(data.Value, handle);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawTile(Content.Shared._Sol.Medical.Virology.Components.PathogenDebugOverlayTile data, DrawingHandleWorld handle)
    {
        var fill = GetFill(data);
        var interp = Math.Clamp((fill - _system.CfgBase) / Math.Max(0.001f, _system.CfgScale), 0f, 1f);
        var color = _system.CfgCBM
            ? Color.InterpolateBetween(Color.Black, Color.White, interp)
            : interp < 0.5f
                ? Color.InterpolateBetween(Color.LimeGreen, Color.Yellow, interp * 2)
                : Color.InterpolateBetween(Color.Yellow, Color.Red, (interp - 0.5f) * 2);

        handle.DrawRect(Box2.FromDimensions(new Vector2(data.Indices.X, data.Indices.Y), Vector2.One), color.WithAlpha(0.65f));

        var centre = data.Indices + 0.5f * Vector2.One;
        DrawBlocked(handle, data.BlockedDirections, AtmosDirection.North, centre);
        DrawBlocked(handle, data.BlockedDirections, AtmosDirection.South, centre);
        DrawBlocked(handle, data.BlockedDirections, AtmosDirection.East, centre);
        DrawBlocked(handle, data.BlockedDirections, AtmosDirection.West, centre);
    }

    private float GetFill(Content.Shared._Sol.Medical.Virology.Components.PathogenDebugOverlayTile data)
    {
        if (_system.CfgMode == PathogenDebugOverlayMode.SpecificPathogen &&
            _system.CfgSpecificPathogen != null)
        {
            foreach (var (id, load) in data.Entries)
            {
                if (id == _system.CfgSpecificPathogen)
                    return load;
            }

            return 0f;
        }

        return data.TotalLoad;
    }

    private static void DrawBlocked(DrawingHandleWorld handle, AtmosDirection blocked, AtmosDirection dir, Vector2 centre)
    {
        if (!blocked.HasFlag(dir))
            return;

        var atmosAngle = dir.ToAngle() - Angle.FromDegrees(90);
        var ofs = atmosAngle.ToVec() * 0.45f;
        var r90 = new Vector2(ofs.Y, -ofs.X);
        handle.DrawLine(centre + ofs - r90, centre + ofs + r90, Color.Azure);
    }

    private void DrawTooltip(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var mousePos = _input.MouseScreenPosition;
        if (!mousePos.IsValid)
            return;

        if (_ui.MouseGetControl(mousePos) is not IViewportControl viewport)
            return;

        var coords = viewport.PixelToMap(mousePos.Position);
        GetGrids(coords.MapId, new Box2Rotated(Box2.CenteredAround(coords.Position, 3 * Vector2.One)));

        foreach (var (grid, msg) in _grids)
        {
            var index = _map.WorldToTile(grid, grid, coords.Position);
            foreach (var data in msg.OverlayData)
            {
                if (data?.Indices != index)
                    continue;

                var pos = mousePos.Position;
                var line = _font.GetLineHeight(1f);
                handle.DrawString(_font, pos, $"Total: {data.Value.TotalLoad.ToString(CultureInfo.InvariantCulture)}");
                pos += new Vector2(0, line);
                foreach (var (id, load) in data.Value.Entries)
                {
                    handle.DrawString(_font, pos, $"{id}: {load.ToString("F2", CultureInfo.InvariantCulture)}");
                    pos += new Vector2(0, line);
                }

                handle.DrawString(_font, pos, $"Blocked: {data.Value.BlockedDirections}");
                return;
            }
        }
    }

    private void GetGrids(MapId mapId, Box2Rotated box)
    {
        _grids.Clear();
        _map.FindGridsIntersecting(mapId, box, ref _grids,
            (EntityUid uid, MapGridComponent grid, ref List<(Entity<MapGridComponent>, DebugMessage)> state) =>
            {
                if (_system.TileData.TryGetValue(uid, out var data))
                    state.Add(((uid, grid), data));
                return true;
            });
    }
}
