using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._Starlight.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Light;

/// <summary>
/// Stops overlapping lights from washing out sprite colours.
/// </summary>
/// <remarks>
/// Smoothly tonemaps the light map, preserving values below
/// <see cref="StarlightCCVars.LightTonemapKnee"/> and gradually bringing higher values
/// toward <see cref="StarlightCCVars.LightTonemapMax"/>.
///
/// Client <see cref="StarlightCCVars.LightBoost"/> multiplies the exposure and never turns the roll-off off.
/// With the roll-off disabled, the overlay still runs while the boost is above 1 and applies only the boost.
/// Server exposure is applied only when roll-off is enabled.
///
/// Runs in <see cref="OverlaySpace.WorldSpaceBelowWorld"/>, after the light map is generated
/// and before the world is rendered. A float texture copy prevents reading from and
/// writing to the same texture.
/// </remarks>
public sealed partial class LightTonemapOverlay : Robust.Client.Graphics.Overlay
{
    private static readonly ProtoId<ShaderPrototype> _copyShader = "LightTonemapCopy";
    private static readonly ProtoId<ShaderPrototype> _tonemapShader = "LightTonemap";

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private ILightManager _lightManager = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public const int ContentZIndex = -150;

    private const float MinRollOff = 0.01f;

    public const float MinLightBoost = 1f;
    public const float MaxLightBoost = 1.3f;

    /// <summary>
    /// <see cref="StarlightCCVars.LightTonemapKnee"/> used when roll-off is disabled and only the light boost is applied. Nothing becomes this bright.
    /// </summary>
    private const float NoRollOff = 1000f;

    private readonly OverlayResourceCache<CachedResources> _resources = new();
    private readonly ShaderInstance _copyShaderInst;
    private readonly ShaderInstance _tonemapShaderInst;

    /// <summary>
    /// Set when the light map copy can't be made, which means no float render targets and nothing above 1 to roll off.
    /// </summary>
    private bool _unsupported;

    private readonly CVarSubscriptions _cvars;
    private float _boost;
    private bool _rollOff;
    private float _exposure;
    private float _knee;
    private float _maxLight;

    public LightTonemapOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = ContentZIndex;

        _copyShaderInst = _proto.Index(_copyShader).InstanceUnique();
        _tonemapShaderInst = _proto.Index(_tonemapShader).InstanceUnique();

        _cvars = new CVarSubscriptions(_cfg);
        _cvars.Track(StarlightCCVars.LightBoost, v => _boost = Math.Clamp(v, MinLightBoost, MaxLightBoost));
        _cvars.Track(StarlightCCVars.LightTonemap, v => _rollOff = v);
        _cvars.Track(StarlightCCVars.LightTonemapExposure, v => _exposure = v);
        _cvars.Track(StarlightCCVars.LightTonemapKnee, v => _knee = v);
        _cvars.Track(StarlightCCVars.LightTonemapMax, v => _maxLight = v);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye;
        if (_unsupported || eye == null || !eye.DrawLight || !_lightManager.Enabled || !_lightManager.DrawLighting)
            return false;

        return _entMan.TryGetComponent<MapComponent>(args.MapUid, out var map) && map.LightingEnabled;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        var target = viewport.LightRenderTarget;
        var res = _resources.GetForViewport(viewport, static _ => new CachedResources());

        if (res.Copy?.Size != target.Size)
        {
            res.Copy?.Dispose();
            res.Copy = null;

            try
            {
                res.Copy = _clyde.CreateRenderTarget(target.Size,
                    new RenderTargetFormatParameters(RenderTargetColorFormat.R11FG11FB10F),
                    name: "light-tonemap-copy");
            }
            catch (Exception e)
            {
                _unsupported = true;
                _log.GetSawmill("light").Warning($"Light tonemapping disabled, can't create a float render target: {e.Message}");
                return;
            }
        }

        // Same maths as AfterLightTargetOverlay, the light map can be smaller than the viewport.
        var lightScale = target.Size / (Vector2) viewport.Size;
        var matrix = target.GetWorldToLocalMatrix(viewport.Eye!, viewport.RenderScale * lightScale);
        // Both passes sample by pixel position, the quad only has to cover the whole target.
        var bounds = args.WorldBounds.Enlarged(1f);
        var targetSize = (Vector2) target.Size;
        var handle = args.WorldHandle;
        var copy = res.Copy;

        _copyShaderInst.SetParameter("targetSize", targetSize);
        _tonemapShaderInst.SetParameter("targetSize", targetSize);
        float exposure, maxLight, knee;
        if (_rollOff)
        {
            exposure = _exposure * _boost;
            maxLight = MathF.Max(_maxLight, MinRollOff);
            knee = Math.Clamp(_knee, 0f, maxLight - MinRollOff);
        }
        else
        {
            exposure = _boost;
            knee = NoRollOff;
            maxLight = NoRollOff + 1f;
        }

        _tonemapShaderInst.SetParameter("exposure", exposure);
        _tonemapShaderInst.SetParameter("knee", knee);
        _tonemapShaderInst.SetParameter("maxLight", maxLight);

        handle.RenderInRenderTarget(copy,
            () =>
            {
                handle.UseShader(_copyShaderInst);
                handle.SetTransform(matrix);
                handle.DrawTextureRect(target.Texture, bounds);
            }, null);

        handle.RenderInRenderTarget(target,
            () =>
            {
                handle.UseShader(_tonemapShaderInst);
                handle.SetTransform(matrix);
                handle.DrawTextureRect(copy.Texture, bounds);
            }, null);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    protected override void DisposeBehavior()
    {
        _cvars.Dispose();
        _resources.Dispose();

        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? Copy;

        public void Dispose()
            => Copy?.Dispose();
    }
}
