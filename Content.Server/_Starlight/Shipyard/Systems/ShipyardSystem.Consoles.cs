using Content.Server.Popups;
using Content.Server.Cargo.Systems;
using Content.Shared._Starlight.Shipyard.Components;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Shipyard.Events;
using Content.Shared._Starlight.Shipyard.BUI;
using Content.Shared._Starlight.Shipyard.Prototypes;
using Content.Shared.Access.Systems;
using Content.Shared.Access.Components;
using Content.Shared._Starlight.Shipyard;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared.Cargo.Components;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Utility;
using Robust.Shared.Audio.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Starlight.Speech;

namespace Content.Server._Starlight.Shipyard.Systems;

public sealed class ShipyardConsoleSystem : SharedShipyardSystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ShipyardSystem _shipyard = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShipyardConsoleComponent, ShipyardConsolePurchaseMessage>(OnPurchaseMessage);
        SubscribeLocalEvent<ShipyardConsoleComponent, BoundUIOpenedEvent>(OnConsoleUIOpened);
    }

    private void OnPurchaseMessage(EntityUid uid, ShipyardConsoleComponent component, ShipyardConsolePurchaseMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        if (TryComp<AccessReaderComponent>(uid, out var accessReaderComponent) &&
            accessReaderComponent.Enabled &&
            !_access.IsAllowed(player, uid, accessReaderComponent))
        {
            ConsolePopup(player, Loc.GetString("comms-console-permission-denied"));
            PlayDenySound(uid, component);
            return;
        }

        if (!_prototypeManager.TryIndex<VesselPrototype>(args.Vessel, out var vessel))
        {
            ConsolePopup(player, Loc.GetString("shipyard-console-invalid-vessel", ("vessel", args.Vessel)));
            PlayDenySound(uid, component);
            return;
        }

        if (vessel.Price <= 0)
        {
            ConsolePopup(player, Loc.GetString("shipyard-console-invalid-price"));
            PlayDenySound(uid, component);
            return;
        }

        if (_station.GetOwningStation(uid) is not { } station)
        {
            ConsolePopup(player, Loc.GetString("shipyard-console-no-station"));
            PlayDenySound(uid, component);
            return;
        }

        var bank = GetBankAccount(station);
        if (bank == null)
        {
            ConsolePopup(player, Loc.GetString("shipyard-console-no-bank"));
            PlayDenySound(uid, component);
            return;
        }

        var balance = bank.Accounts.GetValueOrDefault(bank.PrimaryAccount, 0);

        if (balance < vessel.Price)
        {
            ConsolePopup(player, Loc.GetString("cargo-console-insufficient-funds", ("cost", vessel.Price)));
            PlayDenySound(uid, component);
            return;
        }

        if (!TryPurchaseVessel(uid, vessel, out var shuttle))
        {
            PlayDenySound(uid, component);
            return;
        }

        _cargo.UpdateBankAccount((station, bank), -vessel.Price, bank.PrimaryAccount);
        var channel = component.AnnouncementChannel;

        var message = new SpeechMessage
        {
            Text = Loc.GetString("shipyard-console-docking",
                ("vessel", vessel.Name.ToString()),
                ("delay", vessel.Delay))
        };

        _radio.SendRadioMessage(uid, message, channel, uid);
        PlayConfirmSound(uid, component);

        var newState = new ShipyardConsoleInterfaceState(
            balance - vessel.Price,
            true);

        _ui.SetUiState(uid, ShipyardConsoleUiKey.Shipyard, newState);
    }

    private void OnConsoleUIOpened(EntityUid uid, ShipyardConsoleComponent component, BoundUIOpenedEvent args)
    {
        var station = _station.GetOwningStation(uid);
        var bank = GetBankAccount(station);
        var balance = bank?.Accounts.GetValueOrDefault(bank.PrimaryAccount, 0) ?? 0;

        var accessGranted = true;

        if (TryComp<AccessReaderComponent>(uid, out var accessReader) &&
            accessReader.Enabled &&
            args.Actor is { Valid: true } actor)
        {
            accessGranted = _access.IsAllowed(actor, uid, accessReader);
        }

        var newState = new ShipyardConsoleInterfaceState(
            balance,
            accessGranted);

        _ui.SetUiState(uid, ShipyardConsoleUiKey.Shipyard, newState);
    }

    private void ConsolePopup(EntityUid player, string text) =>
        _popup.PopupEntity(text, player);

    private void PlayDenySound(EntityUid uid, ShipyardConsoleComponent component) =>
        _audio.PlayPvs(_audio.ResolveSound(component.ErrorSound), uid);

    private void PlayConfirmSound(EntityUid uid, ShipyardConsoleComponent component) =>
        _audio.PlayPvs(_audio.ResolveSound(component.ConfirmSound), uid);

    private bool TryPurchaseVessel(EntityUid uid, VesselPrototype vessel, out ShuttleComponent? deed)
    {
        var stationUid = _station.GetOwningStation(uid);

        if (vessel.ShuttlePath == ResPath.Empty)
        {
            deed = null;
            return false;
        }

        _shipyard.PurchaseShuttle(
            stationUid,
            vessel.ShuttlePath.ToString(),
            vessel.Delay,
            out deed
        );

        return deed != null;
    }

    public StationBankAccountComponent? GetBankAccount(EntityUid? uid)
    {
        if (uid != null && TryComp<StationBankAccountComponent>(uid, out var bankAccount))
        {
            return bankAccount;
        }
        return null;
    }
}
