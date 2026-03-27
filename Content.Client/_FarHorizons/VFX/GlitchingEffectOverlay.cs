using Content.Shared._FarHorizons.VFX;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._FarHorizons.VFX;

public sealed class GlitchingEffectOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> _shaderProto = "GlitchingEffect";

    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly ShaderInstance _shader;

    public int GlitchSteps = 0;
    public float VignettePower = 0;

    public GlitchingEffectOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _protoMan.Index(_shaderProto).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var playerEntity = _playerMan.LocalSession?.AttachedEntity;

        if (playerEntity == null ||
            !_entMan.TryGetComponent(playerEntity, out EyeComponent? eyeComp) ||
            args.Viewport.Eye != eyeComp.Eye ||
            !_entMan.TryGetComponent<GlitchingEffectComponent>(playerEntity, out _))
            return false;

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var playerEntity = _playerMan.LocalSession?.AttachedEntity;

        if (playerEntity == null ||
            ScreenTexture == null)
            return;
        
        if (_entMan.TryGetComponent<EyeComponent>(playerEntity, out var content))
            _shader.SetParameter("Zoom", content.Zoom.X);

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        _shader.SetParameter("maxSteps", GlitchSteps);
        _shader.SetParameter("vignettePower", VignettePower);

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;

        worldHandle.UseShader(_shader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null);
    }
}