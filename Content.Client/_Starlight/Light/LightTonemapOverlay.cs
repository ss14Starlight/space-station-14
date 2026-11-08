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
/// Prevents overlapping lights from washing sprites out to white.
/// </summary>
/// <remarks>
/// <para>
/// Lights are added together in the light map, so overlapping lights can produce values above 1, washing out sprite colours.
/// </para>
/// <para>
/// This overlay applies a smooth tonemap to the light map: values below the <i>knee</i>
/// (<see cref="StarlightCCVars.LightTonemapKnee"/>) remain unchanged, while higher values gradually approach the
/// <i>maximum</i> (<see cref="StarlightCCVars.LightTonemapMax"/>).
/// </para>
/// <para>
/// It runs in <see cref="OverlaySpace.WorldSpaceBelowWorld"/>, after the light map is generated but before the world is drawn.
/// Since a shader cannot read and write the same texture, the light map is copied to a float texture before processing.
/// </para>
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

    private readonly OverlayResourceCache<CachedResources> _resources = new();
    private readonly ShaderInstance _copyShaderInst;
    private readonly ShaderInstance _tonemapShaderInst;

    /// <summary>
    /// Set when the light map copy can't be made, which means no float render targets and nothing above 1 to roll off.
    /// </summary>
    private bool _unsupported;

    public LightTonemapOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = ContentZIndex;

        _copyShaderInst = _proto.Index(_copyShader).InstanceUnique();
        _tonemapShaderInst = _proto.Index(_tonemapShader).InstanceUnique();
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
        _tonemapShaderInst.SetParameter("exposure", _cfg.GetCVar(StarlightCCVars.LightTonemapExposure));
        var maxLight = MathF.Max(_cfg.GetCVar(StarlightCCVars.LightTonemapMax), MinRollOff);
        var knee = Math.Clamp(_cfg.GetCVar(StarlightCCVars.LightTonemapKnee), 0f, maxLight - MinRollOff);
        _tonemapShaderInst.SetParameter("knee", knee);
        _tonemapShaderInst.SetParameter("maxLight", maxLight);

        handle.RenderInRenderTarget(copy,
            (() =>
            {
                handle.UseShader(_copyShaderInst);
                handle.SetTransform(matrix);
                handle.DrawTextureRect(target.Texture, bounds);
            }), null);

        handle.RenderInRenderTarget(target,
            (() =>
            {
                handle.UseShader(_tonemapShaderInst);
                handle.SetTransform(matrix);
                handle.DrawTextureRect(copy.Texture, bounds);
            }), null);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    protected override void DisposeBehavior()
    {
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
