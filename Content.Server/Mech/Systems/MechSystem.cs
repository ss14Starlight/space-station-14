using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Hands.Systems;
using Content.Server.Mech.Components;
using Content.Shared.Atmos;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

#region Starlight

using Content.Shared.Actions;
using Content.Shared.Atmos.Components;
using Content.Shared.Hands.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Power;
using Content.Shared.Tag;
using Content.Shared.Toggleable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Content.Shared.Movement.Systems;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Server._Starlight.Mech.Components;
#endregion Starlight

namespace Content.Server.Mech.Systems;

/// <inheritdoc/>
public sealed partial class MechSystem : SharedMechSystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedToolSystem _toolSystem = default!;

#region Starlight
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private NpcFactionSystem _factionSystem = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private IGameTiming Timing = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private GasTankSystem _gasTank = default!;
    [Dependency] private SharedWiresSystem _wires = default!;
#endregion Starlight


    private static readonly ProtoId<ToolQualityPrototype> PryingQuality = "Prying";

    [SubscribeLocalEvent]
    private void OnMechCanMoveEvent(Entity<MechComponent> ent, ref VehicleCanRunEvent args)
    {
        if (ent.Comp.Broken || ent.Comp.Integrity <= 0 || ent.Comp.Energy <= 0)
            args.CanRun = false;
    }

    [SubscribeLocalEvent]
    private void OnInteractUsing(EntityUid uid, MechComponent component, InteractUsingEvent args)
    {
        if (TryComp<WiresPanelComponent>(uid, out var panel) && !panel.Open)
            return;

        if (component.BatterySlot.ContainedEntity == null && TryComp<BatteryComponent>(args.Used, out var battery)
        && _whitelistSystem.IsWhitelistPassOrNull(component.BatteryWhitelist, args.Used)) // Starlight - Mech Reactors
        {
            InsertBattery(uid, args.Used, component, battery);
            _actionBlocker.UpdateCanMove(uid);
            return;
        }
        if (component.GasTankSlot.ContainedEntity == null && TryComp<GasTankComponent>(args.Used, out var gasTank))
        {
            InsertGasTank(uid, args.Used, component, gasTank);
        }

        if (_toolSystem.HasQuality(args.Used, PryingQuality) && component.BatterySlot.ContainedEntity != null)
        {
            var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.BatteryRemovalDelay,
                new RemoveBatteryEvent(), uid, target: uid, used: args.Used)
            {
                BreakOnMove = true
            };

                _doAfter.TryStartDoAfter(doAfterEventArgs);
            }
            else if (component.GasTankSlot.ContainedEntity != null)
            {
                var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.BatteryRemovalDelay,
                    new RemoveGasTankEvent(), uid, target: uid, used: args.Target)
                {
                    BreakOnMove = true
                };

                _doAfter.TryStartDoAfter(doAfterEventArgs);
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnInsertBattery(EntityUid uid, MechComponent component, EntInsertedIntoContainerMessage args)
    {
        UpdateUserInterface(uid, component); // Starlight-edit: Correct equipment update
        if (!(args.Container != component.BatterySlot || !TryComp<BatteryComponent>(args.Entity, out var battery)))
        {
            component.Energy = _battery.GetCharge((args.Entity, battery)); // Starlight-edit: Correct energy update
            component.MaxEnergy = battery.MaxCharge;

        Dirty(uid, component);
        Vehicle.RefreshCanRun(uid);
    }

    [SubscribeLocalEvent]
    private void OnRemoveBattery(EntityUid uid, MechComponent component, RemoveBatteryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        RemoveBattery(uid, component);
        _actionBlocker.UpdateCanMove(uid);

        args.Handled = true;
    }

    // STARLIGHT
    private void OnRemoveGasTank(EntityUid uid, MechComponent component, RemoveGasTankEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        _container.EmptyContainer(component.GasTankSlot);

        args.Handled = true;

        component.Internals = false;
        _actions.SetToggled(component.MechToggleInternalsActionEntity, component.Internals);
    }

    [SubscribeLocalEvent]
    private void OnMapInit(EntityUid uid, MechComponent component, MapInitEvent args)
    {
        var xform = Transform(uid);
        // TODO: this should use containerfill?
        foreach (var equipment in component.StartingEquipment)
        {
            var ent = Spawn(equipment, xform.Coordinates);
            InsertEquipment(uid, ent, component);
        }

        // TODO: this should just be damage and battery
        component.Integrity = component.MaxIntegrity;
        component.Energy = component.MaxEnergy;

        Vehicle.RefreshCanRun(uid);
        Dirty(uid, component);
    }

    [SubscribeLocalEvent]
    private void OnRemoveEquipmentMessage(EntityUid uid, MechComponent component, MechEquipmentRemoveMessage args)
    {
        var equip = GetEntity(args.Equipment);

        if (!Exists(equip) || Deleted(equip))
            return;

        if (!component.MaintenanceMode)
        {
            _popup.PopupEntity(Loc.GetString("mech-requires-maintenance-mode"), uid, PopupType.MediumCaution);
            return;
        }

        if (!component.EquipmentContainer.ContainedEntities.Contains(equip))
            return;

        RemoveEquipment(uid, equip, component);
    }

    #region Starlight
    private void OnMaintenanceMessage(EntityUid uid, MechComponent component, MechMaintenanceUiMessage args)
    {
        TryToggleMaintenanceMode(uid, component, args.Toggle);

        Dirty(uid, component); // Starlight-edit: Update Maintenance State

        UpdateCanMove(uid, component); // Starlight-edit: fix movement block
    }

    private void TryToggleMaintenanceMode(EntityUid uid, MechComponent component, bool toggle)
    {
        if (TryComp<WiresPanelComponent>(uid, out var panelComp))
        {
            _wires.TogglePanel(uid, panelComp, toggle, uid);
        }

        if (component.MaintenanceMode != toggle)
        {
            var popupString = toggle ? "mech-maintenance-enabled" : "mech-maintenance-disabled";
            _popup.PopupPredicted(Loc.GetString(popupString), uid, uid);

            var panelSound = toggle ? component.MaintenanceOnSound : component.MaintenanceOffSound;
            _audioSystem.PlayPredicted(panelSound, uid, uid);
        }

        component.MaintenanceMode = toggle;

        UpdateUserInterface(uid, component);
    }
    #endregion

    [SubscribeLocalEvent]
    private void OnOpenUi(EntityUid uid, MechComponent component, MechOpenUiEvent args)
    {
        args.Handled = true;
        ToggleMechUi(uid, component);
    }

    [SubscribeLocalEvent]
    private void OnToolUseAttempt(Entity<VehicleOperatorComponent> ent, ref ToolUserAttemptUseEvent args)
    {
        if (ent.Comp.Vehicle is { } vehicle && args.Target == vehicle)
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnAlternativeVerb(EntityUid uid, MechComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Starlight begin - restrict UI verb to pilot, if pilot present
        if (IsEmpty(component) || args.User == component.PilotSlot.ContainedEntity)
        {
            var openUiVerb = new AlternativeVerb
            {
                Act = () => ToggleMechUi(uid, component, args.User),
                Text = Loc.GetString("mech-ui-open-verb")
            };
            args.Verbs.Add(openUiVerb);
        }
        // Starlight end

        if (component.Broken)
            return;

        if (CanInsert(uid, args.User, component))
        {
            var enterVerb = new AlternativeVerb
            {
                Text = Loc.GetString("mech-verb-enter"),
                Act = () =>
                {
                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.EntryDelay, new MechEntryEvent(), uid, target: uid)
                    {
                        BreakOnMove = true,
                    };

                    _doAfter.TryStartDoAfter(doAfterEventArgs);
                }
            };
            args.Verbs.Add(enterVerb);
        }
        else if (Vehicle.HasOperator(uid))
        {
            var operatorUid = Vehicle.GetOperatorOrNull(uid);
            var ejectVerb = new AlternativeVerb
            {
                Text = Loc.GetString("mech-verb-exit"),
                Priority = 1, // Promote to top to make ejecting the ALT-click action
                Act = () =>
                {
                    if (args.User == uid || args.User == operatorUid)
                    {
                        TryEject(uid, component);
                        return;
                    }

                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.ExitDelay, new MechExitEvent(), uid, target: uid)
                    {
                        BreakOnMove = true,
                    };
                    _popup.PopupEntity(Loc.GetString("mech-eject-pilot-alert", ("item", uid), ("user", Identity.Entity(args.User, EntityManager))), uid, PopupType.Large);

                    _doAfter.TryStartDoAfter(doAfterEventArgs);
                }
            };
            args.Verbs.Add(ejectVerb);
        }
    }

    [SubscribeLocalEvent]
    private void OnMechEntry(EntityUid uid, MechComponent component, MechEntryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!Vehicle.CanOperate(uid, args.User))
        {
            _popup.PopupEntity(Loc.GetString("mech-no-enter", ("item", uid)), Identity.Entity(args.User, EntityManager));
            return;
        }

        TryInsert(uid, args.User, component);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnMechExit(EntityUid uid, MechComponent component, MechExitEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!TryEject(uid, component))
            return;

        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnDamageChanged(EntityUid uid, MechComponent component, DamageChangedEvent args)
    {
        var integrity = component.MaxIntegrity - _damageable.GetTotalDamage((uid, args.Damageable));
        SetIntegrity(uid, integrity, component);
    }

    private void ToggleMechUi(EntityUid uid, MechComponent? component = null, EntityUid? user = null)
    {
        if (!Resolve(uid, ref component))
            return;
        user ??= Vehicle.GetOperatorOrNull(uid);
        if (user == null)
            return;

        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        _ui.TryToggleUi(uid, MechUiKey.Key, actor.PlayerSession);
        UpdateUserInterface(uid, component);
    }

    [SubscribeLocalEvent]
    private void RelayGrabberUiMessage(EntityUid uid, MechComponent component, ref MechGrabberEjectMessage args)
    {
        ReceiveEquipmentUiMesssages(component, args);
    }

    [SubscribeLocalEvent]
    private void RelaySoundboardUiMessage(EntityUid uid, MechComponent component, ref MechSoundboardPlayMessage args)
    {
        ReceiveEquipmentUiMesssages(component, args);
    }

    private void ReceiveEquipmentUiMesssages<T>(MechComponent component, T args) where T : MechEquipmentUiMessage
    {
        var ev = new MechEquipmentUiMessageRelayEvent(args);
        var allEquipment = new List<EntityUid>(component.EquipmentContainer.ContainedEntities);
        var argEquip = GetEntity(args.Equipment);

        foreach (var equipment in allEquipment)
        {
            if (argEquip == equipment)
                RaiseLocalEvent(equipment, ev);
        }
    }

    public override void UpdateUserInterface(EntityUid uid, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        base.UpdateUserInterface(uid, component);

        var ev = new MechEquipmentUiStateReadyEvent();
        foreach (var ent in component.EquipmentContainer.ContainedEntities)
        {
            RaiseLocalEvent(ent, ev);
        }

        var state = new MechBoundUiState
        {
            EquipmentStates = ev.States,
            Equipment = component.EquipmentContainer.ContainedEntities.Select(o => GetNetEntity(o)).ToList() // Starlight-edit: Correct equipment update
        };
        _ui.SetUiState(uid, MechUiKey.Key, state);
    }

    public override void BreakMech(EntityUid uid, MechComponent? component = null)
    {
        base.BreakMech(uid, component);

        _ui.CloseUi(uid, MechUiKey.Key);
        Vehicle.RefreshCanRun(uid);
    }

    public override bool TryChangeEnergy(EntityUid uid, FixedPoint2 delta, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!base.TryChangeEnergy(uid, delta, component))
            return false;

        var battery = component.BatterySlot.ContainedEntity;
        if (battery == null)
            return false;

        //Starlight Start - use the actual battery system to handle charge
        if (component.BatterySlot.ContainedEntity != null &&
            TryComp<BatteryComponent>(component.BatterySlot.ContainedEntity.Value, out var batteryComp))
        {
            _battery.TryUseCharge((component.BatterySlot.ContainedEntity.Value, batteryComp), (float)FixedPoint2.Abs(delta));

            var currentCharge = _battery.GetCharge((component.BatterySlot.ContainedEntity.Value, batteryComp));
            if (component.Energy != currentCharge)
            {
                var ev = new ChargeChangedEvent(currentCharge, 0, batteryComp.ChargeRate, batteryComp.MaxCharge);
                RaiseLocalEvent(uid, ref ev);
            }
        }
        //Starlight End
        // TODO: Power cells are predicted now, so no need to duplicate the charge level
        var charge = _battery.GetCharge(battery.Value);
        if (charge != component.Energy) //if there's a discrepency, we have to resync them
        {
            Log.Debug($"Battery charge was not equal to mech charge. Battery {charge}. Mech {component.Energy}");
            component.Energy = charge;
            Dirty(uid, component);
        }
        Vehicle.RefreshCanRun(uid);
        return true;
    }
    public void InsertGasTank(EntityUid uid, EntityUid toInsert, MechComponent? component = null, GasTankComponent? gasTank = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (!Resolve(toInsert, ref gasTank, false))
            return;

        _container.Insert(toInsert, component.GasTankSlot);
        Dirty(uid, component);
    }
    public void InsertBattery(EntityUid uid, EntityUid toInsert, MechComponent? component = null, BatteryComponent? battery = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (!Resolve(toInsert, ref battery, false))
            return;

        var mechBattery = EnsureComp<MechBatteryComponent>(toInsert); // Starlight-edit: Correct Charge/UI Update
        mechBattery.Mech = uid; // Starlight-edit: Correct Charge/UI Update

        _container.Insert(toInsert, component.BatterySlot);
        component.Energy = _battery.GetCharge(toInsert);
        component.MaxEnergy = battery.MaxCharge;

        Vehicle.RefreshCanRun(uid);

        Dirty(uid, component);
        UpdateUserInterface(uid, component);
    }

    public void RemoveBattery(EntityUid uid, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.BatterySlot.ContainedEntity == null) // Starlight-edit: Correct Charge/UI Update
            return;

        RemComp<MechBatteryComponent>(component.BatterySlot.ContainedEntity.Value); // Starlight-edit: Correct Charge/UI Update

        _container.EmptyContainer(component.BatterySlot);
        component.Energy = 0;
        component.MaxEnergy = 0;

        Vehicle.RefreshCanRun(uid);

        Dirty(uid, component);
        UpdateUserInterface(uid, component);
    }

    #region Atmos Handling
    [SubscribeLocalEvent]
    private void OnInhale(Entity<VehicleOperatorComponent> ent, ref ref InhaleLocationEvent args)
    {
        if (ent.Comp.Vehicle is not { } vehicle ||
            !TryComp<MechComponent>(vehicle, out var mech) ||
            !TryComp<MechAirComponent>(vehicle, out var mechAir))
        {
            return;
        }

        // Breathe in surrounding gas if you don't have internals on or equipped. Even if Airtight.
        if (!mech.Internals || mech.GasTankSlot.ContainedEntity == null)
        {
            args.Gas = _atmosphere.GetContainingMixture(component.Mech);
            return;
        }

        var tankEnt = mech.GasTankSlot.ContainedEntity.Value;
        if (TryComp<GasTankComponent>(tankEnt, out var gasTank) && gasTank.Air.Volume > 0)
            args.Gas = _gasTank.RemoveAirVolume((tankEnt, gasTank), args.Respirator.BreathVolume);
    }
    // STARLIGHT END

    [SubscribeLocalEvent]
    private void OnExhale(Entity<VehicleOperatorComponent> ent, ref ExhaleLocationEvent args)
    {
        if (ent.Comp.Vehicle is not { } vehicle ||
            !TryComp<MechComponent>(vehicle, out var mech) ||
            !TryComp<MechAirComponent>(vehicle, out var mechAir))
        {
            return;
        }

        args.Gas = _atmosphere.GetContainingMixture(component.Mech); // STARLIGHT
    }

    [SubscribeLocalEvent]
    private void OnExpose(Entity<VehicleOperatorComponent> ent, ref AtmosExposedGetAirEvent args)
    {
        if (args.Handled || ent.Comp.Vehicle is not { } vehicle)
            return;

        if (!TryComp(vehicle, out MechComponent? mech))
            return;

        if (mech.Airtight && TryComp(vehicle, out MechAirComponent? air))
        {
            args.Handled = true;
            args.Gas = air.Air;
            return;
        }

        args.Gas = _atmosphere.GetContainingMixture(vehicle, excite: args.Excite);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnGetFilterAir(EntityUid uid, MechAirComponent comp, ref GetFilterAirEvent args)
    {
        var moles = Atmospherics.OneAtmosphere * comp.Air.Volume / (Atmospherics.R * Atmospherics.T20C);
        // "Skin" interactions will still have issues (ex. slime + h2o), so choosing a safe gas here
        comp.Air.SetMoles(Gas.Frezon, moles); // You will get in the LCL and you will like it
        comp.Air.Temperature = Atmospherics.T20C;
        comp.Air.MarkImmutable(); // Disallow changes explicitly to prevent gas reactions.
    }
    // STARLIGHT END
    #endregion
}
