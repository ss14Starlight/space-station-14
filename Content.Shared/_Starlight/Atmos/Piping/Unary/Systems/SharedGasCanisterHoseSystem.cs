using Content.Shared.Containers.ItemSlots;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Binary.Components;
using Content.Shared.Interaction;
using Content.Shared.Lock;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using GasCanisterComponent = Content.Shared.Atmos.Piping.Unary.Components.GasCanisterComponent;
using GasCanisterHoseComponent = Content.Shared.Atmos.Piping.Unary.Components.GasCanisterHoseComponent;
using GasCanisterHoseSlotComponent = Content.Shared.Atmos.Piping.Unary.Components.GasCanisterHoseSlotComponent;
using Robust.Shared.Containers;

namespace Content.Shared.Atmos.Piping.Unary.Systems;

public abstract partial class SharedGasCanisterHoseSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GasCanisterHoseSlotComponent, ComponentStartup>(OnHoseSlotStartup);
        SubscribeLocalEvent<GasCanisterHoseSlotComponent, EntInsertedIntoContainerMessage>(OnHoseInserted);
        SubscribeLocalEvent<GasCanisterHoseSlotComponent, EntRemovedFromContainerMessage>(OnHoseRemoved);
        SubscribeLocalEvent<GasCanisterHoseSlotComponent, ItemSlotEjectAttemptEvent>(OnHoseEjectAttempt);
        SubscribeLocalEvent<SimpleToolUsageComponent, SimpleToolDoAfterEvent>(OnHoseDetach);
        SubscribeLocalEvent<SimpleToolUsageComponent, AttemptSimpleToolUseEvent>(OnHoseDetachAttempt);
        SubscribeLocalEvent<GasCanisterComponent, InteractUsingEvent>(OnCanisterInteractUsing,
            before: new[] { typeof(ItemSlotsSystem) });
    }

    private void OnHoseSlotStartup(Entity<GasCanisterHoseSlotComponent> ent, ref ComponentStartup args)
    {
        _slots.AddItemSlot(ent.Owner, ent.Comp.ContainerName, ent.Comp.HoseSlot);
        UpdateHoseAppearance(ent.Owner, ent.Comp.HoseSlot.HasItem);
    }

    private void OnHoseInserted(EntityUid uid, GasCanisterHoseSlotComponent component, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != component.ContainerName)
            return;

        UpdateHoseAppearance(uid, true);
    }

    private void OnHoseRemoved(EntityUid uid, GasCanisterHoseSlotComponent component, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != component.ContainerName)
            return;

        UpdateHoseAppearance(uid, false);
    }

    private void UpdateHoseAppearance(EntityUid uid, bool attached)
    {
        _appearance.SetData(uid, GasCanisterVisuals.HoseAttached, attached);
    }

    private void OnCanisterInteractUsing(Entity<GasCanisterComponent> canister, ref InteractUsingEvent args)
    {
        if (args.Handled
            || !TryComp<GasCanisterHoseSlotComponent>(canister, out var hoseSlot))
        {
            return;
        }

        if (HasComp<GasTankComponent>(args.Used))
        {
            if (!hoseSlot.HoseSlot.HasItem)
                return;

            if (TryComp<GasTankComponent>(args.Used, out var tank) && tank.IsValveOpen)
                return;

            args.Handled = true;
            RefillTank(canister, args.Used, args.User);
            return;
        }

        if (HasComp<GasCanisterHoseComponent>(args.Used)
            && (canister.Comp.GasTankSlot.HasItem
                || (TryComp<LockComponent>(canister, out var lockState) && lockState.Locked)))
        {
            args.Handled = true;
            return;
        }
    }

    protected virtual void RefillTank(Entity<GasCanisterComponent> canister, EntityUid tank, EntityUid user)
    {
    }

    private void OnHoseDetachAttempt(Entity<SimpleToolUsageComponent> ent, ref AttemptSimpleToolUseEvent args)
    {
        if (TryComp<GasCanisterHoseSlotComponent>(ent.Owner, out var hoseSlot)
            && hoseSlot.HoseSlot.HasItem
            && TryComp<LockComponent>(ent.Owner, out var lockComp)
            && lockComp.Locked)
        {
            args.Cancelled = true;
        }
    }

    private void OnHoseEjectAttempt(Entity<GasCanisterHoseSlotComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Slot.ID != ent.Comp.ContainerName)
            return;

        if (!ent.Comp.AllowEject)
            args.Cancelled = true;
    }

    private void OnHoseDetach(Entity<SimpleToolUsageComponent> ent, ref SimpleToolDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryComp<LockComponent>(ent.Owner, out var lockComp) && lockComp.Locked)
            return;

        if (!TryComp<GasCanisterHoseSlotComponent>(ent.Owner, out var hoseSlot) || !hoseSlot.HoseSlot.HasItem)
            return;

        if (args.Used is { } used && TryComp<ToolComponent>(used, out var tool))
            _tool.PlayToolSound(used, tool, args.User);

        hoseSlot.AllowEject = true;
        _slots.TryEjectToHands(ent.Owner, hoseSlot.HoseSlot, args.User, excludeUserAudio: true);
        hoseSlot.AllowEject = false;
    }
}
