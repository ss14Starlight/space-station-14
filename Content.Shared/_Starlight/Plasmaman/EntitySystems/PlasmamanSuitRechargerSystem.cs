using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared._Starlight.Plasmaman.Components;

namespace Content.Shared._Starlight.Plasmaman.EntitySystems;

/// <summary>
/// Handles the plasmaman suit recharge crystal restoring self-extinguisher charges on interaction.
/// </summary>
public sealed class PlasmamanSuitRechargerSystem : EntitySystem
{
    [Dependency] private readonly SharedChargesSystem _charges = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlasmamanSuitRechargerComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<PlasmamanSuitRechargerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (args.Target is not { Valid: true } target ||
            !HasComp<SelfExtinguisherComponent>(target) ||
            !TryComp<LimitedChargesComponent>(target, out var charges))
            return;

        args.Handled = true;
        var user = args.User;

        if (charges.LastCharges >= charges.MaxCharges)
        {
            _popup.PopupClient(Loc.GetString("plasmaman-suit-recharger-full"), target, user);
            return;
        }

        _charges.AddCharges(target, charges.MaxCharges - charges.LastCharges);
        _popup.PopupClient(Loc.GetString("plasmaman-suit-recharger-recharged"), target, user);
        PredictedQueueDel(ent.Owner);
    }
}
