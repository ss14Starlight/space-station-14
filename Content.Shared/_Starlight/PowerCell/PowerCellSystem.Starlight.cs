using Content.Shared.Containers.ItemSlots;
using Content.Shared.PowerCell.Components;
using Content.Shared.Wires;
using Robust.Shared.GameObjects;

// ReSharper disable once CheckNamespace
namespace Content.Shared.PowerCell;

public sealed partial class PowerCellSystem
{
    [SubscribeLocalEvent]
    private void OnCellSlotEjectAttempt(Entity<PowerCellSlotComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Cancelled || !ent.Comp.RequiresOpenPanelToEject)
            return;

        if (!_itemSlots.TryGetSlot(ent.Owner, ent.Comp.CellSlotId, out var slot) || slot != args.Slot)
            return;

        if (!TryComp<WiresPanelComponent>(ent.Owner, out var panel) || !panel.Open)
            args.Cancelled = true;
    }
}
