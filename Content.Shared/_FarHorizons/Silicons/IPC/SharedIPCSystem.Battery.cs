// IPC System - Battery (Shared)
// SOURCE: Far-Horizons-SS14
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135
// _STARLIGHT: Namespace changes for compatibility

using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.PowerCell.Components;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Shared.Utility;

namespace Content.Shared._FarHorizons.Silicons.IPC;

public abstract partial class SharedIPCSystem
{
    [Dependency] private readonly ILogManager _logs = default!;
    [Dependency] private readonly ItemSlotsSystem _items = default!;
    
    protected ISawmill _sawmill = default!;

    protected virtual void SetupBattery()
    {
        _sawmill = _logs.GetSawmill("IPC");

        // Note: ComponentStartup subscription moved to server-side for battery drainer initialization
        SubscribeLocalEvent<IPCBatteryComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt);
        SubscribeLocalEvent<IPCBatteryComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEjectAttempt);
        SubscribeLocalEvent<IPCBatteryComponent, GetVerbsEvent<AlternativeVerb>>(AddBatteryAltVerbs);
    }

    protected abstract void UpdateBattery(float frameTime);

    private void AddBatteryAltVerbs(Entity<IPCBatteryComponent> ent, ref GetVerbsEvent<AlternativeVerb> ev)
    {
        if (!ev.CanComplexInteract || 
            !TryComp<IPCBatteryComponent>(ev.User, out var battery) ||
            !TryComp(ev.Target, out MetaDataComponent? metadata) ||
            metadata.EntityPrototype == null ||
            !battery.DrainAllowedTargets.Contains(metadata.EntityPrototype.ID))
            return;

        var user = ev.User;
        var target = ev.Target;
        
        AlternativeVerb verb = new()
        {
            Act = () => StartDrain((user, battery), target),
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

    // Note: BatteryHasCharge moved to server-side as it's only used there
}


