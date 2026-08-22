using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Railroading.Components;
using Content.Shared._Starlight.Railroading.Events;
using Robust.Client.Player;

namespace Content.Client._Starlight.Railroading;

public sealed partial class RailroadingSystem : SharedRailroadingSystem
{
    [Dependency] private IPlayerManager _player = default!;

    public event Action? CardsPendingChanged;

    public bool CardsPending { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadCardsPendingComponent, ComponentStartup>(OnPendingStartup);
        SubscribeLocalEvent<RailroadCardsPendingComponent, ComponentShutdown>(OnPendingShutdown);

        _player.LocalPlayerAttached += OnLocalPlayerChanged;
        _player.LocalPlayerDetached += OnLocalPlayerChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _player.LocalPlayerAttached -= OnLocalPlayerChanged;
        _player.LocalPlayerDetached -= OnLocalPlayerChanged;
    }

    public void RequestCardSelection() => RaiseNetworkEvent(new OpenCardsRequestEvent());

    private void OnPendingStartup(Entity<RailroadCardsPendingComponent> ent, ref ComponentStartup args)
        => SetPending(ent.Owner, true);

    private void OnPendingShutdown(Entity<RailroadCardsPendingComponent> ent, ref ComponentShutdown args)
        => SetPending(ent.Owner, false);

    private void OnLocalPlayerChanged(EntityUid uid)
        => SetPending(_player.LocalEntity, HasComp<RailroadCardsPendingComponent>(_player.LocalEntity));

    private void SetPending(EntityUid? uid, bool pending)
    {
        if (uid != _player.LocalEntity || CardsPending == pending)
            return;

        CardsPending = pending;
        CardsPendingChanged?.Invoke();
    }
}
