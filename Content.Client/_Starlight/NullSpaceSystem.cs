using Robust.Client.Graphics;
using Robust.Shared.Player;
using Content.Shared._Starlight.NullSpace;
using Robust.Shared.Prototypes;
using Content.Client._Starlight.Overlay;

namespace Content.Client._Starlight;

public sealed partial class NullSpaceSystem : SharedNullSpaceSystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly ISharedPlayerManager _playerMan = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private NullSpaceOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullSpaceComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<NullSpaceComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NullSpaceComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<NullSpaceComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _overlay = new(_prototypeManager.Index<ShaderPrototype>("NullSpaceShader"));
    }

    private void OnInit(EntityUid uid, NullSpaceComponent component, ComponentInit args)
    {
        if (uid != _playerMan.LocalEntity)
            return;

        _overlayMan.AddOverlay(_overlay);
    }

    private void OnShutdown(EntityUid uid, NullSpaceComponent component, ComponentShutdown args)
    {
        if (uid != _playerMan.LocalEntity)
            return;

        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttached(EntityUid uid, NullSpaceComponent component, LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, NullSpaceComponent component, LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }
}