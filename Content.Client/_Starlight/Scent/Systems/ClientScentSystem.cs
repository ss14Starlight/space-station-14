using Content.Client._Starlight.Computers.RemoteControl;
using Content.Client._Starlight.Scent.Overlays;
using Content.Shared._Starlight.Scent.Components;
using Content.Shared._Starlight.Scent.Events;
using Content.Shared._Starlight.Scent.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Scent.Systems;

/// <summary>
/// Registers scent overlays when either the local player or remote viewport is controlled by a
/// Smeller, so SharedScentSystem's handlers actually run on the client too.
/// </summary>
public sealed partial class ClientScentSystem : SharedScentSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;

    private RemoteControlInterface _remoteControl = default!;
    private ScentTrackingSystem _scentTracking = default!;
    private ScentPerceptionOverlay _overlay = default!;
    private ScentSourcePingOverlay _pingOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _remoteControl = EntityManager.System<RemoteControlInterface>();
        _scentTracking = EntityManager.System<ScentTrackingSystem>();
        _remoteControl.ControlledEntityChanged += OnRemoteControlledEntityChanged;
        _overlay = new();
        _pingOverlay = new(EntityManager, _remoteControl, _eye, _timing);

        SubscribeLocalEvent<SmellerComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SmellerComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<ScentSourcePingEvent>(OnScentSourcePing);
        UpdateOverlayRegistration();
    }

    /// <summary>
    /// ComponentShutdown/LocalPlayerDetachedEvent only fire on ordinary gameplay transitions, not
    /// when this system itself gets torn down (e.g. disconnecting mid-round). IOverlayManager is a
    /// process-lifetime singleton that outlives this system, so without this, _overlay stays
    /// registered forever, holding a live reference to this connection's entire entity graph.
    /// </summary>
    public override void Shutdown()
    {
        if (_remoteControl is { } remoteControl)
            remoteControl.ControlledEntityChanged -= OnRemoteControlledEntityChanged;
        base.Shutdown();

        if (_overlay is { } overlay)
            _overlayMan.RemoveOverlay(overlay);
        if (_pingOverlay is { } pingOverlay)
            _overlayMan.RemoveOverlay(pingOverlay);
    }

    private void OnScentSourcePing(ScentSourcePingEvent ev)
    {
        if (!_overlayMan.HasOverlay<ScentSourcePingOverlay>())
            return;

        _pingOverlay.AddFlash(
            ev.Smeller,
            ScentTrackingSystem.GetScentColor(ev.ScentId),
            new MapCoordinates(ev.Position, ev.MapId));
    }

    protected override void OnSmellerInit(EntityUid uid, SmellerComponent component, ComponentInit args)
    {
        base.OnSmellerInit(uid, component, args);
        UpdateOverlayRegistration();
    }

    /// <summary>
    /// Overrides the base handler instead of subscribing to ComponentShutdown again: the base
    /// class already owns that subscription, and SubscribeLocalEvent doesn't allow a second one
    /// for the same (component, event) pair.
    /// </summary>
    /// <param name="ent">The entity and SmellerComponent being removed.</param>
    /// <param name="args">Component shutdown event args.</param>
    protected override void OnSmellerShutdown(Entity<SmellerComponent> ent, ref ComponentShutdown args)
    {
        base.OnSmellerShutdown(ent, ref args);
        _scentTracking.RefreshAfterSmellerShutdown(ent.Owner);
        UpdateOverlayRegistration(ent.Owner);
    }

    private void OnPlayerAttached(EntityUid uid, SmellerComponent component, LocalPlayerAttachedEvent args)
        => UpdateOverlayRegistration();

    private void OnPlayerDetached(EntityUid uid, SmellerComponent component, LocalPlayerDetachedEvent args)
        => UpdateOverlayRegistration(uid);

    private void OnRemoteControlledEntityChanged(EntityUid? entity)
        => UpdateOverlayRegistration();

    private void UpdateOverlayRegistration(EntityUid? excludedEntity = null)
    {
        var localSmeller = _player.LocalEntity is { } local
                           && local != excludedEntity
                           && HasComp<SmellerComponent>(local);
        var remoteSmeller = _remoteControl.ControlledEntity is { } remote
                            && remote != excludedEntity
                            && HasComp<SmellerComponent>(remote);

        if (localSmeller || remoteSmeller)
        {
            if (!_overlayMan.HasOverlay<ScentPerceptionOverlay>())
                _overlayMan.AddOverlay(_overlay);
            if (!_overlayMan.HasOverlay<ScentSourcePingOverlay>())
                _overlayMan.AddOverlay(_pingOverlay);
        }
        else
        {
            _overlayMan.RemoveOverlay(_overlay);
            _overlayMan.RemoveOverlay(_pingOverlay);
            _pingOverlay.ClearFlashes();
        }
    }
}
