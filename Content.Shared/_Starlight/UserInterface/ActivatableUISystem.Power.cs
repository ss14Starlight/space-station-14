using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;

// ReSharper disable once CheckNamespace
namespace Content.Shared.UserInterface;

public sealed partial class ActivatableUISystem
{
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;

    [SubscribeLocalEvent]
    private void OnPowerChanged(Entity<ActivatableUIRequiresPowerCellComponent> ent, ref PowerChangedEvent args)
        => OnPowerSourceChanged(ent.Owner, args.Powered);

    [SubscribeLocalEvent]
    private void OnPowerCellChanged(Entity<ActivatableUIRequiresPowerCellComponent> ent, ref PowerCellChangedEvent args)
        => OnPowerSourceChanged(ent.Owner, _powerReceiver.IsPowered(ent.Owner));

    [SubscribeLocalEvent]
    private void OnPowerCellSlotEmpty(Entity<ActivatableUIRequiresPowerCellComponent> ent, ref PowerCellSlotEmptyEvent args)
        => OnPowerSourceChanged(ent.Owner, _powerReceiver.IsPowered(ent.Owner));

    private void OnPowerSourceChanged(EntityUid uid, bool powered)
    {
        if (!TryComp<ActivatableUIRequiresPowerComponent>(uid, out var powerRequirement) ||
            !powerRequirement.AllowPowerCellFallback)
            return;

        if (powered)
        {
            _toggle.TryDeactivate(uid);
            return;
        }

        if (_cell.HasActivatableCharge(uid) && _cell.HasDrawCharge(uid))
        {
            if (TryComp<ActivatableUIComponent>(uid, out var ui) && ui.Key is { } openKey &&
                _uiSystem.IsUiOpen(uid, openKey))
                _toggle.TryActivate(uid);
            return;
        }

        if (TryComp<ActivatableUIComponent>(uid, out var activatable) && activatable.Key is { } key &&
            _uiSystem.IsUiOpen(uid, key))
            _uiSystem.CloseUi(uid, key);
    }

    private partial bool IsApcPoweredFallback(EntityUid uid) =>
        TryComp<ActivatableUIRequiresPowerComponent>(uid, out var powerRequirement) &&
        powerRequirement.AllowPowerCellFallback &&
        _powerReceiver.IsPowered(uid);
}
