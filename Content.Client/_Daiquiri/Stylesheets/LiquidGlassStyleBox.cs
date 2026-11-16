using System;
using System.Numerics;
using Content.Client.Viewport;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._Daiquiri.Stylesheets;

public sealed class LiquidGlassStyleBox : StyleBox
{
    private const float MinBlurArea = 16000f;

    private const float StripMaxDimension = 64f;
    private const float StripCornerFactor = 0.15f;

    private static readonly ProtoId<ShaderPrototype> LiquidGlassShader = "LiquidGlass";

    private readonly StyleBoxFlat _fallback;
    private readonly Color _tintColor;
    private readonly float _tintStrength;
    private readonly float _opacity;
    private readonly float _cornerRadius;
    private readonly float _blurPixels;
    private readonly float _refractionPixels;
    private readonly float _highlight;
    private readonly int _quality;
    private readonly bool _animate;

    private readonly bool _forceShader;

    private static readonly ushort[] QuadIndices = { 0, 1, 2, 0, 2, 3 };
    private readonly DrawVertexUV2DColor[] _vertices = new DrawVertexUV2DColor[4];

    private IEyeManager? _eyeManager;
    private ShaderInstance? _shader;
    private bool _shaderResolved;

    public LiquidGlassStyleBox(
        StyleBoxFlat source,
        GlassTheme.LiquidGlassParams pars,
        bool forceShader = false) : base(source)
    {
        _forceShader = forceShader;
        _fallback = new StyleBoxFlat(source)
        {
            PaddingLeft = 0,
            PaddingTop = 0,
            PaddingRight = 0,
            PaddingBottom = 0,
        };

        _tintColor = pars.TintColor ?? source.BackgroundColor;
        _tintStrength = pars.TintStrength;
        _opacity = pars.Opacity;
        _cornerRadius = pars.CornerRadius;
        _blurPixels = pars.BlurPixels;
        _refractionPixels = pars.RefractionPixels;
        _highlight = pars.Highlight;
        _quality = pars.Quality;
        _animate = pars.Animate;
    }

    protected override float GetDefaultContentMargin(Margin margin) => margin switch
    {
        Margin.Top => _fallback.BorderThickness.Top,
        Margin.Bottom => _fallback.BorderThickness.Bottom,
        Margin.Right => _fallback.BorderThickness.Right,
        Margin.Left => _fallback.BorderThickness.Left,
        _ => throw new ArgumentOutOfRangeException(nameof(margin), margin, null),
    };

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        if (box.Width < 2f || box.Height < 2f)
        {
            _fallback.Draw(handle, box, uiScale);
            return;
        }

        var hasBackdrop = TryGetBackdrop(out var viewport, out var texture);

        var shader = GetShader();
        if (shader == null)
        {
            _fallback.Draw(handle, box, uiScale);
            return;
        }

        var transform = Matrix3x2.Identity;
        var screenToViewport = Matrix3x2.Identity;
        var viewportSize = Vector2.One;

        if (hasBackdrop)
        {
            transform = handle.GetTransform();
            if (!Matrix3x2.Invert(viewport.GetLocalToScreenMatrix(), out screenToViewport))
            {
                _fallback.Draw(handle, box, uiScale);
                return;
            }

            viewportSize = (Vector2) viewport.ClydeViewport!.Size;
            if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
            {
                _fallback.Draw(handle, box, uiScale);
                return;
            }
        }
        else
        {
            texture = Texture.White;
        }

        Vector2 ToViewportUv(Vector2 local, Vector2 uv)
        {
            if (!hasBackdrop)
                return uv;

            var screen = Vector2.Transform(local, transform);
            var viewportLocal = Vector2.Transform(screen, screenToViewport);
            return new Vector2(
                viewportLocal.X / viewportSize.X,
                1f - viewportLocal.Y / viewportSize.Y);
        }

        var topLeft = new Vector2(box.Left, box.Top);
        var topRight = new Vector2(box.Right, box.Top);
        var bottomRight = new Vector2(box.Right, box.Bottom);
        var bottomLeft = new Vector2(box.Left, box.Bottom);

        var cornerRadius = CornerRadiusFor(box, uiScale);

        var quality = box.Width * box.Height < MinBlurArea ? 0 : _quality;

        shader.SetParameter("SCREEN_TEXTURE", texture);
        shader.SetParameter("CornerRadius", cornerRadius);
        shader.SetParameter("BlurPixels", _blurPixels * uiScale);
        shader.SetParameter("RefractionPixels", _refractionPixels * uiScale);
        shader.SetParameter("Highlight", _highlight);
        shader.SetParameter("Opacity", _opacity);
        shader.SetParameter("Quality", (float) quality);
        shader.SetParameter("Animate", _animate ? 1f : 0f);
        shader.SetParameter("BackdropAvailable", hasBackdrop ? 1f : 0f);
        shader.SetParameter("Tint", new Vector4(_tintColor.R, _tintColor.G, _tintColor.B, _tintStrength));
        shader.SetParameter("EdgeTint", new Vector4(1f, 1f, 1f, 1f));

        handle.UseShader(shader);

        var vertexColor = Color.FromSrgb(Color.White * handle.Modulate);
        _vertices[0] = new DrawVertexUV2DColor(topLeft, new Vector2(0f, 0f), vertexColor) { UV2 = ToViewportUv(topLeft, new Vector2(0f, 0f)) };
        _vertices[1] = new DrawVertexUV2DColor(topRight, new Vector2(1f, 0f), vertexColor) { UV2 = ToViewportUv(topRight, new Vector2(1f, 0f)) };
        _vertices[2] = new DrawVertexUV2DColor(bottomRight, new Vector2(1f, 1f), vertexColor) { UV2 = ToViewportUv(bottomRight, new Vector2(1f, 1f)) };
        _vertices[3] = new DrawVertexUV2DColor(bottomLeft, new Vector2(0f, 1f), vertexColor) { UV2 = ToViewportUv(bottomLeft, new Vector2(0f, 1f)) };

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, texture, QuadIndices, _vertices);

        handle.UseShader(null);
    }

    private float CornerRadiusFor(UIBox2 box, float uiScale)
    {
        var themeRadius = _cornerRadius * uiScale;
        var minSide = MathF.Min(box.Width, box.Height);

        if (_forceShader)
            return MathF.Min(themeRadius, ButtonCornerRadius(box));

        if (minSide < StripMaxDimension)
            return MathF.Min(themeRadius, minSide * StripCornerFactor);

        return themeRadius;
    }

    private static float ButtonCornerRadius(UIBox2 box)
    {
        var minSide = MathF.Min(box.Width, box.Height);
        if (minSide <= 0f)
            return 0f;

        var factor = box.Width >= box.Height * 1.6f ? 0.35f : 0.28f;
        return minSide * factor;
    }

    private bool TryGetBackdrop(out ScalingViewport viewport, out Texture texture)
    {
        viewport = default!;
        texture = default!;

        _eyeManager ??= IoCManager.Resolve<IEyeManager>();

        if (_eyeManager.MainViewport is not ScalingViewport scalingViewport)
            return false;

        if (scalingViewport.PixelSize.X <= 0 || scalingViewport.PixelSize.Y <= 0)
            return false;

        var clydeViewport = scalingViewport.ClydeViewport;
        if (clydeViewport == null)
            return false;

        viewport = scalingViewport;
        texture = clydeViewport.RenderTarget.Texture;
        return true;
    }

    private ShaderInstance? GetShader()
    {
        if (_shaderResolved)
            return _shader;

        _shaderResolved = true;

        if (IoCManager.Resolve<IPrototypeManager>().TryIndex(LiquidGlassShader, out ShaderPrototype? prototype))
            _shader = prototype.InstanceUnique();

        return _shader;
    }
}
