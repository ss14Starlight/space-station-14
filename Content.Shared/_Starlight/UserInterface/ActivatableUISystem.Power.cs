using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;

// ReSharper disable once CheckNamespace
namespace Content.Shared.UserInterface;

public sealed partial class ActivatableUISystem
{
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;

    [SubscribeLocalEvent]
    private void OnPowerChanged(Entity<ActivatableUIRequiresPowerCellComponent> ent, ref PowerChangedEvent args)
    {
        if (!TryComp<ActivatableUIRequiresPowerComponent>(ent, out var powerRequirement) ||
            !powerRequirement.AllowPowerCellFallback)
            return;

        if (args.Powered)
        {
            _toggle.TryDeactivate(ent.Owner);
            return;
        }

        if (_cell.HasActivatableCharge(ent.Owner) && _cell.HasDrawCharge(ent.Owner))
            return;

        if (TryComp<ActivatableUIComponent>(ent, out var activatable) && activatable.Key is { } key)
            _uiSystem.CloseUi(ent.Owner, key);
    }

    private partial bool IsApcPoweredFallback(EntityUid uid)
    {
        return TryComp<ActivatableUIRequiresPowerComponent>(uid, out var powerRequirement) &&
               powerRequirement.AllowPowerCellFallback &&
               _powerReceiver.IsPowered(uid);
    }
}
