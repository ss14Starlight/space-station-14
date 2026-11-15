using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._Starlight.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Light;

public sealed partial class ScreenPostProcessOverlay : Robust.Client.Graphics.Overlay
{
    private static readonly ProtoId<ShaderPrototype> _shader = "ScreenPostProcess";
    private static readonly ProtoId<ShaderPrototype> _bloomShader = "BloomExtract";

    private const float BloomScale = 0.5f;

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private ILightManager _lightManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public const int ContentZIndex = 100;

    private readonly OverlayResourceCache<CachedResources> _resources = new();
    private readonly ShaderInstance _shaderInst;
    private readonly ShaderInstance _bloomShaderInst;

    public ScreenPostProcessOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = ContentZIndex;

        _shaderInst = _proto.Index(_shader).InstanceUnique();
        _bloomShaderInst = _proto.Index(_bloomShader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity == null || args.Viewport.Eye == null || args.Viewport.Eye != _eye.CurrentEye)
            return false;

        return _cfg.GetCVar(StarlightCCVars.NightVision)
            || _cfg.GetCVar(StarlightCCVars.Bloom)
            || _cfg.GetCVar(StarlightCCVars.Vignette)
            || _cfg.GetCVar(StarlightCCVars.FilmGrain);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var eye = args.Viewport.Eye!;
        var lit = eye.DrawLight
                && _lightManager.Enabled
                && _lightManager.DrawLighting
                && _entMan.TryGetComponent<MapComponent>(args.MapUid, out var map)
                && map.LightingEnabled;

        // Without the light map there is no telling what glows.
        var bloom = lit && _cfg.GetCVar(StarlightCCVars.Bloom)
            ? Math.Clamp(_cfg.GetCVar(StarlightCCVars.BloomStrength), 0f, 3f)
            : 0f;
        var bloomTexture = bloom > 0f ? DrawBloom(args) : null;

        _shaderInst.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shaderInst.SetParameter("LIGHT_TEXTURE", args.Viewport.LightRenderTarget.Texture);
        _shaderInst.SetParameter("BLOOM_TEXTURE", bloomTexture ?? ScreenTexture);
        _shaderInst.SetParameter("lightAvailable", lit ? 1f : 0f);
        _shaderInst.SetParameter("nightVision", Strength(StarlightCCVars.NightVision, StarlightCCVars.NightVisionStrength));
        _shaderInst.SetParameter("bloom", bloomTexture != null ? bloom : 0f);
        _shaderInst.SetParameter("vignette", Strength(StarlightCCVars.Vignette, StarlightCCVars.VignetteStrength));
        _shaderInst.SetParameter("grain", Strength(StarlightCCVars.FilmGrain, StarlightCCVars.FilmGrainStrength));

        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(_shaderInst);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }

    private Texture? DrawBloom(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        var size = new Vector2i(
            Math.Max(1, (int) MathF.Ceiling(viewport.Size.X * BloomScale)),
            Math.Max(1, (int) MathF.Ceiling(viewport.Size.Y * BloomScale)));

        var res = _resources.GetForViewport(viewport, static _ => new CachedResources());
        if (res.Target?.Size != size)
        {
            res.Dispose();
            var format = new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb);
            res.Target = _clyde.CreateRenderTarget(size, format, name: "bloom");
            res.BlurBuffer = _clyde.CreateRenderTarget(size, format, name: "bloom-blur");
        }

        var target = res.Target;
        var handle = args.WorldHandle;
        // The shader samples by pixel position, the quad only has to cover the whole target.
        var matrix = target.GetWorldToLocalMatrix(viewport.Eye!, viewport.RenderScale * (size / (Vector2) viewport.Size));
        var bounds = args.WorldBounds.Enlarged(1f);

        _bloomShaderInst.SetParameter("SCREEN_TEXTURE", ScreenTexture!);
        _bloomShaderInst.SetParameter("LIGHT_TEXTURE", viewport.LightRenderTarget.Texture);
        _bloomShaderInst.SetParameter("targetSize", (Vector2) size);
        _bloomShaderInst.SetParameter("threshold", Math.Clamp(_cfg.GetCVar(StarlightCCVars.BloomThreshold), 0f, 1f));

        handle.RenderInRenderTarget(target,
            (Action)(() =>
            {
                handle.UseShader(_bloomShaderInst);
                handle.SetTransform(matrix);
                handle.DrawRect(bounds, Color.White);
            }), Color.Black);

        _clyde.BlurRenderTarget(viewport, target, res.BlurBuffer!, viewport.Eye!, MathF.Max(0f, _cfg.GetCVar(StarlightCCVars.BloomRadius)));
        return target.Texture;
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();

        base.DisposeBehavior();
    }

    private float Strength(CVarDef<bool> enabled, CVarDef<float> strength)
        => _cfg.GetCVar(enabled) ? Math.Clamp(_cfg.GetCVar(strength), 0f, 1f) : 0f;

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? Target;
        public IRenderTexture? BlurBuffer;

        public void Dispose()
        {
            Target?.Dispose();
            BlurBuffer?.Dispose();
            Target = null;
            BlurBuffer = null;
        }
    }
}
