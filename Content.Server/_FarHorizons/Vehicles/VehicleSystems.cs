using Content.Shared._FarHorizons.Vehicles;
using Content.Shared._FarHorizons.Vehicles.Components;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Access.Components;
using Content.Shared.Actions;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using System.Numerics;
using System.Linq;
using Content.Server.PowerCell;
using Content.Shared.PowerCell;
using Robust.Shared.Physics.Events;
using Content.Server.Stunnable;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared._FarHorizons.ReagantDraw.Components;
using Content.Server._FarHorizons.ReagantDraw.EntitySystems;
using Content.Shared.Audio;
using Content.Shared.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Timing;
using Robust.Shared.Containers;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Interaction.Components;
using Content.Shared.DragDrop;
using Content.Shared.Verbs;
using Content.Shared.Destructible;
using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Content.Shared.Lock;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Content.Server.Wieldable;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Damage.Components;
using Content.Server.Damage.Systems;
using Content.Server.Destructible;
using Content.Shared.Repairable;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Effects;
using Robust.Shared.Player;
using Content.Shared.Administration.Logs;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Hands;
using Content.Shared._FarHorizons.Vehicles.Events;

namespace Content.Server._FarHorizons.Vehicles;

public sealed partial class VehicleSystems : SharedVehicleSystems
{
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly ReagantDrawSystem _reagantDraw = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly WieldableSystem _wield = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly LockSystem _lock = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    private static readonly ProtoId<TagPrototype> _vehicleKeyTag = "VehicleKey";
    private static readonly string _bluntname = "Blunt";
    private EntityQuery<ProjectileComponent> _projQuery;
    public override void Initialize()
    {
        base.Initialize();

        _projQuery = GetEntityQuery<ProjectileComponent>();

        SubscribeLocalEvent<VehicleComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<VehicleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VehicleComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);
        SubscribeLocalEvent<VehicleComponent, ItemSlotInsertEvent>(OnInsertEvent);
        SubscribeLocalEvent<VehicleComponent, ItemSlotEjectEvent>(OnEjectEvent);
        SubscribeLocalEvent<VehicleComponent, EjectKeysDoAfter>(OnEjectKeysDoAfter);
        SubscribeLocalEvent<VehicleComponent, TurnKeysDoAfter>(OnTurnKeysDoAfter);
        SubscribeLocalEvent<VehicleComponent, ReagantContainerSlotEmptyEvent>(OnEmptyReagantContainer);
        SubscribeLocalEvent<VehicleComponent, PowerCellSlotEmptyEvent>(OnPowerCellEmpty);
        SubscribeLocalEvent<VehicleComponent, RepairFinishedEvent>(OnRepairFinished);
        SubscribeLocalEvent<VehicleComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<VehicleComponent, BreakageEventArgs>(OnBreakageEvent);
        SubscribeLocalEvent<VehicleComponent, DamageChangedEvent>(OnDamageChanged);

        SubscribeLocalEvent<VehicleBuckleComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<VehicleBuckleComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<VehicleBuckleComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        SubscribeLocalEvent<VehicleBuckleComponent, VehicleUnbuckleDoAfter>(OnUnbuckleDoAfter);
        SubscribeLocalEvent<VehicleBuckleComponent, RefreshMovementSpeedModifiersEvent>(OnMovementSpeedRefreshVehicleEvent);

        SubscribeLocalEvent<VehicleContainerComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<VehicleContainerComponent, VehicleEntryDoAfter>(OnVehicleEntryDoAfter);
        SubscribeLocalEvent<VehicleContainerComponent, VehicleRemoveDoAfter>(OnVehicleRemoveDoAfter);
        SubscribeLocalEvent<VehicleContainerComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeVerb);
        SubscribeLocalEvent<VehicleContainerComponent, EntInsertedIntoContainerMessage>(OnEntInserted);

        SubscribeLocalEvent<RiderComponent, StunnedEvent>(OnStunned);
        SubscribeLocalEvent<RiderComponent, KnockedDownEvent>(OnKnockdown);
        SubscribeLocalEvent<RiderComponent, UpdateCanMoveEvent>(OnUpdateCanMoveEvent);
        SubscribeLocalEvent<RiderComponent, JumpActionEvent>(OnJumpActionEvent);
        SubscribeLocalEvent<RiderComponent, WieldAttemptEvent>(OnWieldAttemptEvent);
        SubscribeLocalEvent<RiderComponent, ShooterImpulseEvent>(OnShooterEvent);
        SubscribeLocalEvent<RiderComponent, RefreshMovementSpeedModifiersEvent>(OnMovementSpeedRefreshRiderEvent);
        SubscribeLocalEvent<RiderComponent, DidEquipHandEvent>(OnHandEquippedRider);

        SubscribeLocalEvent<GunComponent, ItemWieldedEvent>(OnGunWielded);
        SubscribeLocalEvent<GunComponent, ItemUnwieldedEvent>(OnGunUnwielded);
        SubscribeLocalEvent<GunComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);

        SubscribeLocalEvent<TransformComponent, JetJumpActionEvent>(OnJetJumpActionEvent);
        SubscribeLocalEvent<DidEquipHandEvent>(OnHandEquipped);
        _transform.OnGlobalMoveEvent += OnMoveEvent;
    }

    #region Vehicle Generic Events

    private void OnComponentStartup(Entity<VehicleComponent> ent, ref ComponentStartup args)
    {

        if(TryComp<VehicleContainerComponent>(ent.Owner, out var vcComp))
        {
            vcComp.PassengerSlot = _container.EnsureContainer<Container>(ent.Owner, vcComp.PassengerSlotId);
            Dirty(ent.Owner, vcComp);
        }
    }

    private void OnMapInit(Entity<VehicleComponent> ent, ref MapInitEvent args)
    {
        if(TryComp<ContainerManagerComponent>(ent.Owner, out var container))
        {
            ent.Comp.hasKeys = container.Containers.Values
                .SelectMany(c => c.ContainedEntities)
                .Any(e => _tags.HasTag(e, _vehicleKeyTag));
        }
        EnsureComp<VehicleActionsComponent>(ent.Owner);
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnInsertEvent(Entity<VehicleComponent> ent, ref ItemSlotInsertEvent args)
    {
        if(_tags.HasTag(args.Item, _vehicleKeyTag))
        {
            ent.Comp.hasKeys = true;
            Dirty(ent.Owner, ent.Comp);
            var target = args.User;
            if(target != null)
            {
                if(TryComp<BuckleComponent>(target, out var buckleComp) && buckleComp.BuckledTo == ent.Owner && ent.Comp.Rider == null)
                    SetUpRider(target.Value, ent.Owner, ent.Comp);
                if(TryComp<VehicleContainerComponent>(ent.Owner, out var vcComp) && vcComp.PassengerSlot.ContainedEntities.Any(x => x == target))
                    SetUpRider(target.Value, ent.Owner, ent.Comp);
            }
        }
    }

    private void OnEjectEvent(Entity<VehicleComponent> ent, ref ItemSlotEjectEvent args)
    {
        if (!_gameTiming.IsFirstTimePredicted) return;
        if(args.User == null) return;
        if(_tags.HasTag(args.Item, _vehicleKeyTag))
        {
            if(ent.Comp.Rider == args.User || ent.Comp.Rider == null)
            {
                ent.Comp.hasKeys = false;
                if(ent.Comp.Rider != null)
                {
                    UpdateActions(ent.Comp.Rider.Value, false);
                    if(TryComp<InputMoverComponent>(ent.Comp.Rider.Value, out var imComp) && imComp.CanMove)
                        _actionBlocker.UpdateCanMove(ent.Comp.Rider.Value);

                    for (var i = 0; i < ent.Comp.HandsNeeded; i++)
                    {
                        _virtualItem.DeleteInHandsMatching(ent.Comp.Rider.Value, ent.Owner);
                    }
                }

                TurnOffVehicle(ent.Owner, ent.Comp);
                _handsSystem.PickupOrDrop(args.User, args.Item);
                Dirty(ent.Owner, ent.Comp);
            }
            else
            {
                args.Cancelled = true;
                _popup.PopupEntity(Loc.GetString("vehicle-steal-keys-attempt"), ent.Owner, PopupType.LargeCaution);
                var ev = new EjectKeysDoAfter();
                var doAfter = new DoAfterArgs(EntityManager, args.User.Value, ent.Comp.timeToStealKeys, ev, ent.Owner, ent.Owner)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                    CancelDuplicate = false

                };
                _adminLogger.Add(Shared.Database.LogType.Action, Shared.Database.LogImpact.Medium, $"{ToPrettyString(args.User.Value)} began to attempt to steal keys from {ToPrettyString(ent.Owner)}");
                _doAfter.TryStartDoAfter(doAfter);
            }
        }
    }

    private void OnEjectKeysDoAfter(Entity<VehicleComponent> ent, ref EjectKeysDoAfter args)
    {
        if(args.Cancelled) return;
        if(TryComp<ContainerManagerComponent>(ent.Owner, out var container))
        {
            var key = container.Containers.Values.SelectMany(c => c.ContainedEntities).FirstOrDefault(e => _tags.HasTag(e, _vehicleKeyTag));
            _handsSystem.PickupOrDrop(args.User, key);
            ent.Comp.hasKeys = false;
            Dirty(ent.Owner, ent.Comp);

            TurnOffVehicle(ent.Owner, ent.Comp);
            if(ent.Comp.Rider == null) return;

            UpdateActions(ent.Comp.Rider.Value, false);
            if(TryComp<InputMoverComponent>(ent.Comp.Rider.Value, out var imComp) && imComp.CanMove)
                _actionBlocker.UpdateCanMove(ent.Comp.Rider.Value);

            for (var i = 0; i < ent.Comp.HandsNeeded; i++)
            {
                _virtualItem.DeleteInHandsMatching(ent.Comp.Rider.Value, ent.Owner);
            }
        }
    }

    protected override void OnTurnKeysEvent(Entity<VehicleComponent> ent, ref TurnKeysEvent args)
    {
        if(args.Handled) return;
        if(ent.Comp.Rider == null) return;
        if(!TryComp<MovementSpeedModifierComponent>(ent.Owner, out var msmComp) || msmComp.BaseSprintSpeed <= 0)
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("vehicle-turn-keys-fail"), ent.Owner, PopupType.SmallCaution);
            return;
        }
        if(!ent.Comp.Started)
        {
            _popup.PopupEntity(Loc.GetString("vehicle-turn-keys-start"), ent.Owner, PopupType.Medium);
            _adminLogger.Add(Shared.Database.LogType.Action, Shared.Database.LogImpact.Low, $"{ToPrettyString(ent.Comp.Rider.Value)} started the engine of {ToPrettyString(ent.Owner)}");

        }
        if(ent.Comp.Started)
        {
            _popup.PopupEntity(Loc.GetString("vehicle-turn-keys-stop"), ent.Owner, PopupType.Medium);
            _adminLogger.Add(Shared.Database.LogType.Action, Shared.Database.LogImpact.Low, $"{ToPrettyString(ent.Comp.Rider.Value)} stopped the engine of {ToPrettyString(ent.Owner)}");
        }
        var ev = new TurnKeysDoAfter();
        var doAfter = new DoAfterArgs(EntityManager, ent.Comp.Rider.Value, ent.Comp.startupTime, ev, ent.Owner)
        {
            BreakOnMove = true
        };
        _doAfter.TryStartDoAfter(doAfter);
        args.Handled = true;
    }

    private void OnTurnKeysDoAfter(Entity<VehicleComponent> ent, ref TurnKeysDoAfter args)
    {
        if(args.Cancelled) return;
        if(ent.Comp.Rider == null) return;

        if(!ent.Comp.Started)
        {
            if((ent.Comp.CellPowered && HasComp<PowerCellDrawComponent>(ent.Owner) && !_powerCell.HasDrawCharge(ent.Owner))
            || (!ent.Comp.CellPowered && HasComp<ReagantDrawComponent>(ent.Owner) && !_reagantDraw.HasDrawReagant(ent.Owner)))
                return;

            for (var i = 0; i < ent.Comp.HandsNeeded; i++)
            {
                if (_virtualItem.TrySpawnVirtualItem(ent.Owner, ent.Comp.Rider.Value, out var virtItem))
                {
                    EnsureComp<UnremoveableComponent>(virtItem.Value);
                    _handsSystem.TryForcePickupAnyHand(ent.Comp.Rider.Value, virtItem.Value);
                }
            }
        }

        if(ent.Comp.Started)
        {
            for (var i = 0; i < ent.Comp.HandsNeeded; i++)
            {
                _virtualItem.DeleteInHandsMatching(ent.Comp.Rider.Value, ent.Owner);
            }
        }

        ent.Comp.Started = !ent.Comp.Started;
        if(ent.Comp.CellPowered && TryComp<PowerCellDrawComponent>(ent.Owner, out var pcdComp))
        {
            pcdComp.Enabled = ent.Comp.Started;
        }
        if(!ent.Comp.CellPowered && TryComp<ReagantDrawComponent>(ent.Owner, out var rdComp))
        {
            rdComp.Enabled = ent.Comp.Started;
            Dirty(ent.Owner, rdComp);
            _ambientSound.SetAmbience(ent.Owner, ent.Comp.Started);
        }

        _actionBlocker.UpdateCanMove(ent.Comp.Rider.Value);

        Dirty(ent.Owner, ent.Comp);
    }

    protected override void HandleCollide(Entity<VehicleComponent> ent, ref StartCollideEvent args)
    {
        if(ent.Comp.Rider == null) return;
        var rider = ent.Comp.Rider.Value;

        if(!ent.Comp.AllowCrashing) return;
        if(!TryComp<MovementSpeedModifierComponent>(ent.Owner, out var msmComp)) return;

        var speed = args.OurBody.LinearVelocity.Length();
        if (speed < ent.Comp.CrashingSpeed) return;

        if (args.OurFixture.Hard && args.OtherFixture.Hard)
        {
            if(TryComp<VehicleBuckleComponent>(ent.Owner, out var vbComp) && TryComp<BuckleComponent>(rider, out var buckleComp))
            {
                if(TryComp<PhysicsComponent>(ent.Owner, out var vehiclePhys) && TryComp<PhysicsComponent>(rider, out var riderPhys))
                    if(_buckle.TryUnbuckle(rider, null, buckleComp) && vbComp.EjectOnCrash)
                    {
                        var riderXform = Transform(rider);
                        _stun.TryCrawling(rider, TimeSpan.FromSeconds(3));
                        _throwing.TryThrow(rider, vehiclePhys.LinearVelocity, riderPhys, riderXform, _projQuery, vehiclePhys.LinearVelocity.Length(), playSound: false);
                        _adminLogger.Add(Shared.Database.LogType.Landed, Shared.Database.LogImpact.Medium, $"{ToPrettyString(rider)} was launched from vehicle {ToPrettyString(ent.Owner)}");
                    }
            }
            else if(TryComp<VehicleContainerComponent>(ent.Owner, out var vcComp))
            {
                foreach(var passenger in vcComp.PassengerSlot.ContainedEntities)
                {
                    _stun.TryAddStunDuration(passenger, TimeSpan.FromSeconds(3));
                    _adminLogger.Add(Shared.Database.LogType.Landed, Shared.Database.LogImpact.Medium, $"{ToPrettyString(passenger)} was stunned inside of vehicle {ToPrettyString(ent.Owner)}");
                }
            }
        }
        else if(args.OurFixture.Hard && !args.OtherFixture.Hard)
        {
            if(!HasComp<DamageableComponent>(args.OtherEntity) || HasComp<PacifiedComponent>(ent.Owner)) return;

            DamageTypePrototype? _blunt = _prototypes.Index<DamageTypePrototype>(_bluntname);
            DamageSpecifier? _damage = new(_blunt, Math.Clamp(10 * (1 + (0.5 * speed / crashingSpeed)), 10, 20));
            _damageable.TryChangeDamage(args.OtherEntity, _damage, origin: ent.Comp.Rider.Value);
            _color.RaiseEffect(Color.Red, new List<EntityUid>() { args.OtherEntity, }, Filter.Pvs(args.OtherEntity, entityManager: EntityManager));

            if(!TryComp<MovementSpeedModifierComponent>(ent.Owner, out var msmComp)) return;

            Timer.Spawn(TimeSpan.FromSeconds(2), () => _movementSpeed.ChangeBaseSpeed(ent.Owner, msmComp.BaseWalkSpeed * 4, msmComp.BaseSprintSpeed * 4, msmComp.Acceleration));
            _movementSpeed.ChangeBaseSpeed(ent.Owner, msmComp.BaseWalkSpeed/4, msmComp.BaseSprintSpeed/4, msmComp.Acceleration);
            _adminLogger.Add(Shared.Database.LogType.Landed, Shared.Database.LogImpact.High, $"{ToPrettyString(ent.Comp.Rider.Value)} ran over {ToPrettyString(args.OtherEntity)} dealing a total of {_damage.DamageDict}");
        }
    }

    private void OnGetAdditionalAccess(Entity<VehicleComponent> ent, ref GetAdditionalAccessEvent args)
    {
        if (ent.Comp.Rider == null) return;

        args.Entities.Add(ent.Comp.Rider.Value);
    }

    private void OnEmptyReagantContainer(Entity<VehicleComponent> ent, ref ReagantContainerSlotEmptyEvent args)
    {
        if(!ent.Comp.CellPowered)
            TurnOffVehicle(ent.Owner, ent.Comp);
    }

    private void OnPowerCellEmpty(Entity<VehicleComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        if(ent.Comp.CellPowered)
            TurnOffVehicle(ent.Owner, ent.Comp);

        if(ent.Comp.Rider != null)
            if(TryComp<InputMoverComponent>(ent.Comp.Rider.Value, out var imComp) && imComp.CanMove)
                _actionBlocker.UpdateCanMove(ent.Comp.Rider.Value);
    }

    private void OnRepairFinished(Entity<VehicleComponent> ent, ref RepairFinishedEvent args)
    {
        _adminLogger.Add(Shared.Database.LogType.Action, Shared.Database.LogImpact.Low, $"{ToPrettyString(args.User)} repaired the vehicle {ToPrettyString(ent.Owner)}");
        ent.Comp.isBroken = false;

        if(TryComp<VehicleBuckleComponent>(ent, out var vbComp))
        {
            _buckle.StrapSetEnabled(ent, true);
        }
        TryUpdateVisualState(ent.Owner);
        Dirty(ent.Owner, ent.Comp);
    }

    protected override void OnToggleTrunk(Entity<VehicleComponent> ent, ref ToggleTrunkActionEvent args)
    {
        if(args.Handled) return;
        if(!TryComp<LockComponent>(ent.Owner, out var lockComp)) return;
        _adminLogger.Add(Shared.Database.LogType.Action, Shared.Database.LogImpact.Low, $"{ToPrettyString(args.Performer)} toggled the trunk from {ToPrettyString(ent.Owner)}");
        _lock.ToggleLock(ent.Owner, args.Performer, lockComp);

        if(!_lock.IsLocked(ent.Owner))
            _popup.PopupEntity(Loc.GetString("vehicle-toggle-trunk-open"), ent.Owner, PopupType.Small);
        else
            _popup.PopupEntity(Loc.GetString("vehicle-toggle-trunk-close"), ent.Owner, PopupType.Small);
        args.Handled = true;
    }

    private void OnDamageChanged(EntityUid ent, VehicleComponent component, DamageChangedEvent args)
    {
        if(!args.DamageIncreased || args.DamageDelta == null) return;
        if (TryComp<VehicleContainerComponent>(ent, out var vcComp)
            && vcComp.PassengerSlot.ContainedEntities.Count != 0)
        {
            var damage = args.DamageDelta * vcComp.DamageTransferMultiplier;
            foreach(var passenger in vcComp.PassengerSlot.ContainedEntities)
            {
                _damageable.TryChangeDamage(passenger, damage / vcComp.PassengerSlot.ContainedEntities.Count, origin: args.Origin);
            }
        }
        else if(HasComp<VehicleBuckleComponent>(ent) && component.Rider != null)
        {
            _damageable.TryChangeDamage(component.Rider.Value, args.DamageDelta, origin: args.Origin);
            _color.RaiseEffect(Color.Red, new List<EntityUid>() { component.Rider.Value, }, Filter.Pvs(component.Rider.Value, entityManager: EntityManager));
        }
    }

    private void OnEmpPulse(Entity<VehicleComponent> ent, ref EmpPulseEvent args) => TurnOffVehicle(ent.Owner, ent.Comp);

    private void OnBreakageEvent(EntityUid ent, VehicleComponent component, BreakageEventArgs args)
    {
        if(TryComp<VehicleContainerComponent>(ent, out var vcComp))
        {
            if(vcComp.PassengerSlot.ContainedEntities.Count != 0)
            {
                foreach(var passengers in vcComp.PassengerSlot.ContainedEntities.ToArray())
                {
                    RemoveRider(passengers, ent, component);
                }
            }
        }
        if(TryComp<VehicleBuckleComponent>(ent, out var vbComp))
        {
            _buckle.StrapSetEnabled(ent, false);
        }

        component.isBroken = true;

        TryUpdateVisualState(ent);

        TurnOffVehicle(ent, component);
    }

    #endregion
    #region VehicleBuckle Events
    private void OnStrapped(Entity<VehicleBuckleComponent> ent, ref StrappedEvent args)
    {
        if(!TryComp<VehicleComponent>(ent, out var vehicleComp)) return;
        SetUpRider(args.Buckle.Owner, ent.Owner, vehicleComp);
        foreach(var held in _handsSystem.EnumerateHeld(args.Buckle.Owner))
        {
            if(TryComp<WieldableComponent>(held, out var wieldComp))
                _wield.TryUnwield(held, wieldComp, args.Buckle.Owner);
        }
        Timer.Spawn(0, () => _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner)); // Race conditions :strangle:
    }

    private void OnUnstrapAttempt(Entity<VehicleBuckleComponent> ent, ref UnstrapAttemptEvent args)
    {
        if(!TryComp<VehicleComponent>(ent.Owner, out var vehicleComp)) return;
        if(args.User == null || !args.Popup) return;
        if(vehicleComp.Rider == null) return;
        if (vehicleComp.Rider != args.User)
        {
            args.Cancelled = true;
            _adminLogger.Add(Shared.Database.LogType.Action, Shared.Database.LogImpact.Low, $"{ToPrettyString(args.User)} attempted to steal vehicle {ToPrettyString(ent.Owner)}");
            _popup.PopupEntity(Loc.GetString("vehicle-steal-vehicle-attempt"), vehicleComp.Rider.Value, PopupType.LargeCaution);
            var ev = new VehicleUnbuckleDoAfter();
            var doAfter = new DoAfterArgs(EntityManager, args.User.Value, ent.Comp.duration, ev, ent.Owner, ent.Owner)
            {
                BreakOnMove = true,
                BreakOnDamage = true
            };
            _doAfter.TryStartDoAfter(doAfter);
        }
    }

    private void OnUnstrapped(Entity<VehicleBuckleComponent> ent, ref UnstrappedEvent args)
    {
        if(!TryComp<VehicleComponent>(ent, out var vehicleComp)) return;

        if(HasComp<RiderComponent>(args.Buckle.Owner))
            RemoveRider(args.Buckle.Owner, ent.Owner, vehicleComp);
    }

    private void OnUnbuckleDoAfter(Entity<VehicleBuckleComponent> ent, ref VehicleUnbuckleDoAfter args)
    {
        if(args.Cancelled) return;
        if(!TryComp<VehicleComponent>(ent.Owner, out var vehicleComp)) return;
        if(vehicleComp.Rider == null) return;
        var user = vehicleComp.Rider.Value;
        if(!TryComp<BuckleComponent>(user, out var buckleComp)) return;
        _buckle.Unbuckle((user, buckleComp), user);
    }

    private void OnMovementSpeedRefreshVehicleEvent(Entity<VehicleBuckleComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if(!ent.Comp.armoraffectsvehicle) return;
        if(!TryComp<VehicleComponent>(ent.Owner, out var vehicleComp) || vehicleComp.Rider == null) return;
        if(!TryComp<MovementSpeedModifierComponent>(vehicleComp.Rider.Value, out var msmComp)) return;
        args.ModifySpeed(msmComp.WalkSpeedModifier, msmComp.SprintSpeedModifier);

    }

    #endregion
    #region VehicleContainer Events

    private void OnAlternativeVerb(EntityUid uid, VehicleContainerComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        if(!TryComp<VehicleComponent>(uid, out var vehicleComp)) return;
        if(TryComp<DestructibleComponent>(uid, out var destructibleComp) && destructibleComp.IsBroken) return;

        if (CanInsert(uid, component) && !component.PassengerSlot.ContainedEntities.Contains(args.User))
        {
            var enterVerb = new AlternativeVerb
            {
                Text = Loc.GetString("vehicle-verb-enter"),
                Act = () =>
                {
                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.EntryTime, new VehicleEntryDoAfter(), uid, target: args.User)
                    {
                        BreakOnMove = true,
                    };

                    _doAfter.TryStartDoAfter(doAfterEventArgs);
                }
            };
            args.Verbs.Add(enterVerb);
        }
        else if(component.PassengerSlot.ContainedEntities.Contains(args.User))
        {
            var exitVerb = new AlternativeVerb
            {
                Text = Loc.GetString("vehicle-verb-leave"),
                Act = () =>
                {
                    TryRemove(args.User, uid, component);
                    if(HasComp<RiderComponent>(args.User))
                        RemoveRider(args.User, uid, vehicleComp);
                }
            };
            args.Verbs.Add(exitVerb);
        }

        if(component.PassengerSlot.ContainedEntities.Count != 0 && !component.PassengerSlot.ContainedEntities.Contains(args.User))
        {
            var removeVerb = new AlternativeVerb
            {
                Text = Loc.GetString("vehicle-verb-remove"),
                Act = () =>
                {
                    _popup.PopupEntity(Loc.GetString("vehicle-remove-passenger-attempt"), uid, PopupType.LargeCaution);
                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.RemoveTime, new VehicleRemoveDoAfter(), uid, target: uid)
                    {
                        BreakOnMove = true,
                    };
                    _adminLogger.Add(Shared.Database.LogType.Verb, Shared.Database.LogImpact.Medium, $"{ToPrettyString(args.User)} attempted to remove a passenger from {ToPrettyString(uid)}");

                    _doAfter.TryStartDoAfter(doAfterEventArgs);
                }
            };
            args.Verbs.Add(removeVerb);
        }
    }

    private void OnDragDrop(Entity<VehicleContainerComponent> ent, ref DragDropTargetEvent args)
    {
        if(args.Handled) return;
        args.Handled = true;
        if(TryComp<DestructibleComponent>(ent.Owner, out var destructibleComp) && destructibleComp.IsBroken) return;

        if(!CanInsert(ent.Owner, ent.Comp)) return;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, ent.Comp.EntryTime, new VehicleEntryDoAfter(), ent.Owner, target: args.Dragged)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnVehicleEntryDoAfter(Entity<VehicleContainerComponent> ent, ref VehicleEntryDoAfter args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if(!TryComp<VehicleComponent>(ent, out var vehicleComp)) return;
        if(!TryInsert(args.Args.Target, ent.Owner, ent.Comp)) return;

        SetUpRider(args.Args.Target!.Value, ent.Owner, vehicleComp);

        args.Handled = true;
    }

    private void OnVehicleRemoveDoAfter(Entity<VehicleContainerComponent> ent, ref VehicleRemoveDoAfter args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if(!TryComp<VehicleComponent>(ent, out var vehicleComp)) return;
        RemoveRider(ent.Comp.PassengerSlot.ContainedEntities.First(), ent.Owner, vehicleComp);

        if(!TryRemove(ent.Comp.PassengerSlot.ContainedEntities.First(), ent.Owner, ent.Comp)) return;

        args.Handled = true;
    }

    private void OnDamageChanged(EntityUid ent, VehicleContainerComponent component, DamageChangedEvent args)
    {
        if (args.DamageIncreased &&
            args.DamageDelta != null &&
            component.PassengerSlot.ContainedEntities.Count != 0)
        {
            var damage = args.DamageDelta * component.DamageTransferMultiplier;
            foreach(var passenger in component.PassengerSlot.ContainedEntities)
            {
                _damageable.TryChangeDamage(passenger, damage / component.PassengerSlot.ContainedEntities.Count);
            }
        }
    }

    private void OnEntInserted(EntityUid ent, VehicleContainerComponent component, EntInsertedIntoContainerMessage args)
    {
        if(args.Container != component.PassengerSlot) return;

        if(_whitelist.IsWhitelistFail(component.PassengerWhitelist, args.Entity))
        {
            if(HasComp<RiderComponent>(args.Entity) && TryComp<VehicleComponent>(ent, out var vehicleComp))
                RemoveRider(args.Entity, ent, vehicleComp);
            _container.Remove(args.Entity, component.PassengerSlot);
        }
    }

    #endregion
    #region Rider Events

    private void OnStunned(Entity<RiderComponent> ent, ref StunnedEvent args)
    {
        var vehicle = ent.Comp.Riding;
        if(!TryComp<VehicleBuckleComponent>(vehicle, out var vehicleBuckleComp)) return;
        if(!vehicleBuckleComp.stundismount) return;
        if(!TryComp<BuckleComponent>(ent.Owner, out var buckleComp)) return;

        _buckle.Unbuckle((ent.Owner, buckleComp), ent.Owner);
    }

    private void OnKnockdown(Entity<RiderComponent> ent, ref KnockedDownEvent args)
    {
        var vehicle = ent.Comp.Riding;
        if(!TryComp<VehicleBuckleComponent>(vehicle, out var vehicleBuckleComp)) return;
        if(!vehicleBuckleComp.knockdowndismount) return;
        if(!TryComp<BuckleComponent>(ent.Owner, out var buckleComp)) return;

        _buckle.Unbuckle((ent.Owner, buckleComp), ent.Owner);
    }

    private void OnJumpActionEvent(Entity<RiderComponent> ent, ref JumpActionEvent args)
    {
        if(!TryComp<BuckleComponent>(ent.Owner, out var buckleComp)) return;
        _buckle.Unbuckle((ent.Owner, buckleComp), ent.Owner);
    }

    private void OnUpdateCanMoveEvent(Entity<RiderComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (!TryComp<VehicleComponent>(ent.Comp.Riding, out var vehicleComp))
            return;

        if (vehicleComp.RequireIgnition && !vehicleComp.Started)
        {
            args.Cancel();
            return;
        }

        if (ent.Comp.Riding == null)
            return;

        var riding = ent.Comp.Riding.Value;

        TryComp<PowerCellDrawComponent>(riding, out var pcdComp);
        TryComp<ReagantDrawComponent>(riding, out var rdComp);

        var noPower =
            (pcdComp != null && !_powerCell.HasDrawCharge(riding)) ||
            (rdComp != null && !_reagantDraw.HasDrawReagant(riding));

        if (!noPower) return;

        if (vehicleComp.Started)
            vehicleComp.Started = false;

        if (vehicleComp.CellPowered && pcdComp?.Enabled == vehicleComp.Started)
        {
            pcdComp.Enabled = false;
            Dirty(riding, pcdComp);
        }

        if (!vehicleComp.CellPowered && rdComp?.Enabled == true)
        {
            rdComp.Enabled = vehicleComp.Started;
            _ambientSound.SetAmbience(riding, false);
            Dirty(riding, rdComp);
        }

        Dirty(riding, vehicleComp);
        args.Cancel();
    }

    private void OnWieldAttemptEvent(Entity<RiderComponent> ent, ref WieldAttemptEvent args)
    {
        if(ent.Comp.Riding != null && TryComp<VehicleComponent>(ent.Comp.Riding.Value, out var vehicleComp) && !vehicleComp.DisallowWieldingGuns) return;

        args.Cancel();
    }

    private void OnShooterEvent(Entity<RiderComponent> ent, ref ShooterImpulseEvent args)
    {
        if(!TryComp<StaminaComponent>(ent.Owner, out var stamina)) return;
        if(ent.Comp.Riding != null && TryComp<VehicleComponent>(ent.Comp.Riding.Value, out var vehicleComp) && !vehicleComp.AllowGunKnockback) return;

        foreach(var held in _handsSystem.EnumerateHeld(ent.Owner))
        {
            if(HasComp<GunComponent>(held) && HasComp<WieldableComponent>(held))
            {
                _stamina.TakeStaminaDamage(ent.Owner, stamina.CritThreshold*0.10f, component: stamina);
            }
        }
    }

    private void OnMovementSpeedRefreshRiderEvent(Entity<RiderComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if(ent.Comp.Riding == null) return;
        Timer.Spawn(0, () => _movementSpeed.RefreshMovementSpeedModifiers(ent.Comp.Riding.Value)); // Race conditions :strangle:
    }

    private void OnHandEquippedRider(Entity<RiderComponent> ent, ref DidEquipHandEvent args)
    {
        if(!HasComp<GunComponent>(args.Equipped)) return;
        _gun.RefreshModifiers(args.Equipped);
    }

    #endregion
    #region Gun Events
    private void OnGunUnwielded(EntityUid uid, GunComponent component, ItemUnwieldedEvent args)
    {
        if(HasComp<RiderComponent>(args.User))
            _gun.RefreshModifiers(uid);
    }

    private void OnGunWielded(EntityUid uid, GunComponent component, ref ItemWieldedEvent args)
    {
        if(HasComp<RiderComponent>(args.User))
            _gun.RefreshModifiers(uid);
    }

    private void OnGunRefreshModifiers(Entity<GunComponent> ent, ref GunRefreshModifiersEvent args)
    {
        var transform = Transform(ent.Owner);
        if(!TryComp<RiderComponent>(transform.ParentUid, out var riderComp)) return;
        if(riderComp.Riding == null) return;
        if(HasComp<PowerCellDrawComponent>(riderComp.Riding.Value)
            ^ HasComp<ReagantDrawComponent>(riderComp.Riding.Value))
        {
            if(HasComp<VehicleContainerComponent>(riderComp.Riding.Value))
            {
                args.MinAngle += Angle.FromDegrees(30);
                args.MaxAngle += Angle.FromDegrees(30);
            }
            else if(HasComp<VehicleBuckleComponent>(riderComp.Riding.Value))
            {
                args.MinAngle += Angle.FromDegrees(20);
                args.MaxAngle += Angle.FromDegrees(20);
            }
        }
    }

    #endregion
    #region Misc Events

    private void OnJetJumpActionEvent(Entity<TransformComponent> ent, ref JetJumpActionEvent args)
    {
        if(!TryComp<BuckleComponent>(ent.Comp.ParentUid, out var buckleComp)) return;
        _buckle.Unbuckle((ent.Comp.ParentUid, buckleComp), ent.Comp.ParentUid);
    }

    //The pickable races check...
    private void OnHandEquipped(DidEquipHandEvent ev)
    {
        if(!TryComp<RiderComponent>(ev.Equipped, out var riderComp)
            || riderComp.Riding != null
            || !TryComp<VehicleComponent>(riderComp.Riding, out var vehicleComp)) return;
        RemoveRider(ev.Equipped, riderComp.Riding.Value, vehicleComp);
    }

    private void OnMoveEvent(ref MoveEvent ev)
    {
        var vehicle = ev.Entity.Owner;
        if(!TryComp<VehicleComponent>(vehicle, out var vehicleComp)) return;
        if(!TryComp<PhysicsComponent>(vehicle, out var vehiclePhys)) return;

        var speed = vehiclePhys.LinearVelocity.Length();
        if(speed > 0.3)
            vehicleComp.isMoving = true;
        else if(speed < 0.3)
            vehicleComp.isMoving = false;

        TryUpdateVisualState(vehicle);
        Dirty(vehicle, vehicleComp);

        if( vehicleComp.Rider == null) return;
        var rider = vehicleComp.Rider.Value;
        var riderTransform = Transform(rider);
        if(riderTransform.ParentUid !=  vehicle) return;

        if(HasComp<VehicleBuckleComponent>(vehicle) && TryComp<StrapComponent>(vehicle, out var strapComp))
        {
            if(riderTransform.LocalPosition.X != 0+strapComp.BuckleOffset.X || riderTransform.LocalPosition.Y != 0+strapComp.BuckleOffset.Y)
                _transform.SetLocalPosition(rider, new Vector2(0f+strapComp.BuckleOffset.X, 0f+strapComp.BuckleOffset.Y), riderTransform);
            if(riderTransform.LocalRotation != 0)
                _transform.SetLocalRotation(rider, 0f, riderTransform);
        }
    }

    #endregion
    #region Functions
    private void SetUpRider(EntityUid rider, EntityUid vehicle, VehicleComponent vehicleComp)
    {
        var riderComp = EnsureComp<RiderComponent>(rider);
        riderComp.Riding = vehicle;
        Dirty(rider, riderComp);
        _adminLogger.Add(Shared.Database.LogType.Action, Shared.Database.LogImpact.Low, $"{ToPrettyString(rider)} entered vehicle {ToPrettyString(vehicle)}");
        if(TryComp<InputMoverComponent>(rider, out var imComp) && imComp.CanMove)
            _actionBlocker.UpdateCanMove(rider);

        foreach(var item in _handsSystem.EnumerateHeld(rider))
        {
            if(HasComp<GunComponent>(item))
                _gun.RefreshModifiers(item);
        }

        if(_whitelist.IsWhitelistFail(vehicleComp.RiderWhitelist, rider)) return;
        if(!vehicleComp.hasKeys && vehicleComp.RequireIgnition) return;

        _actions.GrantContainedActions(rider, vehicle);
        UpdateActions(rider, true);

        _mover.SetRelay(rider, vehicle);
        vehicleComp.Rider = rider;
        Dirty(vehicle, vehicleComp);

        AddActions(vehicleComp.Rider.Value, vehicle, vehicleComp);
        if(vehicleComp.Started)
        {
            for (var i = 0; i < vehicleComp.HandsNeeded; i++)
            {
                if (_virtualItem.TrySpawnVirtualItem(vehicle, rider, out var virtItem))
                {
                    EnsureComp<UnremoveableComponent>(virtItem.Value);
                    Timer.Spawn(0, () => _handsSystem.TryForcePickupAnyHand(rider, virtItem.Value, checkActionBlocker: false));
                }
            }
        }
    }

    private void UpdateActions(EntityUid rider, bool gettingOn)
    {
        if(!TryComp<RiderComponent>(rider, out var riderComp) || riderComp.Riding == null) return;
        var vehicle = riderComp.Riding.Value;

        if(!TryComp<VehicleActionsComponent>(vehicle, out var vaComp)
            || !TryComp<VehicleComponent>(vehicle, out var vehicleComp)) return;

        if(gettingOn)
        {
            if(vaComp.TurnKeysActionEntity == null && vehicleComp.hasKeys)
                _actions.AddAction(rider, ref vaComp.TurnKeysActionEntity, vaComp.TurnKeysAction, vehicle);
            if(HasComp<LockComponent>(vehicle) && vaComp.ToggleTrunkActionEntity == null && vehicleComp.hasKeys)
                _actions.AddAction(rider, ref vaComp.ToggleTrunkActionEntity, vaComp.ToggleTrunkAction, vehicle);
            if(vehicleComp.HornSound != null && vaComp.HornVehicleActionEntity == null)
                _actions.AddAction(rider, ref vaComp.HornVehicleActionEntity, vaComp.HornVehicleAction, vehicle);

            var addingActions = new AddRiderActions(rider);
            RaiseLocalEvent(rider, ref addingActions);
        }
        else if(!gettingOn)
        {
            if(vaComp.TurnKeysActionEntity != null && !vehicleComp.hasKeys)
            {
                _actions.RemoveAction(rider, vaComp.TurnKeysActionEntity);
                QueueDel(vaComp.TurnKeysActionEntity);
                vaComp.TurnKeysActionEntity = null;
            }
            if(HasComp<LockComponent>(vehicle) && vaComp.ToggleTrunkActionEntity != null && !vehicleComp.hasKeys)
            {
                _actions.RemoveAction(rider, vaComp.ToggleTrunkActionEntity);
                QueueDel(vaComp.ToggleTrunkActionEntity);
                vaComp.ToggleTrunkActionEntity = null;
            }
            if(vehicleComp.HornSound != null && vaComp.HornVehicleActionEntity != null)
            {
                _actions.RemoveAction(rider, vaComp.HornVehicleActionEntity);
                QueueDel(vaComp.HornVehicleActionEntity);
                vaComp.HornVehicleActionEntity = null;
            }

            var removingActions = new RemoveRiderActions(rider);
            RaiseLocalEvent(rider, ref removingActions);
        }

        Dirty(vehicle, vehicleComp);
    }

    private void RemoveRider(EntityUid rider, EntityUid vehicle, VehicleComponent vehicleComp)
    {
        _adminLogger.Add(Shared.Database.LogType.Action, Shared.Database.LogImpact.Low, $"{ToPrettyString(rider)} exited vehicle {ToPrettyString(vehicle)}");
        foreach(var item in _handsSystem.EnumerateHeld(rider))
        {
            if(HasComp<GunComponent>(item))
                _gun.RefreshModifiers(item);
        }

        if(rider != vehicleComp.Rider) return;
        vehicleComp.Rider = null;
        _actions.RemoveProvidedActions(rider, vehicle);
        if(vehicleComp.Headlight != null)
            _actions.RemoveProvidedActions(rider, vehicleComp.Headlight.Value);
        if(vehicleComp.Sirenlight != null)
            _actions.RemoveProvidedActions(rider, vehicleComp.Sirenlight.Value);

        if(_container.TryGetContainer(vehicle, vehicleComp.VehicleModsSlot, out var items))
            foreach(var item in items.ContainedEntities)
                _actions.RemoveProvidedActions(rider, item);

        for (var i = 0; i < vehicleComp.HandsNeeded; i++)
        {
            _virtualItem.DeleteInHandsMatching(rider, vehicle);
        }

        Dirty(vehicle, vehicleComp);
    }
    private void AddActions(EntityUid rider, EntityUid vehicle, VehicleComponent? component=null)
    {
        if (!Resolve(vehicle, ref component))
            return;

        if(component.isBroken) return;

        if(component.requireIgnition && TryComp(vehicle, out ItemSlotsComponent? itemComp)
                && itemComp.Slots.Values.Any(slot =>
                    slot.ContainerSlot?.ContainedEntity is EntityUid item
                    && _tags.HasTag(item, _vehicleKeyTag)))
        {
            _actions.AddAction(rider, ref component.TurnKeysActionEntity, component.TurnKeysAction, vehicle);
            if(HasComp<LockComponent>(vehicle))
                _actions.AddAction(rider, ref component.ToggleTrunkActionEntity, component.ToggleTrunkAction, vehicle);
        }

        if(component.HornSound != null)
            _actions.AddAction(rider, ref component.HornVehicleActionEntity, component.HornVehicleAction, vehicle);

        if(component.Headlight != null && TryComp<UnpoweredFlashlightComponent>(component.Headlight, out var headLight))
            _actions.AddAction(rider, ref headLight.ToggleActionEntity, headLight.ToggleAction, component.Headlight.Value);

        if(component.Sirenlight != null && HasComp<PointLightComponent>(component.Sirenlight))
            _actions.AddAction(rider, component.ToggleSirenAction, component.Sirenlight.Value);

        Dirty(vehicle, vehicleComp);
    }

    private bool TryInsert(EntityUid? Rider, EntityUid Vehicle, VehicleContainerComponent? component=null)
    {
        if(!Resolve(Vehicle, ref component))
            return false;

        if(Rider == null)
            return false;

        if (!CanInsert(Vehicle, component))
            return false;

        _container.Insert(Rider.Value, component.PassengerSlot);
        Dirty(Rider.Value, component);
        return true;
    }

    public bool CanInsert(EntityUid uid, VehicleContainerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        return component.PassengerSlot.ContainedEntities.Count() < component.Seats;
    }

    private bool TryRemove(EntityUid? Rider, EntityUid Vehicle, VehicleContainerComponent? component=null)
    {
        if(!Resolve(Vehicle, ref component))
            return false;

        if(Rider == null)
            return false;

        _container.Remove(Rider.Value, component.PassengerSlot);
        Dirty(Rider.Value, component);
        return true;
    }

    private void TurnOffVehicle(EntityUid vehicle, VehicleComponent? component=null)
    {
        if(!Resolve(vehicle, ref component))
            return;

        var ev = new TurnOffVehicleEvent();
        RaiseLocalEvent(vehicle, TurnOffVehicle);

        if(component.Started)
            component.Started = false;

        if(component.CellPowered && TryComp<PowerCellDrawComponent>(vehicle, out var pcdComp) && pcdComp.Enabled)
        {
            pcdComp.Enabled = false;
            Dirty(vehicle, pcdComp);
        }
        if(!component.CellPowered && TryComp<ReagantDrawComponent>(vehicle, out var rdComp) && rdComp.Enabled)
        {
            rdComp.Enabled = false;
            _ambientSound.SetAmbience(vehicle, rdComp.Enabled);
            Dirty(vehicle, rdComp);
        }

        if(component.Rider != null)
            if(TryComp<InputMoverComponent>(component.Rider.Value, out var imComp) && imComp.CanMove)
                _actionBlocker.UpdateCanMove(component.Rider.Value);

        Dirty(vehicle, component);
    }
    #endregion
}
