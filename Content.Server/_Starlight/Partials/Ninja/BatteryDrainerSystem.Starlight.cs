using Content.Server.Power.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;

namespace Content.Server.Ninja.Systems;

public sealed partial class BatteryDrainerSystem
{
    [Dependency] private InventorySystem _gloveInventory = default!;
    [Dependency] private PowerCellSystem _glovePowerCell = default!;

    /// <summary>Lets worn gloves with BatteryDrainer and a slotted cell drain an APC to recharge.</summary>
    [SubscribeLocalEvent]
    private void OnGlovesBeforeInteractHand(Entity<InventoryComponent> ent, ref BeforeInteractHandEvent args)
    {
        if (args.Handled || !HasComp<PowerNetworkBatteryComponent>(args.Target) ||
            !_gloveInventory.TryGetSlotEntity(ent.Owner, "gloves", out var item) || item is not { } gloves ||
            !TryComp<ClothingComponent>(gloves, out var clothing) ||
            (clothing.Slots & SlotFlags.GLOVES) == 0 ||
            !HasComp<PowerCellSlotComponent>(gloves) ||
            !TryComp<BatteryDrainerComponent>(gloves, out var drainer))
            return;

        args.Handled = true;
        if (!_glovePowerCell.TryGetBatteryFromSlot(gloves, out var cell))
        {
            _popup.PopupEntity(Loc.GetString("power-cell-no-battery"), ent.Owner, ent.Owner);
            return;
        }

        if (_battery.IsFull(cell.Value.Owner))
        {
            _popup.PopupEntity(Loc.GetString("battery-drainer-full"), ent.Owner, ent.Owner, PopupType.Medium);
            return;
        }

        SetBattery(gloves, cell.Value.Owner);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent.Owner, drainer.DrainTime,
            new DrainDoAfterEvent(), target: args.Target, eventTarget: gloves)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = false,
            AttemptFrequency = AttemptFrequency.StartAndEnd,
        });
    }

    [SubscribeLocalEvent]
    private void OnGlovesDrainAttempt(Entity<ClothingComponent> ent, ref DoAfterAttemptEvent<DrainDoAfterEvent> args)
    {
        if ((ent.Comp.Slots & SlotFlags.GLOVES) == 0 ||
            !HasComp<PowerCellSlotComponent>(ent.Owner) ||
            !TryComp<BatteryDrainerComponent>(ent.Owner, out var drainer))
            return;

        var wearer = args.DoAfter.Args.User;
        if (!_gloveInventory.TryGetSlotEntity(wearer, "gloves", out var worn) || worn != ent.Owner ||
            !_glovePowerCell.TryGetBatteryFromSlot(ent.Owner, out var cell) ||
            drainer.BatteryUid != cell.Value.Owner ||
            args.DoAfter.Args.Target is not { } target || !HasComp<PowerNetworkBatteryComponent>(target))
            args.Cancel();
    }
}
