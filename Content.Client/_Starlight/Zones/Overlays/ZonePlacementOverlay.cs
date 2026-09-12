using System.Numerics;
using Content.Client.Resources;
using Content.Shared._Starlight.Zones;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Zones.Overlays;

public sealed class ZonePlacementOverlay : Robust.Client.Graphics.Overlay
{
    private readonly ZonePlacementSystem _placement;
    private readonly SharedTransformSystem _transform;
    private readonly IEyeManager _eye;
    private readonly IPrototypeManager _proto;
    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities | OverlaySpace.ScreenSpace;

    private const float FillAlpha = 0.15f;

    private const float BorderWidth = 0.1f;

    private const float RoomFillAlpha = 0.18f;
    private const float RoomEdge = 0.12f;

    private const float MinLabelWidth = 48f;
    private const float MinLabelHeight = 20f;

    private const float MinLabelLuma = 0.7f;

    private static readonly Vector2[] _outline =
    [
        new(-1, -1), new(0, -1), new(1, -1),
        new(-1, 0), new(1, 0),
        new(-1, 1), new(0, 1), new(1, 1),
    ];

    public ZonePlacementOverlay(
        ZonePlacementSystem placement,
        SharedTransformSystem transform,
        IEyeManager eye,
        IPrototypeManager proto,
        IResourceCache cache)
    {
        _placement = placement;
        _transform = transform;
        _eye = eye;
        _proto = proto;
        _font = cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 11);

        ZIndex = 1000;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Space == OverlaySpace.ScreenSpace)
        {
            DrawLabels(args);
            return;
        }

        DrawAreas(args);
    }

    private void DrawRooms(DrawingHandleWorld handle)
    {
        var view = _placement.GetRooms(out var grid);

        if (view == null)
            return;

        handle.SetTransform(_transform.GetWorldMatrix(grid));

        foreach (var (origin, rooms) in view.Chunks)
        {
            for (var i = 0; i < rooms.Length; i++)
            {
                var room = rooms[i];

                if (room == 0)
                    continue;

                var tile = (origin * SharedZoneSystem.ChunkSize) + new Vector2i(
                    i >> 3,
                    i & (SharedZoneSystem.ChunkSize - 1));

                var color = RoomColour(view, room);
                var box = new Box2(tile.X, tile.Y, tile.X + 1, tile.Y + 1);

                handle.DrawRect(box, color.WithAlpha(RoomFillAlpha));

                if (view.RoomAt(tile + new Vector2i(1, 0)) != room)
                    handle.DrawRect(new Box2(box.Right - RoomEdge, box.Bottom, box.Right, box.Top), color);

                if (view.RoomAt(tile + new Vector2i(-1, 0)) != room)
                    handle.DrawRect(new Box2(box.Left, box.Bottom, box.Left + RoomEdge, box.Top), color);

                if (view.RoomAt(tile + new Vector2i(0, 1)) != room)
                    handle.DrawRect(new Box2(box.Left, box.Top - RoomEdge, box.Right, box.Top), color);

                if (view.RoomAt(tile + new Vector2i(0, -1)) != room)
                    handle.DrawRect(new Box2(box.Left, box.Bottom, box.Right, box.Bottom + RoomEdge), color);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private Color RoomColour(ZoneRoomView view, ushort room) =>
        (view.Zones.TryGetValue(room, out var zone)
        && _proto.TryIndex(zone, out var proto))
        ? proto.Color
        : Color.FromHsv(new Vector4(room * 0.61803f % 1f, 0.4f, 0.9f, 1f));

    private void DrawAreas(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        DrawRooms(handle);
        var (draw, grid, shapes) = _placement.GetDrawTarget();

        if (draw && shapes != null)
        {
            handle.SetTransform(_transform.GetWorldMatrix(grid));

            foreach (var set in shapes)
            {
                var color = _placement.ZoneColor(set.Zone);

                foreach (var rect in set.Rects)
                    DrawArea(handle, ToBox(rect), color);

                foreach (var circle in set.Circles)
                {
                    handle.DrawCircle(circle.Center, circle.Radius, color.WithAlpha(FillAlpha));
                    handle.DrawCircle(circle.Center, circle.Radius, color, false);
                }
            }

            handle.SetTransform(Matrix3x2.Identity);
        }

        if (!_placement.TryGetPreview(out var previewGrid, out var preview, out var previewColor))
            return;

        handle.SetTransform(_transform.GetWorldMatrix(previewGrid));
        DrawArea(handle, ToBox(preview), previewColor);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private static void DrawArea(DrawingHandleWorld handle, Box2 box, Color color)
    {
        handle.DrawRect(box, color.WithAlpha(FillAlpha));

        var width = MathF.Min(BorderWidth, MathF.Min(box.Width, box.Height) / 2f);

        handle.DrawRect(new Box2(box.Left, box.Bottom, box.Right, box.Bottom + width), color);
        handle.DrawRect(new Box2(box.Left, box.Top - width, box.Right, box.Top), color);
        handle.DrawRect(new Box2(box.Left, box.Bottom + width, box.Left + width, box.Top - width), color);
        handle.DrawRect(new Box2(box.Right - width, box.Bottom + width, box.Right, box.Top - width), color);
    }

    private void DrawLabels(in OverlayDrawArgs args)
    {
        var (draw, grid, shapes) = _placement.GetDrawTarget();

        if (!draw || shapes == null)
            return;

        var handle = args.ScreenHandle;
        var matrix = _transform.GetWorldMatrix(grid);

        foreach (var set in shapes)
        {
            if (!_proto.TryIndex(set.Zone, out var zone))
                continue;

            var name = zone.Name is { } loc ? Loc.GetString(loc) : zone.ID;

            foreach (var rect in set.Rects)
                DrawLabel(handle, matrix, ToBox(rect), name, zone.Color);

            foreach (var circle in set.Circles)
            {
                var box = Box2.CenteredAround(circle.Center, new Vector2(circle.Radius * 2f));
                DrawLabel(handle, matrix, box, name, zone.Color);
            }
        }
    }

    private void DrawLabel(DrawingHandleScreen handle, Matrix3x2 matrix, Box2 box, string name, Color color)
    {
        var bottomLeft = _eye.WorldToScreen(Vector2.Transform(box.BottomLeft, matrix));
        var topRight = _eye.WorldToScreen(Vector2.Transform(box.TopRight, matrix));

        var width = MathF.Abs(topRight.X - bottomLeft.X);
        var height = MathF.Abs(topRight.Y - bottomLeft.Y);

        if (width < MinLabelWidth || height < MinLabelHeight)
            return;

        var center = (bottomLeft + topRight) / 2f;
        var offset = new Vector2(MeasureWidth(name) / 2f, _font.GetLineHeight(1f) / 2f);
        var pos = (center - offset).Rounded();

        foreach (var shadow in _outline)
            handle.DrawString(_font, pos + shadow, name, Color.Black);

        handle.DrawString(_font, pos, name, Brighten(color));
    }

    private static Color Brighten(Color color)
    {
        var luma = (color.R * 0.299f) + (color.G * 0.587f) + (color.B * 0.114f);

        return luma >= MinLabelLuma
            ? color
            : Color.InterpolateBetween(color, Color.White, (MinLabelLuma - luma) / MinLabelLuma);
    }

    private float MeasureWidth(string text)
    {
        var width = 0f;

        foreach (var rune in text.EnumerateRunes())
            if (_font.GetCharMetrics(rune, 1f) is { } metrics)
                width += metrics.Advance;

        return width;
    }

    private static Box2 ToBox(Box2i rect) => new(rect.Left, rect.Bottom, rect.Right, rect.Top);
}
