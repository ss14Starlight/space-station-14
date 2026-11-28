using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Eye;
using Content.Client._Starlight.Computers.RemoteControl;

// ReSharper disable once CheckNamespace
namespace Content.Client.Overlays;

public sealed partial class DarkenedVisionOverlay : Overlay
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    private readonly RemoteControlInterface _remoteControl;
    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly ShaderInstance _circleMaskShader;
    private static readonly ProtoId<ShaderPrototype> _circleMaskShaderId = "CircleMask";

    public DarkenedVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _remoteControl = _entityManager.System<RemoteControlInterface>();
        ZIndex = -60;
        _circleMaskShader = _prototypeManager.Index(_circleMaskShaderId).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_remoteControl.TryGetViewportEntity(args, out var viewer)
            || !_entityManager.TryGetComponent(viewer, out DarkenedVisionComponent? darkened))
            return false;

        return darkened.Strength < darkened.BlindTreshold && darkened.Strength > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || !_remoteControl.TryGetViewportEntity(args, out var viewer)
            || !_entityManager.TryGetComponent(viewer, out DarkenedVisionComponent? darkened))
            return;

        if (_entityManager.TryGetComponent<EyeComponent>(viewer, out var eye))
        {
            _circleMaskShader.SetParameter("Zoom", eye.Zoom.X);
        }

        _circleMaskShader.SetParameter("CirclePow", 1f);
        _circleMaskShader.SetParameter("CircleRadius", (10 - darkened.Strength) * 32f);

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;
        worldHandle.UseShader(_circleMaskShader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null);
    }
}
