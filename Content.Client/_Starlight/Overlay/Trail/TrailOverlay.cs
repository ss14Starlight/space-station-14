using System.Numerics;
using Content.Client._Starlight.Shaders;
using Content.Client._Starlight.Trail;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._Starlight.Overlay.Trail;

public sealed class TrailOverlay : Robust.Client.Graphics.Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    private readonly IEntityManager _entMan;
    private readonly IStarlightShaderManager _shaderMan;
    private readonly List<Vector2> _verts = [];
    private readonly List<Vector2> _ribbon = [];

    public TrailOverlay(IEntityManager entMan, IStarlightShaderManager shaderMan)
    {
        _entMan = entMan;
        _shaderMan = shaderMan;
        ZIndex = (int)Shared.DrawDepth.DrawDepth.Effects;
    }

    public bool Enabled { get; set; } = true;

    private const int MaxTrails = 10;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!Enabled)
            return;

        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);

        var drawn = 0;
        var query = _entMan.EntityQueryEnumerator<TrailComponent>();
        while (query.MoveNext(out var comp))
        {
            if (comp.Points.Count < 2)
                continue;

            DrawTrail(handle, comp, args);

            if (++drawn >= MaxTrails)
                break;
        }
    }

    private void DrawTrail(DrawingHandleWorld handle, TrailComponent comp, in OverlayDrawArgs args)
    {
        var points = comp.Points;
        var baseColor = comp.TrailColor;
        var halfWidth = comp.LineWidth * 0.5f;

        var shader = _shaderMan.GetShader(comp.Shader);
        if (shader != null)
        {
            var totalLen = 0f;
            for (var j = 0; j < points.Count - 1; j++)
                totalLen += (points[j + 1] - points[j]).Length();

            var viewport = args.Viewport;
            var size = viewport.Size;

            var tail = points[0];
            var head = points[^1];
            var tailLocal = viewport.WorldToLocal(tail);
            var headLocal = viewport.WorldToLocal(head);
            var tailUV = new Vector2(tailLocal.X / size.X, 1f - (tailLocal.Y / size.Y));
            var headUV = new Vector2(headLocal.X / size.X, 1f - (headLocal.Y / size.Y));

            shader.SetParameter("totalLength", totalLen);
            shader.SetParameter("headPos", headUV);
            shader.SetParameter("tailPos", tailUV);

            handle.UseShader(shader);
        }

        // Build ribbon vertices (left/right pairs)
        _ribbon.Clear();
        _ribbon.EnsureCapacity(points.Count * 2);

        for (var i = 0; i < points.Count; i++)
        {
            Vector2 perp;
            if (i == 0)
            {
                var dir = points[1] - points[0];
                var len = dir.Length();
                perp = len > 0.001f ? new Vector2(-dir.Y, dir.X) / len : Vector2.UnitX;
            }
            else if (i == points.Count - 1)
            {
                var dir = points[i] - points[i - 1];
                var len = dir.Length();
                perp = len > 0.001f ? new Vector2(-dir.Y, dir.X) / len : Vector2.UnitX;
            }
            else
            {
                var avg = points[i] - points[i - 1] + (points[i + 1] - points[i]);
                var len = avg.Length();
                perp = len > 0.001f ? new Vector2(-avg.Y, avg.X) / len : Vector2.UnitX;
            }

            var t = (float)i / (points.Count - 1);
            var w = halfWidth * t;

            _ribbon.Add(points[i] + (perp * w));
            _ribbon.Add(points[i] - (perp * w));
        }

        // Convert ribbon + cap into a single TriangleList
        _verts.Clear();

        for (var i = 0; i < _ribbon.Count - 3; i += 2)
        {
            _verts.Add(_ribbon[i]);
            _verts.Add(_ribbon[i + 1]);
            _verts.Add(_ribbon[i + 2]);

            _verts.Add(_ribbon[i + 1]);
            _verts.Add(_ribbon[i + 3]);
            _verts.Add(_ribbon[i + 2]);
        }

        // Semicircle cap at the head
        if (points.Count >= 2)
        {
            var head = points[^1];
            var trailLen = 0f;
            for (var j = 0; j < points.Count - 1; j++)
                trailLen += (points[j + 1] - points[j]).Length();

            var radius = MathF.Min(halfWidth, trailLen);
            if (radius > 0.01f)
            {
                var lastDir = points[^1] - points[^2];
                var lastLen = lastDir.Length();
                var fwd = lastLen > 0.001f ? lastDir / lastLen : Vector2.UnitX;

                const int CapSegs = 8;
                var startAngle = MathF.Atan2(fwd.Y, fwd.X) - (MathF.PI * 0.5f);
                for (var s = 0; s < CapSegs; s++)
                {
                    var a0 = startAngle + (s / (float)CapSegs * MathF.PI);
                    var a1 = startAngle + ((s + 1) / (float)CapSegs * MathF.PI);
                    _verts.Add(head);
                    _verts.Add(head + (new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius));
                    _verts.Add(head + (new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius));
                }
            }
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _verts.ToArray(), baseColor);

        if (shader != null)
            handle.UseShader(null);
    }
}
