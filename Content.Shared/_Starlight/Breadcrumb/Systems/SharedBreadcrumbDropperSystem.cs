using System.Linq;
using Content.Shared._Starlight.Breadcrumb.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Breadcrumb.Systems;

public abstract partial class SharedBreadcrumbDropperSystem : EntitySystem
{
    [Dependency] protected SharedChargesSystem ChargesSystem = null!;

    /// <summary>
    /// Using in hand = pressing Z. Toggle on/off.
    /// </summary>
    [SubscribeLocalEvent]
    public void OnUseInHand(Entity<BreadcrumbDropperComponent> dropper, ref UseInHandEvent args)
        => dropper.Comp.Enabled = !dropper.Comp.Enabled;

    [SubscribeLocalEvent]
    public void OnInteractUsingEvent(Entity<BreadcrumbDropperComponent> dropper, ref InteractUsingEvent args)
    {
        if (args.Handled) return;

        if (!TryComp<BreadcrumbChargesComponent>(args.Used, out var charges))
            return;

        ChargesSystem.AddCharges((dropper, null), charges.Charges);

        QueueDel(args.Used);
        args.Handled = true;
    }

}
