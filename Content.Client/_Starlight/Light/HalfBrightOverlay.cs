using System.Numerics;
using Content.Client.Graphics;
using Content.Shared.Ghost;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Light;

/// <summary>
/// Brightens the light map for ghosts in Half Bright mode. Black is raised the most and full light not at all, so light
/// fixtures still stand out.
/// </summary>
public sealed partial class HalfBrightOverlay : Robust.Client.Graphics.Overlay
{
    private static readonly ProtoId<ShaderPrototype> _copyShader = "LightTonemapCopy";
    private static readonly ProtoId<ShaderPrototype> _halfBrightShader = "HalfBright";

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private ILightManager _lightManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    // After the tonemap, so it works on the final light map.
    public const int ContentZIndex = LightTonemapOverlay.ContentZIndex + 1;

    private readonly OverlayResourceCache<CachedResources> _resources = new();
    private readonly ShaderInstance _copyShaderInst;
    private readonly ShaderInstance _halfBrightShaderInst;

    public HalfBrightOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = ContentZIndex;

        _copyShaderInst = _proto.Index(_copyShader).InstanceUnique();
        _halfBrightShaderInst = _proto.Index(_halfBrightShader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye;
        if (eye == null || !eye.DrawLight || !_lightManager.Enabled || !_lightManager.DrawLighting)
            return false;

        // Never brighten a living player's view, even if a detach was missed.
        if (_player.LocalEntity is not { } local || !_entMan.HasComponent<GhostComponent>(local))
            return false;

        return _entMan.TryGetComponent<MapComponent>(args.MapUid, out var map) && map.LightingEnabled;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        var target = viewport.LightRenderTarget;
        var res = _resources.GetForViewport(viewport, static _ => new CachedResources());

        if (res.Copy is not { } copy || copy.Size != target.Size)
        {
            res.Copy?.Dispose();
            copy = _clyde.CreateRenderTarget(target.Size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8),
                name: "half-bright-copy");
            res.Copy = copy;
        }

        // Same setup as LightTonemapOverlay.
        var lightScale = target.Size / (Vector2) viewport.Size;
        var matrix = target.GetWorldToLocalMatrix(viewport.Eye!, viewport.RenderScale * lightScale);
        var bounds = args.WorldBounds.Enlarged(1f);
        var targetSize = (Vector2) target.Size;
        var handle = args.WorldHandle;

        _copyShaderInst.SetParameter("targetSize", targetSize);
        _halfBrightShaderInst.SetParameter("targetSize", targetSize);

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
                handle.UseShader(_halfBrightShaderInst);
                handle.SetTransform(matrix);
                handle.DrawTextureRect(copy.Texture, bounds);
            }, null);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        _copyShaderInst.Dispose();
        _halfBrightShaderInst.Dispose();

        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? Copy;

        public void Dispose()
            => Copy?.Dispose();
    }
}
