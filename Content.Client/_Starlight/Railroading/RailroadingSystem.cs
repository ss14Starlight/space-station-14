using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Railroading.Components;
using Content.Shared._Starlight.Railroading.Events;
using Content.Client.UserInterface.Systems.Character;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Railroading;

public sealed partial class RailroadingSystem : SharedRailroadingSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IGameTiming _timing = default!;

    public event Action? CardsPendingChanged;

    public bool CardsPending { get; private set; }

    /// <summary>
    /// Whether the local player let a hand expire and is barred from further offers.
    /// </summary>
    public bool CardsRestricted => HasComp<RailroadRestrictedComponent>(_player.LocalEntity);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadCardsPendingComponent, ComponentStartup>(OnPendingStartup);
        SubscribeLocalEvent<RailroadCardsPendingComponent, ComponentShutdown>(OnPendingShutdown);
        SubscribeLocalEvent<RailroadCardsPendingComponent, OpenCardsAlertEvent>(OnCardsAlert);

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

    private void OnCardsAlert(Entity<RailroadCardsPendingComponent> ent, ref OpenCardsAlertEvent args)
    {
        args.Handled = true;
        if (_timing.IsFirstTimePredicted)
            _ui.GetUIController<CharacterUIController>().OpenCharacterOverview();
    }

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
