using System.Globalization;
using System.Numerics;
using Content.Client.Resources;
using Content.Shared._Starlight.Shadekin;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using LightDebugOverlayMessage = Content.Shared._Starlight.Shadekin.SharedLightDebugOverlaySystem.LightDebugOverlayMessage;

namespace Content.Client._Starlight.Shadekin;

public sealed class LightDebugOverlay : Robust.Client.Graphics.Overlay
{
    private const float MaxColorIntensity = 15f;
    public const float LightByteScale = 16f;

    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    private readonly SharedTransformSystem _transform;
    private readonly SharedMapSystem _map;
    private readonly LightDebugOverlaySystem _system;
    private readonly Font _font;
    private List<(Entity<MapGridComponent>, LightDebugOverlayMessage)> _grids = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    internal LightDebugOverlay(LightDebugOverlaySystem system)
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
            DrawData(msg, handle);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void GetGrids(MapId mapId, Box2Rotated worldBounds)
    {
        _grids.Clear();
        var gridList = new List<Entity<MapGridComponent>>();
        _mapManager.FindGridsIntersecting(mapId, worldBounds, ref gridList, approx: true);

        foreach (var grid in gridList)
        {
            if (_system.TileData.TryGetValue(grid.Owner, out var msg))
                _grids.Add((grid, msg));
        }
    }

    private void DrawData(LightDebugOverlayMessage msg, DrawingHandleWorld handle)
    {
        var baseIdx = msg.BaseIdx;
        var range = SharedLightDebugOverlaySystem.LocalViewRange;

        for (var i = 0; i < msg.OverlayData.Length; i++)
        {
            var raw = msg.OverlayData[i];
            if (raw == 0)
                continue;

            var intensity = raw * (LightByteScale / 255f);

            var x = i % range;
            var y = i / range;
            var tile = new Vector2i(baseIdx.X + x, baseIdx.Y + y);

            var normalized = Math.Clamp(intensity / MaxColorIntensity, 0f, 1f);

            Color color;
            if (normalized < 0.5f)
            {
                color = Color.InterpolateBetween(Color.Black, Color.Yellow, normalized * 2f);
            }
            else
            {
                color = Color.InterpolateBetween(Color.Yellow, Color.White, (normalized - 0.5f) * 2f);
            }

            color = color.WithAlpha(0.65f);
            handle.DrawRect(Box2.FromDimensions(new Vector2(tile.X, tile.Y), new Vector2(1, 1)), color);
        }
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
        var box = Box2.CenteredAround(coords.Position, 3 * Vector2.One);
        GetGrids(coords.MapId, new Box2Rotated(box));

        foreach (var (grid, msg) in _grids)
        {
            var tileIdx = _map.WorldToTile(grid, grid, coords.Position);
            var baseIdx = msg.BaseIdx;
            var range = SharedLightDebugOverlaySystem.LocalViewRange;

            var localX = tileIdx.X - baseIdx.X;
            var localY = tileIdx.Y - baseIdx.Y;

            if (localX < 0 || localX >= range || localY < 0 || localY >= range)
                continue;

            var i = (localY * range) + localX;
            if (i < 0 || i >= msg.OverlayData.Length)
                continue;

            var raw = msg.OverlayData[i];
            if (raw == 0)
                continue;

            var intensity = raw * (LightByteScale / 255f);
            var state = GetShadekinState(intensity);
            var lineHeight = _font.GetLineHeight(1f);
            var offset = new Vector2(0, lineHeight);
            var pos = mousePos.Position + new Vector2(12, 12);

            handle.DrawString(_font, pos, $"Light: {intensity.ToString("F2", CultureInfo.InvariantCulture)}");
            pos += offset;
            handle.DrawString(_font, pos, $"State: {state}");
            pos += offset;
            handle.DrawString(_font, pos, $"Tile: ({tileIdx.X}, {tileIdx.Y})");
            return;
        }
    }

    private static ShadekinState GetShadekinState(float intensity)
    {
        if (intensity >= 15f)
            return ShadekinState.Extreme;
        if (intensity >= 10f)
            return ShadekinState.High;
        if (intensity >= 5f)
            return ShadekinState.Annoying;
        if (intensity >= 0.8f)
            return ShadekinState.Low;
        return ShadekinState.Dark;
    }
}
