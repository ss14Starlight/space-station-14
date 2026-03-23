using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Ninja.Components;
using Content.Shared.PowerCell.Components;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared._FarHorizons.Silicons.IPC;

public abstract partial class SharedIPCSystem
{
    protected virtual void SetupBattery()
    {
        SubscribeLocalEvent<IPCBatteryComponent, ComponentStartup>(OnBatteryStartup);
        SubscribeLocalEvent<IPCBatteryComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt);
        SubscribeLocalEvent<IPCBatteryComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEjectAttempt);
    }

    protected abstract void UpdateBattery(float frameTime);

    private void AddBatteryAltVerbs(GetVerbsEvent<AlternativeVerb> ev)
    {
        if (!ev.CanComplexInteract || 
            !TryComp<IPCBatteryComponent>(ev.User, out var battery) ||
            !TryComp(ev.Target, out MetaDataComponent? metadata) ||
            metadata.EntityPrototype == null ||
            !battery.DrainAllowedTargets.Contains(metadata.EntityPrototype.ID))
            return;

        AlternativeVerb verb = new()
        {
            Act = () => StartDrain((ev.User, battery), ev.Target),
            Text = Loc.GetString("ipc-drain-power-alt-verb"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/zap.svg.192dpi.png")),
        };
        ev.Verbs.Add(verb);
    }

    protected virtual void StartDrain(Entity<IPCBatteryComponent> user, EntityUid target){}

    private void OnItemSlotEjectAttempt(Entity<IPCBatteryComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Cancelled ||
            !TryComp<PowerCellSlotComponent>(ent, out var cellSlotComp) ||
            !TryComp<WiresPanelComponent>(ent, out var panel) ||
            !_items.TryGetSlot(ent, cellSlotComp.CellSlotId, out var cellSlot) ||
            cellSlot != args.Slot)
            return;

        if (!panel.Open)
            args.Cancelled = true;
    }

    private void OnItemSlotInsertAttempt(Entity<IPCBatteryComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled ||
            !TryComp<PowerCellSlotComponent>(ent, out var cellSlotComp) ||
            !TryComp<WiresPanelComponent>(ent, out var panel) ||
            !_items.TryGetSlot(ent, cellSlotComp.CellSlotId, out var cellSlot) ||
            cellSlot != args.Slot)
            return;

        if (!panel.Open)
            args.Cancelled = true;
    }

    private void OnBatteryStartup(Entity<IPCBatteryComponent> ent, ref ComponentStartup args) 
    {
        ent.Comp.PowerCellSlot = EnsureComp<PowerCellSlotComponent>(ent);
        ent.Comp.BatteryDrainer = EnsureComp<BatteryDrainerComponent>(ent);
        EnsureComp<PowerCellDrawComponent>(ent);
    }

    public bool BatteryHasCharge(EntityUid uid) => _powerCell.HasDrawCharge(uid);
}