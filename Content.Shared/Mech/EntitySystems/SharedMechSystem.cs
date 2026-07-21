using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Emag.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using System.Linq;

#region Starlight
using Content.Shared.Movement.Events;
using Content.Shared.Repairable;
using Content.Shared.Stunnable;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Wires;
using Content.Shared.Electrocution;
using Content.Shared._Starlight.Mech;
using Content.Shared._Starlight.Weapons.Melee.Events;
#endregion

namespace Content.Shared.Mech.EntitySystems;

/// <summary>
/// Handles all of the interactions, UI handling, and items shennanigans for <see cref="MechComponent"/>
/// </summary>
public abstract partial class SharedMechSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] protected VehicleSystem Vehicle = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedBatterySystem _battery = default!; //Starlight

    [SubscribeLocalEvent]
    private void OnToggleEquipmentAction(EntityUid uid, MechComponent component, MechToggleEquipmentEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        CycleEquipment(uid);
    }

    private void OnMechToggleInternals(EntityUid uid, MechComponent component, MechToggleInternalsEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        // STARLIGHT - check for gas tank presence.
        if (component.GasTankSlot.ContainedEntity == null)
        {
            if(_net.IsServer && component.PilotSlot.ContainedEntity != null)
            {
                _popup.PopupEntity(
                    Loc.GetString("mech-no-tank"),
                    uid,
                    component.PilotSlot.ContainedEntity.Value);
            }
            return;
        }

        component.Internals = !component.Internals;

        _actions.SetToggled(component.MechToggleInternalsActionEntity, component.Internals);
    }

    [SubscribeLocalEvent]
    private void OnEjectPilotEvent(EntityUid uid, MechComponent component, MechEjectPilotEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        TryEject(uid, component);
    }

    [SubscribeLocalEvent]
    private void RelayInteractionEvent(EntityUid uid, MechComponent component, UserActivateInWorldEvent args)
    {
        if (!Vehicle.HasOperator(uid))
            return;

        // TODO why is this being blocked?
        if (!_timing.IsFirstTimePredicted)
            return;

        if (component.CurrentSelectedEquipment != null)
        {
            RaiseLocalEvent(component.CurrentSelectedEquipment.Value, args);
        }
    }

    [SubscribeLocalEvent]
    private void OnStartup(EntityUid uid, MechComponent component, ComponentStartup args)
    {
        component.PilotSlot = _container.EnsureContainer<ContainerSlot>(uid, component.PilotSlotId);
        component.PilotSlot.OccludesLight = false; //starlight
        component.EquipmentContainer = _container.EnsureContainer<Container>(uid, component.EquipmentContainerId);
        component.BatterySlot = _container.EnsureContainer<ContainerSlot>(uid, component.BatterySlotId);
        component.GasTankSlot = _container.EnsureContainer<ContainerSlot>(uid, component.GasTankSlotId);
        UpdateAppearance(uid, component);
    }

    [SubscribeLocalEvent]
    private void OnDestruction(EntityUid uid, MechComponent component, DestructionEventArgs args)
    {
        BreakMech(uid, component);
    }

    [SubscribeLocalEvent]
    private void OnEntityStorageDump(Entity<MechComponent> entity, ref EntityStorageIntoContainerAttemptEvent args)
    {
        // There's no reason we should dump into /any/ of the mech's containers.
        args.Cancelled = true;
    }

    private void SetupUser(EntityUid mech, EntityUid pilot, MechComponent? component = null)
    {
        if (!Resolve(mech, ref component))
            return;

        // Warning: this bypasses most normal interaction blocking components on the user, like drone laws and the like.
        var irelay = EnsureComp<InteractionRelayComponent>(pilot);
        _interaction.SetRelay(pilot, mech, irelay);

        if ((component.Integrity / component.MaxIntegrity) * 100 >= 50)
            if (component.FirstStart)
            {
                _audioSystem.PlayPredicted(component.NominalLongSound, mech, mech); //Starlight Edit: Play Predicted
                component.FirstStart = false;
                Dirty(mech, component);
            }
            else
                _audioSystem.PlayPredicted(component.NominalSound, mech, mech); //Starlight Edit: Play Predicted
        else
            _audioSystem.PlayPredicted(component.CriticalDamageSound, mech, mech); //Starlight Edit: Play Predicted

        UpdateActions(mech, pilot, component);
    }

    private void UpdateActions(EntityUid mech, EntityUid pilot, MechComponent? component = null)
    {
        if (!Resolve(mech, ref component))
            return;

        if (_net.IsClient)
            return;

        _actions.AddAction(pilot, ref component.MechCycleActionEntity, component.MechCycleAction, mech);
        _actions.AddAction(pilot, ref component.MechUiActionEntity, component.MechUiAction, mech);
        _actions.AddAction(pilot, ref component.MechEjectActionEntity, component.MechEjectAction, mech);
        if (component.Airtight)
            _actions.AddAction(pilot, ref component.MechToggleInternalsActionEntity, component.MechToggleInternalsAction, mech);
        if (_light.TryGetLight(mech, out var light))
            _actions.AddAction(pilot, ref component.MechToggleLightActionEntity, component.MechToggleLightAction, mech);
    }

    private void RemoveUser(EntityUid mech, EntityUid pilot)
    {
        RemComp<InteractionRelayComponent>(pilot);
        _actions.RemoveProvidedActions(pilot, mech);
    }

    /// <summary>
    /// Destroys the mech, removing the user and ejecting anything contained.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    public virtual void BreakMech(EntityUid uid, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        TryEject(uid, component);
        var equipment = new List<EntityUid>(component.EquipmentContainer.ContainedEntities);
        foreach (var ent in equipment)
        {
            RemoveEquipment(uid, ent, component, forced: true);
        }

        component.Broken = true;
        UpdateAppearance(uid, component);
    }

    /// <summary>
    /// Cycles through the currently selected equipment.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    public void CycleEquipment(EntityUid uid, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var allEquipment = component.EquipmentContainer.ContainedEntities.ToList();

        var equipmentIndex = -1;
        if (component.CurrentSelectedEquipment != null)
        {
            bool StartIndex(EntityUid u) => u == component.CurrentSelectedEquipment;
            equipmentIndex = allEquipment.FindIndex(StartIndex);
        }

        equipmentIndex++;
        component.CurrentSelectedEquipment = equipmentIndex >= allEquipment.Count
            ? null
            : allEquipment[equipmentIndex];

        var popupString = component.CurrentSelectedEquipment != null
            ? Loc.GetString("mech-equipment-select-popup", ("item", component.CurrentSelectedEquipment))
            : Loc.GetString("mech-equipment-select-none-popup");

        if (_net.IsServer)
            _popup.PopupEntity(popupString, uid);

        Dirty(uid, component);
    }

    /// <summary>
    /// Inserts an equipment item into the mech.
    /// </summary>
    /// <param name="uid"> Mech </param>
    /// <param name="toInsert"> Equipment what inserted </param>
    /// <param name="component"> Mech Component </param>
    /// <param name="equipmentComponent"> Equipment Component </param>
    public void InsertEquipment(EntityUid uid, EntityUid toInsert, MechComponent? component = null,
        MechEquipmentComponent? equipmentComponent = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!Resolve(toInsert, ref equipmentComponent))
            return;

        if (component.EquipmentContainer.ContainedEntities.Count >= component.MaxEquipmentAmount)
            return;

        if (_whitelistSystem.IsWhitelistFail(component.EquipmentWhitelist, toInsert))
            return;

        // Starlight start
        var toInsertMeta = MetaData(toInsert);

        var equipment = new List<EntityUid>(component.EquipmentContainer.ContainedEntities);
        foreach (var ent in equipment)
        {
            var entMeta = MetaData(ent);
            if (entMeta.EntityPrototype == toInsertMeta.EntityPrototype)
                return;
        }
        // Starlight end

        equipmentComponent.EquipmentOwner = uid;
        _container.Insert(toInsert, component.EquipmentContainer);
        var ev = new MechEquipmentInsertedEvent(uid);
        RaiseLocalEvent(toInsert, ref ev);
        // Starlight-edit: UpdateUserInterface moved on server side
        if (component.PilotSlot.ContainedEntity != null)
            UpdateActions(uid, component.PilotSlot.ContainedEntity.Value, component);
    }

    /// <summary>
    /// Removes an equipment item from a mech.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="toRemove"></param>
    /// <param name="component"></param>
    /// <param name="equipmentComponent"></param>
    /// <param name="forced">
    ///     Whether or not the removal can be cancelled, and if non-mech equipment should be ejected.
    /// </param>
    public void RemoveEquipment(EntityUid uid, EntityUid toRemove, MechComponent? component = null,
        MechEquipmentComponent? equipmentComponent = null, bool forced = false)
    {
        if (!Resolve(uid, ref component))
            return;

        // When forced, we also want to handle the possibility that the "equipment" isn't actually equipment.
        // This /shouldn't/ be possible thanks to OnEntityStorageDump, but there's been quite a few regressions
        // with entities being hardlock stuck inside mechs.
        if (!Resolve(toRemove, ref equipmentComponent) && !forced)
            return;

        if (!forced)
        {
            var attemptev = new AttemptRemoveMechEquipmentEvent();
            RaiseLocalEvent(toRemove, ref attemptev);
            if (attemptev.Cancelled)
                return;
        }

        var ev = new MechEquipmentRemovedEvent(uid);
        RaiseLocalEvent(toRemove, ref ev);

        if (component.CurrentSelectedEquipment == toRemove)
            CycleEquipment(uid, component);

        if (forced && equipmentComponent != null)
            equipmentComponent.EquipmentOwner = null;

        _container.Remove(toRemove, component.EquipmentContainer);
        // Starlight-edit: UpdateUserInterface moved on server side.
    }

    /// <summary>
    /// Attempts to change the amount of energy in the mech.
    /// TODO: Power cells are predicted now, so no need to duplicate the charge level
    /// </summary>
    /// <param name="uid">The mech itself</param>
    /// <param name="delta">The change in energy</param>
    /// <param name="component"></param>
    /// <returns>If the energy was successfully changed.</returns>
    public virtual bool TryChangeEnergy(EntityUid uid, FixedPoint2 delta, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (component.Energy + delta < 0)
            return false;

        //Starlight Start - play sounds dependent on *battery*
        if (component.BatterySlot.ContainedEntity != null && TryComp(component.BatterySlot.ContainedEntity, out BatteryComponent? battery))
        {
            if ((int)(_battery.GetCharge((component.BatterySlot.ContainedEntity.Value, battery)) / battery.MaxCharge * 100) <= 33 //Starlight Edit: Earlier low power warning, and we run it off of the % power readout
                && component.PlayPowerSound
                && component.PilotSlot.ContainedEntity != null)
            {
                _audioSystem.PlayPredicted(component.LowPowerSound, uid, component.PilotSlot.ContainedEntity.Value); //Starlight: Play Predicted

                component.PlayPowerSound = false;
            }
            else if ((int)(_battery.GetCharge((component.BatterySlot.ContainedEntity.Value, battery)) / battery.MaxCharge * 100) > 33) //Starlight Edit: Earlier low power warning, and we run it off of the % power readout
                component.PlayPowerSound = true;
        }
        //Starlight End
        component.Energy = FixedPoint2.Clamp(component.Energy + delta, 0, component.MaxEnergy);
        Dirty(uid, component);
        UpdateUserInterface(uid, component);
        return true;
    }

    /// <summary>
    /// Sets the integrity of the mech.
    /// </summary>
    /// <param name="uid">The mech itself</param>
    /// <param name="value">The value the integrity will be set at</param>
    /// <param name="component"></param>
    public void SetIntegrity(EntityUid uid, FixedPoint2 value, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Integrity = FixedPoint2.Clamp(value, 0, component.MaxIntegrity);

        if (component.Integrity <= 0)
        {
            BreakMech(uid, component);
        }
        else if (component.Broken)
        {
            component.Broken = false;
            UpdateAppearance(uid, component);
        }

        Dirty(uid, component);
        UpdateUserInterface(uid, component);
    }

    /// <summary>
    /// Checks if an entity can be inserted into the mech.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="toInsert"></param>
    /// <param name="component"></param>
    /// <returns></returns>
    public bool CanInsert(EntityUid uid, EntityUid toInsert, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!_actionBlocker.CanMove(toInsert))
            return false;

        if (Vehicle.GetOperatorOrNull(uid) == toInsert)
            return false;

        if (!_container.CanInsert(toInsert, component.PilotSlot))
            return false;

        return true;
    }

    /// <summary>
    /// Updates the user interface
    /// </summary>
    /// <remarks>
    /// This is defined here so that UI updates can be accessed from shared.
    /// </remarks>
    public virtual void UpdateUserInterface(EntityUid uid, MechComponent? component = null)
    {
    }

    /// <summary>
    /// Attempts to insert a pilot into the mech.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="toInsert"></param>
    /// <param name="component"></param>
    /// <returns>Whether or not the entity was inserted</returns>
    public bool TryInsert(EntityUid uid, EntityUid toInsert, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!CanInsert(uid, toInsert, component))
            return false;


        // Starlight Begin - Pilot Events
        var ev = new BeforePilotInsertEvent(uid, toInsert.Value);
        RaiseLocalEvent(uid, ref ev);
        var equipment = new List<EntityUid>(component.EquipmentContainer.ContainedEntities);
        foreach (var ent in equipment)
        {
            RaiseLocalEvent(ent, ref ev);
        }

        RaiseLocalEvent(toInsert.Value, ref ev);
        // Starlight End

        _container.Insert(toInsert, component.PilotSlot);
        return true;
    }

    /// <summary>
    /// Attempts to eject the current pilot from the mech
    /// </summary>
    /// <param name="uid"> mech </param>
    /// <param name="component"> mech component </param>
    /// <returns>Whether or not the pilot was ejected.</returns>
    public bool TryEject(EntityUid uid, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!Vehicle.TryGetOperator(uid, out var operatorEnt))
            return false;

        return _container.RemoveEntity(uid, operatorEnt.Value);
    }

    [SubscribeLocalEvent]
    private void OnGetMeleeWeapon(Entity<VehicleOperatorComponent> ent, ref GetMeleeWeaponEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Vehicle is not { } vehicle)
            return;

        if (!TryComp<MechComponent>(vehicle, out var mech))
            return;

        var weapon = mech.CurrentSelectedEquipment ?? vehicle;
        args.Weapon = weapon;
        args.Handled = true;
    }

    public void UpdateAppearance(EntityUid uid, MechComponent? component = null,
        AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref component, ref appearance, false))
            return;

        _appearance.SetData(uid, MechVisuals.Open, !Vehicle.HasOperator(uid), appearance);
        _appearance.SetData(uid, MechVisuals.Broken, component.Broken, appearance);
        _appearance.SetData(uid, MechVisuals.Light, component.Light, appearance);
    }

    [SubscribeLocalEvent]
    private void OnDragDrop(EntityUid uid, MechComponent component, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.Dragged, component.EntryDelay, new MechEntryEvent(), uid, target: uid)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    [SubscribeLocalEvent]
    private void OnCanDragDrop(EntityUid uid, MechComponent component, ref CanDropTargetEvent args)
    {
        args.Handled = true;

        args.CanDrop |= !component.Broken && CanInsert(uid, args.Dragged, component);
    }

    [SubscribeLocalEvent]
    private void OnOperatorSet(Entity<MechComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (args.OldOperator is { } oldOperator)
        {
            RemoveUser(ent, oldOperator);
        }

        if (args.NewOperator is { } newOperator)
        {
            SetupUser(ent, newOperator, ent);
        }

        UpdateAppearance(ent);
        UpdateUserInterface(ent, ent);
    }
}

/// <summary>
///     Event raised when the battery is successfully removed from the mech,
///     on both success and failure
/// </summary>
[Serializable, NetSerializable]
public sealed partial class RemoveBatteryEvent : SimpleDoAfterEvent;

/// <summary>
///     Event raised when a person removes someone from a mech,
///     on both success and failure
/// </summary>
[Serializable, NetSerializable]
public sealed partial class MechExitEvent : SimpleDoAfterEvent;

/// <summary>
///     Event raised when a person enters a mech, on both success and failure
/// </summary>
[Serializable, NetSerializable]
public sealed partial class MechEntryEvent : SimpleDoAfterEvent;
