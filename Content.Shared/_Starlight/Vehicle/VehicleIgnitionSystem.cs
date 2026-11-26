using Content.Shared._Starlight.ReagentDraw.Components;
using Content.Shared._Starlight.ReagentDraw.EntitySystems;
using Content.Shared._FarHorizons.Vehicles.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Audio;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Emp;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Vehicle;

public sealed partial class VehicleIgnitionSystem : EntitySystem
{
    [Dependency] private VehicleSystem _vehicles = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedReagentDrawSystem _reagents = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;

    [SubscribeLocalEvent]
    private void OnCanRun(Entity<VehicleIgnitionComponent> ent, ref VehicleCanRunEvent args)
        => args.CanRun &= ent.Comp.Started;

    [SubscribeLocalEvent]
    private void OnOperatorChanged(Entity<VehicleIgnitionComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (args.OldOperator != null)
        {
            Stop(ent);
            if (!_net.IsClient)
                _actions.RemoveAction(ent.Comp.ActionEntity);
        }

        RefreshAction(ent, args.NewOperator);
    }

    [SubscribeLocalEvent]
    private void OnKeyInserted(Entity<VehicleIgnitionComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!_timing.ApplyingState && args.Container.ID == _vehicles.GetVehicleKeySlot(ent))
            RefreshAction(ent, _vehicles.GetOperatorOrNull(ent.Owner));
    }

    [SubscribeLocalEvent]
    private void OnKeyRemoved(Entity<VehicleIgnitionComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState || args.Container.ID != _vehicles.GetVehicleKeySlot(ent))
            return;

        Stop(ent);
        RefreshAction(ent, _vehicles.GetOperatorOrNull(ent.Owner));
    }

    private void RefreshAction(Entity<VehicleIgnitionComponent> ent, EntityUid? driver)
    {
        if (_net.IsClient || TerminatingOrDeleted(ent) || driver is not { } uid)
            return;

        if (_vehicles.HasVehicleKey(ent.Owner))
            _actions.AddAction(uid, ref ent.Comp.ActionEntity, ent.Comp.Action, ent.Owner);
        else
            _actions.RemoveAction(ent.Comp.ActionEntity);

        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnTurnKey(EntityUid owner, VehicleIgnitionComponent component, TurnKeysEvent args)
    {
        var ent = new Entity<VehicleIgnitionComponent>(owner, component);
        if (args.Handled || _vehicles.GetOperatorOrNull(ent.Owner) != args.Performer)
            return;

        args.Handled = true;
        var start = !ent.Comp.Started;
        if (start && !CanStart(ent))
        {
            _popup.PopupClient(Loc.GetString("vehicle-turn-keys-fail"), ent, args.Performer);
            return;
        }

        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.Performer, ent.Comp.StartupTime,
                new TurnKeysDoAfter(start), ent.Owner, target: ent.Owner) { BreakOnMove = true, BreakOnDamage = true }))
            return;

        _popup.PopupClient(Loc.GetString(start ? "vehicle-turn-keys-start" : "vehicle-turn-keys-stop"), ent, args.Performer);
        if (start)
            _audio.PlayPredicted(ent.Comp.StartupSound, ent.Owner, args.Performer);
    }

    [SubscribeLocalEvent]
    private void OnKeyTurned(EntityUid owner, VehicleIgnitionComponent component, TurnKeysDoAfter args)
    {
        var ent = new Entity<VehicleIgnitionComponent>(owner, component);
        if (args.Cancelled || args.Handled || _vehicles.GetOperatorOrNull(ent.Owner) != args.User ||
            args.Start == ent.Comp.Started)
            return;

        if (!args.Start)
        {
            Stop(ent);
            args.Handled = true;
            return;
        }

        if (!CanStart(ent) || !_vehicles.SetIgnitionHandBlockers(ent, true))
        {
            _popup.PopupClient(Loc.GetString("vehicle-turn-keys-fail"), ent, args.User);
            return;
        }

        SetStarted(ent, true);
        args.Handled = true;
    }

    private bool CanStart(Entity<VehicleIgnitionComponent> ent)
    {
        if (!_vehicles.HasVehicleKey(ent.Owner) || CompOrNull<VehicleHullComponent>(ent)?.Broken == true ||
            HasComp<EmpDisabledComponent>(ent))
            return false;

        if (TryComp<VehicleModsComponent>(ent, out var mods) &&
            mods.EquipmentSlots.HasFlag(EquipmentType.ENGINE) &&
            (!mods.Equipment.TryGetValue(EquipmentType.ENGINE, out var engine) ||
                !TryComp<VehicleEquipmentComponent>(engine, out var part) || part.Broken))
            return false;

        if (HasComp<PowerCellDrawComponent>(ent))
            return _powerCell.HasDrawCharge(ent.Owner);

        return _reagents.HasDrawReagent(ent.Owner);
    }

    public void Stop(EntityUid uid)
    {
        if (TryComp<VehicleIgnitionComponent>(uid, out var ignition))
            Stop((uid, ignition));
    }

    private void Stop(Entity<VehicleIgnitionComponent> ent)
    {
        if (!ent.Comp.Started)
            return;

        _vehicles.SetIgnitionHandBlockers(ent, false);
        SetStarted(ent, false);
    }

    private void SetStarted(Entity<VehicleIgnitionComponent> ent, bool started)
    {
        ent.Comp.Started = started;
        Dirty(ent);

        if (TryComp<PowerCellDrawComponent>(ent, out var cellDraw))
            _powerCell.SetDrawEnabled((ent.Owner, cellDraw), started);
        else if (TryComp<ReagentDrawComponent>(ent, out var reagentDraw))
        {
            _reagents.SetDrawEnabled((ent.Owner, reagentDraw), started);
            _ambient.SetAmbience(ent, started);
        }

        _actions.SetToggled(ent.Comp.ActionEntity, started);
        _vehicles.RefreshCanRun(ent.Owner);
        var changed = new VehicleIgnitionChangedEvent(started);
        RaiseLocalEvent(ent, ref changed);
    }

    [SubscribeLocalEvent]
    private void OnCellEmpty(Entity<VehicleIgnitionComponent> ent, ref PowerCellSlotEmptyEvent args)
        => Stop(ent);

    [SubscribeLocalEvent]
    private void OnFuelEmpty(Entity<VehicleIgnitionComponent> ent, ref ReagentContainerSlotEmptyEvent args)
        => Stop(ent);

    [SubscribeLocalEvent]
    private void OnBreak(EntityUid uid, VehicleIgnitionComponent component, BreakageEventArgs args)
        => Stop((uid, component));

    [SubscribeLocalEvent]
    private void OnEmp(Entity<VehicleIgnitionComponent> ent, ref EmpPulseEvent args)
    {
        if (!ent.Comp.Started)
            return;

        Stop(ent);
        args.Affected = true;
    }

    [SubscribeLocalEvent]
    private void OnEjectAttempt(Entity<VehicleIgnitionComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.User is { } user &&
            args.Slot.ContainerSlot?.ID == _vehicles.GetVehicleKeySlot(ent) &&
            _vehicles.GetOperatorOrNull(ent.Owner) is { } driver && user != driver)
        {
            args.Cancelled = true;
        }
    }

    [SubscribeLocalEvent]
    private void OnKeyVerb(EntityUid uid, VehicleIgnitionComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        var ent = new Entity<VehicleIgnitionComponent>(uid, component);
        if (!args.CanInteract || !args.CanAccess || args.Hands == null ||
            _vehicles.GetOperatorOrNull(ent.Owner) is not { } driver || driver == args.User ||
            _vehicles.GetVehicleKeySlot(ent) is not { } slotId ||
            !_slots.TryGetSlot(ent, slotId, out var slot) || slot.Item is not { } key)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("vehicle-verb-steal-keys"),
            Act = () =>
            {
                var delay = new DoAfterArgs(EntityManager, user, ent.Comp.KeyRemovalTime,
                    new EjectVehicleKeyDoAfter(GetNetEntity(key)), ent.Owner, target: ent.Owner)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                };
                if (_doAfter.TryStartDoAfter(delay))
                    _popup.PopupClient(Loc.GetString("vehicle-steal-keys-attempt"), ent, driver);
            },
        });
    }

    [SubscribeLocalEvent]
    private void OnKeyStolen(EntityUid owner, VehicleIgnitionComponent component, EjectVehicleKeyDoAfter args)
    {
        var ent = new Entity<VehicleIgnitionComponent>(owner, component);
        if (args.Cancelled || args.Handled || !_interaction.InRangeUnobstructed(args.User, ent.Owner) ||
            _vehicles.GetVehicleKeySlot(ent) is not { } slot ||
            !TryGetEntity(args.Key, out var key) || key is not { } keyUid ||
            !_blocker.CanPickup(args.User, keyUid) ||
            !_slots.TryGetSlot(ent, slot, out var itemSlot) || itemSlot.Item != key ||
            !_slots.TryEject(ent, itemSlot, null, out var ejected, excludeUserAudio: true))
            return;

        _hands.PickupOrDrop(args.User, ejected.Value);
        args.Handled = true;
    }
}
