using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Content.Client.Overlays;
using Content.Client._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Eye;

namespace Content.Client._Starlight.Eye;

public sealed partial class DarkenedVisionSystem : SharedDarkenedVisionSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;

    private RemoteControlInterface _remoteControl = default!;
    private DarkenedVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _remoteControl = EntityManager.System<RemoteControlInterface>();
        _remoteControl.ControlledEntityChanged += OnRemoteControlledEntityChanged;
        SubscribeLocalEvent<DarkenedVisionComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DarkenedVisionComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<DarkenedVisionComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<DarkenedVisionComponent, LocalPlayerDetachedEvent>(OnDetached);

        _overlay = new();
        UpdateOverlayRegistration();
    }

    public override void Shutdown()
    {
        if (_remoteControl is { } remoteControl)
            remoteControl.ControlledEntityChanged -= OnRemoteControlledEntityChanged;
        if (_overlay is { } overlay)
            _overlayMan.RemoveOverlay(overlay);
        base.Shutdown();
    }

    private void OnAttached(Entity<DarkenedVisionComponent> ent, ref LocalPlayerAttachedEvent args)
        => UpdateOverlayRegistration();

    private void OnDetached(Entity<DarkenedVisionComponent> ent, ref LocalPlayerDetachedEvent args)
        => UpdateOverlayRegistration(ent.Owner);

    private void OnStartup(Entity<DarkenedVisionComponent> ent, ref ComponentStartup args)
        => UpdateOverlayRegistration();

    private void OnShutdown(Entity<DarkenedVisionComponent> ent, ref ComponentShutdown args)
        => UpdateOverlayRegistration(ent.Owner);

    private void OnRemoteControlledEntityChanged(EntityUid? entity)
        => UpdateOverlayRegistration();

    private void UpdateOverlayRegistration(EntityUid? excludedEntity = null)
    {
        var localHasVision = _player.LocalEntity is { } local
                             && local != excludedEntity
                             && HasComp<DarkenedVisionComponent>(local);
        var remoteHasVision = _remoteControl.ControlledEntity is { } remote
                              && remote != excludedEntity
                              && HasComp<DarkenedVisionComponent>(remote);

        if (localHasVision || remoteHasVision)
            _overlayMan.AddOverlay(_overlay);
        else
            _overlayMan.RemoveOverlay(_overlay);
    }
}
