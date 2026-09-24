using System.Numerics;
using System.Runtime.InteropServices;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Scent.Overlays;

/// <summary>
/// Draws a brief parabola-shaped flash at the screen edge toward a scent's source.
/// </summary>
public sealed partial class ScentSourcePingOverlay : Robust.Client.Graphics.Overlay
{
    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _player;
    private readonly IEyeManager _eye;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _xform;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private static readonly TimeSpan _duration = TimeSpan.FromSeconds(1.5);

    // Fractions of the viewport's shorter dimension.
    private const float HalfWidthFraction = 0.04f;
    private const float DepthFraction = 0.16f;
    private const float PeakAlpha = 0.55f;
    private const int Segments = 12;

    private readonly record struct ActiveFlash(Color Color, MapId MapId, Vector2 WorldPos, TimeSpan StartTime);

    private readonly List<ActiveFlash> _flashes = [];
    private readonly List<DrawVertexUV2DColor> _verts = [];

    public ScentSourcePingOverlay(IEntityManager entMan, IPlayerManager player, IEyeManager eye, IGameTiming timing)
    {
        _entMan = entMan;
        _player = player;
        _eye = eye;
        _timing = timing;
        _xform = entMan.EntitySysManager.GetEntitySystem<SharedTransformSystem>();
    }

    public void AddFlash(Color color, MapCoordinates target)
        => _flashes.Add(new ActiveFlash(color, target.MapId, target.Position, _timing.CurTime));

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        _flashes.RemoveAll(f => _timing.CurTime - f.StartTime > _duration);
        return _flashes.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var uid = _player.LocalEntity;
        if (uid == null || !_entMan.TryGetComponent(uid.Value, out TransformComponent? xform))
            return;

        var playerPos = _xform.GetWorldPosition(xform);
        var playerMap = xform.MapID;
        var bounds = args.ViewportBounds;
        var playerScreenPos = _eye.WorldToScreen(playerPos);

        foreach (var flash in _flashes)
        {
            if (flash.MapId != playerMap)
                continue;

            var elapsed = (float)(_timing.CurTime - flash.StartTime).TotalSeconds;
            var timeEnvelope = MathF.Sin(MathF.PI * Math.Clamp(elapsed / (float)_duration.TotalSeconds, 0f, 1f));
            if (timeEnvelope <= 0f)
                continue;

            var screenDir = _eye.WorldToScreen(flash.WorldPos) - playerScreenPos;
            if (screenDir.LengthSquared() < 0.01f)
                continue;

            DrawFlash(args.ScreenHandle, bounds, screenDir, flash.Color, timeEnvelope);
        }
    }

    private void DrawFlash(DrawingHandleScreen screen, UIBox2 bounds, Vector2 dir, Color color, float timeEnvelope)
    {
        var center = new Vector2(bounds.Width / 2f, bounds.Height / 2f);

        if (!TryGetEdgeHit(center, dir, bounds, out var edgePoint, out var tangent, out var inward))
            return;

        var shorterDim = MathF.Min(bounds.Width, bounds.Height);
        var halfWidth = shorterDim * HalfWidthFraction;
        var depth = shorterDim * DepthFraction;

        _verts.Clear();

        for (var i = 0; i < Segments; i++)
        {
            var x0 = MathHelper.Lerp(-halfWidth, halfWidth, i / (float)Segments);
            var x1 = MathHelper.Lerp(-halfWidth, halfWidth, (i + 1) / (float)Segments);

            var h0 = ParabolaHeight(x0, halfWidth);
            var h1 = ParabolaHeight(x1, halfWidth);

            var base0 = edgePoint + (tangent * x0);
            var base1 = edgePoint + (tangent * x1);
            var crest0 = base0 + (inward * (depth * h0));
            var crest1 = base1 + (inward * (depth * h1));

            var baseColor = color.WithAlpha(0f);
            var crest0Color = color.WithAlpha(PeakAlpha * h0 * timeEnvelope);
            var crest1Color = color.WithAlpha(PeakAlpha * h1 * timeEnvelope);

            _verts.Add(new DrawVertexUV2DColor(base0, Vector2.Zero, baseColor));
            _verts.Add(new DrawVertexUV2DColor(base1, Vector2.Zero, baseColor));
            _verts.Add(new DrawVertexUV2DColor(crest1, Vector2.Zero, crest1Color));

            _verts.Add(new DrawVertexUV2DColor(base0, Vector2.Zero, baseColor));
            _verts.Add(new DrawVertexUV2DColor(crest1, Vector2.Zero, crest1Color));
            _verts.Add(new DrawVertexUV2DColor(crest0, Vector2.Zero, crest0Color));
        }

        screen.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, CollectionsMarshal.AsSpan(_verts));
    }

    // A true parabola: 1 at the center, 0 at both edges.
    private static float ParabolaHeight(float x, float halfWidth)
        => MathF.Max(0f, 1f - (x / halfWidth * (x / halfWidth)));

    // Ray-AABB exit test from the screen center.
    private static bool TryGetEdgeHit(Vector2 center, Vector2 dir, UIBox2 bounds, out Vector2 edgePoint, out Vector2 tangent, out Vector2 inward)
    {
        edgePoint = default;
        tangent = default;
        inward = default;

        var tx = dir.X switch
        {
            > 0f => (bounds.Width - center.X) / dir.X,
            < 0f => (0f - center.X) / dir.X,
            _ => float.PositiveInfinity,
        };
        var ty = dir.Y switch
        {
            > 0f => (bounds.Height - center.Y) / dir.Y,
            < 0f => (0f - center.Y) / dir.Y,
            _ => float.PositiveInfinity,
        };

        if (float.IsPositiveInfinity(tx) && float.IsPositiveInfinity(ty))
            return false;

        var hitVertical = tx < ty;
        var t = hitVertical ? tx : ty;
        edgePoint = center + (dir * t);

        tangent = hitVertical ? new Vector2(0f, 1f) : new Vector2(1f, 0f);
        inward = hitVertical ? new Vector2(-MathF.Sign(dir.X), 0f) : new Vector2(0f, -MathF.Sign(dir.Y));
        return true;
    }
}
