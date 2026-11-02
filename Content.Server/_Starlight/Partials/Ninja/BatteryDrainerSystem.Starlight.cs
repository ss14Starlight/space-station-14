// ReSharper disable CheckNamespace
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

    /// <summary>Starts glove charging on power-source touch.</summary>
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
            new GloveDrainDoAfterEvent(), target: args.Target, eventTarget: gloves)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = false,
            AttemptFrequency = AttemptFrequency.StartAndEnd,
        });
    }

    /// <summary>Validates the gloves, cell, and source during charging.</summary>
    [SubscribeLocalEvent]
    private void OnGlovesDrainAttempt(Entity<ClothingComponent> ent, ref DoAfterAttemptEvent<GloveDrainDoAfterEvent> args)
    {
        if ((ent.Comp.Slots & SlotFlags.GLOVES) == 0 ||
            !HasComp<PowerCellSlotComponent>(ent.Owner) ||
            !TryComp<BatteryDrainerComponent>(ent.Owner, out var drainer))
            return;

        var wearer = args.DoAfter.Args.User;
        if (!_gloveInventory.TryGetSlotEntity(wearer, "gloves", out var worn) || worn != ent.Owner ||
            !_glovePowerCell.TryGetBatteryFromSlot(ent.Owner, out var cell) ||
            drainer.BatteryUid != cell.Value.Owner ||
            _battery.IsFull(cell.Value.Owner) ||
            args.DoAfter.Args.Target is not { } target || !HasComp<PowerNetworkBatteryComponent>(target))
            args.Cancel();
    }

    /// <summary>Drains power and reports the result to the wearer.</summary>
    [SubscribeLocalEvent]
    private void OnGlovesDrain(Entity<ClothingComponent> ent, ref GloveDrainDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target ||
            !TryComp<BatteryDrainerComponent>(ent.Owner, out var drainer) ||
            drainer.BatteryUid is not { } cell)
            return;

        args.Handled = true;
        var chargeBefore = _battery.GetCharge(cell);
        args.Repeat = TryDrainPower((ent.Owner, drainer), target);
        var wearer = args.User;
        if (_battery.GetCharge(cell) > chargeBefore)
            _popup.PopupEntity(Loc.GetString("battery-drainer-success", ("battery", target)), ent.Owner, wearer);
        else
            _popup.PopupEntity(Loc.GetString("battery-drainer-empty", ("battery", target)), ent.Owner, wearer,
                PopupType.Medium);
    }
}
