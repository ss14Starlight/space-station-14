using Content.Shared._FarHorizons.Vehicles.Components;
using Robust.Shared.Audio.Systems;
using Content.Shared.DragDrop;
using Content.Shared.Lock;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;
using Robust.Shared.Audio;
using Content.Shared.Examine;
using Content.Shared.Damage.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Buckle;
using Content.Shared.Movement.Components;
using Content.Shared._FarHorizons.Vehicles.Events;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Access.Components;
using Content.Shared.Actions;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Containers.ItemSlots;
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
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared._FarHorizons.ReagentDraw.Components;
using Content.Shared.Audio;
using Content.Shared.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Containers;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Interaction.Components;
using Content.Shared.Destructible;
using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Repairable;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Effects;
using Robust.Shared.Player;
using Content.Shared.Administration.Logs;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Hands;
using Content.Shared._FarHorizons.ReagentDraw.EntitySystems;

namespace Content.Shared._FarHorizons.Vehicles;

public abstract partial class SharedVehicleSystem : EntitySystem
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
    [Dependency] private readonly SharedReagentDrawSystem _reagentDraw = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
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
    [Dependency] private readonly SharedWieldableSystem _wield = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly LockSystem _lock = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    private static readonly ProtoId<TagPrototype> _vehicleKeyTag = "VehicleKey";
    private static readonly string _bluntname = "Blunt";
    private EntityQuery<ProjectileComponent> _projQuery;
    public override void Initialize()
    {
        base.Initialize();

        _projQuery = GetEntityQuery<ProjectileComponent>();

        SubscribeLocalEvent<VehicleComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<VehicleComponent, EntInsertedIntoContainerMessage>(OnEntInsertedVehicle);
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
        SubscribeLocalEvent<VehicleComponent, TurnKeysEvent>(OnTurnKeysEvent);
        SubscribeLocalEvent<VehicleComponent, HornActionEvent>(OnHornActionEvent);
        SubscribeLocalEvent<VehicleComponent, ToggleTrunkActionEvent>(OnToggleTrunk);
        SubscribeLocalEvent<VehicleComponent, StartCollideEvent>(HandleCollide);
        SubscribeLocalEvent<VehicleComponent, CanDropTargetEvent>(OnCanDragDrop);
        SubscribeLocalEvent<VehicleComponent, ExaminedEvent>(OnExamine);

        SubscribeLocalEvent<VehicleBuckleComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<VehicleBuckleComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<VehicleBuckleComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        SubscribeLocalEvent<VehicleBuckleComponent, VehicleUnbuckleDoAfter>(OnUnbuckleDoAfter);
        SubscribeLocalEvent<VehicleBuckleComponent, RefreshMovementSpeedModifiersEvent>(OnMovementSpeedRefreshVehicleEvent);

        SubscribeLocalEvent<VehicleContainerComponent, VehicleEntryDoAfter>(OnVehicleEntryDoAfter);
        SubscribeLocalEvent<VehicleContainerComponent, VehicleRemoveDoAfter>(OnVehicleRemoveDoAfter);
        SubscribeLocalEvent<VehicleContainerComponent, EntInsertedIntoContainerMessage>(OnEntInserted);

        SubscribeLocalEvent<RiderComponent, StunnedEvent>(OnStunned);
        SubscribeLocalEvent<RiderComponent, KnockedDownEvent>(OnKnockdown);
        SubscribeLocalEvent<RiderComponent, UpdateCanMoveEvent>(OnUpdateCanMoveEvent);
        SubscribeLocalEvent<RiderComponent, JumpActionEvent>(OnJumpActionEvent);
        SubscribeLocalEvent<RiderComponent, WieldAttemptEvent>(OnWieldAttemptEvent);
        SubscribeLocalEvent<RiderComponent, ShooterImpulseEvent>(OnShooterEvent);
        SubscribeLocalEvent<RiderComponent, RefreshMovementSpeedModifiersEvent>(OnMovementSpeedRefreshRiderEvent);
        SubscribeLocalEvent<RiderComponent, DidEquipHandEvent>(OnHandEquippedRider);
        SubscribeLocalEvent<RiderComponent, PullAttemptEvent>(OnPullAttempt);

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
        EnsureComp<VehicleActionsComponent>(ent.Owner);
        Dirty(ent);
    }

    private void OnEntInsertedVehicle(Entity<VehicleComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if(args.Container.ID != "key_slot") return;
        ent.Comp.hasKeys = _tags.HasTag(args.Entity, _vehicleKeyTag);
        Dirty(ent);
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
        var user = args.User;
        var item = args.Item;
        if(_tags.HasTag(args.Item, _vehicleKeyTag))
        {
            if(ent.Comp.Rider == user || ent.Comp.Rider == null)
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
                Timer.Spawn(0, () =>_handsSystem.PickupOrDrop(user, item));
                ent.Comp.Rider = null;
                Dirty(ent);
            }
            else
            {
                args.Cancelled = true;
                _popup.PopupClient(Loc.GetString("vehicle-steal-keys-attempt"), ent.Owner, PopupType.LargeCaution);
                var ev = new EjectKeysDoAfter();
                var doAfter = new DoAfterArgs(EntityManager, args.User.Value, ent.Comp.timeToStealKeys, ev, ent.Owner, ent.Owner)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                    CancelDuplicate = false

                };
                _adminLogger.Add(Database.LogType.Action, Database.LogImpact.Medium, $"{ToPrettyString(args.User.Value)} began to attempt to steal keys from {ToPrettyString(ent.Owner)}");
                _doAfter.TryStartDoAfter(doAfter);
            }
        }
    }

    private void OnEjectKeysDoAfter(Entity<VehicleComponent> ent, ref EjectKeysDoAfter args)
    {
        if(args.Cancelled || args.Handled) return;
        if(TryComp<ContainerManagerComponent>(ent.Owner, out var container))
        {
            var key = container.Containers.Values.SelectMany(c => c.ContainedEntities).FirstOrDefault(e => _tags.HasTag(e, _vehicleKeyTag));
            ent.Comp.hasKeys = false;
            TurnOffVehicle(ent.Owner, ent.Comp);
            if(ent.Comp.Rider == null) return;

            UpdateActions(ent.Comp.Rider.Value, false);
            if(TryComp<InputMoverComponent>(ent.Comp.Rider.Value, out var imComp) && imComp.CanMove)
                _actionBlocker.UpdateCanMove(ent.Comp.Rider.Value);

            for (var i = 0; i < ent.Comp.HandsNeeded; i++)
            {
                _virtualItem.DeleteInHandsMatching(ent.Comp.Rider.Value, ent.Owner);
            }
            _handsSystem.PickupOrDrop(args.User, key);
            ent.Comp.Rider = null;
            Dirty(ent);
        }
        args.Handled = true;
    }

    private void OnTurnKeysEvent(Entity<VehicleComponent> ent, ref TurnKeysEvent args)
    {
        if(args.Handled || ent.Comp.Rider == null) return;
        if(!TryComp<MovementSpeedModifierComponent>(ent.Owner, out var msmComp) || msmComp.BaseSprintSpeed <= 0)
        {
            args.Handled = true;
            _popup.PopupClient(Loc.GetString("vehicle-turn-keys-fail"), ent.Comp.Rider.Value, PopupType.SmallCaution);
            return;
        }
        if(!ent.Comp.Started)
        {
            _popup.PopupClient(Loc.GetString("vehicle-turn-keys-start"), ent.Comp.Rider.Value, PopupType.Medium);
            _adminLogger.Add(Database.LogType.Action, Database.LogImpact.Low, $"{ToPrettyString(ent.Comp.Rider.Value)} started the engine of {ToPrettyString(ent.Owner)}");
            _audio.PlayPredicted(ent.Comp.StartUp, ent.Owner, ent.Comp.Rider.Value);
        }
        if(ent.Comp.Started)
        {
            _popup.PopupClient(Loc.GetString("vehicle-turn-keys-stop"), ent.Comp.Rider.Value, PopupType.Medium);
            _adminLogger.Add(Database.LogType.Action, Database.LogImpact.Low, $"{ToPrettyString(ent.Comp.Rider.Value)} stopped the engine of {ToPrettyString(ent.Owner)}");
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
            ^ (!ent.Comp.CellPowered && HasComp<ReagentDrawComponent>(ent.Owner) && !_reagentDraw.HasDrawReagant(ent.Owner)))
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
        if(!ent.Comp.CellPowered && TryComp<ReagentDrawComponent>(ent.Owner, out var rdComp))
        {
            rdComp.Enabled = ent.Comp.Started;
            Dirty(ent.Owner, rdComp);
            _ambientSound.SetAmbience(ent.Owner, ent.Comp.Started);
        }

        _actionBlocker.UpdateCanMove(ent.Comp.Rider.Value);

        Dirty(ent.Owner, ent.Comp);
    }

    private void HandleCollide(Entity<VehicleComponent> ent, ref StartCollideEvent args)
    {
        if(!_gameTiming.IsFirstTimePredicted) return;
        if(ent.Comp.Rider == null) return;
        var rider = ent.Comp.Rider.Value;

        if(!ent.Comp.AllowCrashing) return;
        if(!TryComp<MovementSpeedModifierComponent>(ent.Owner, out var msmComp)) return;

        var speed = args.OurBody.LinearVelocity.Length();
        var crashingSpeed = 0f;

        if(msmComp.BaseSprintSpeed > msmComp.BaseWalkSpeed)
            crashingSpeed = msmComp.BaseWalkSpeed+1;
        else if(msmComp.BaseSprintSpeed < msmComp.BaseWalkSpeed)
            crashingSpeed = msmComp.BaseSprintSpeed+1;

        if(crashingSpeed < 8f)
            crashingSpeed = 8f;

        if (speed < crashingSpeed) return;

        if (args.OurFixture.Hard && args.OtherFixture.Hard)
        {
            if (_gameTiming.IsFirstTimePredicted)
                _audio.PlayPredicted(ent.Comp.SoundHit, ent.Owner, null, AudioParams.Default.WithVariation(0.125f).WithVolume(-0.125f));

            if(TryComp<VehicleBuckleComponent>(ent.Owner, out var vbComp) && TryComp<BuckleComponent>(rider, out var buckleComp))
            {
                if(TryComp<PhysicsComponent>(ent.Owner, out var vehiclePhys) && TryComp<PhysicsComponent>(rider, out var riderPhys))
                    if(_buckle.TryUnbuckle(rider, null, buckleComp) && vbComp.EjectOnCrash)
                    {
                        var riderXform = Transform(rider);
                        _stun.TryCrawling(rider, TimeSpan.FromSeconds(3));
                        _throwing.TryThrow(rider, vehiclePhys.LinearVelocity, riderPhys, riderXform, _projQuery, vehiclePhys.LinearVelocity.Length(), playSound: false);
                        _adminLogger.Add(Database.LogType.Landed, Database.LogImpact.Medium, $"{ToPrettyString(rider)} was launched from vehicle {ToPrettyString(ent.Owner)}");
                    }
            }
            else if(TryComp<VehicleContainerComponent>(ent.Owner, out var vcComp))
            {
                foreach(var passenger in vcComp.PassengerSlot.ContainedEntities)
                {
                    _stun.TryAddStunDuration(passenger, TimeSpan.FromSeconds(3));
                    _adminLogger.Add(Database.LogType.Landed, Database.LogImpact.Medium, $"{ToPrettyString(passenger)} was stunned inside of vehicle {ToPrettyString(ent.Owner)}");
                }
            }
        }
        else if(args.OurFixture.Hard && !args.OtherFixture.Hard)
        {
            if(!HasComp<DamageableComponent>(args.OtherEntity) || HasComp<PacifiedComponent>(ent.Owner)) return;

            if (_gameTiming.IsFirstTimePredicted)
                _audio.PlayPredicted(ent.Comp.SoundHit, ent.Owner, null, AudioParams.Default.WithVariation(0.125f).WithVolume(-0.125f));

            DamageTypePrototype? _blunt = _prototypes.Index<DamageTypePrototype>(_bluntname);
            DamageSpecifier? _damage = new(_blunt, Math.Clamp(10 * (1 + (0.5 * speed / crashingSpeed)), 10, 20));
            _damageable.TryChangeDamage(args.OtherEntity, _damage, origin: ent.Comp.Rider.Value);
            _color.RaiseEffect(Color.Red, new List<EntityUid>() { args.OtherEntity, }, Filter.Pvs(args.OtherEntity, entityManager: EntityManager));

            if(!TryComp<MovementSpeedModifierComponent>(ent.Owner, out var msmComp)) return;

            Timer.Spawn(TimeSpan.FromSeconds(2), () => _movementSpeed.ChangeBaseSpeed(ent.Owner, msmComp.BaseWalkSpeed * 4, msmComp.BaseSprintSpeed * 4, msmComp.Acceleration));
            _movementSpeed.ChangeBaseSpeed(ent.Owner, msmComp.BaseWalkSpeed/4, msmComp.BaseSprintSpeed/4, msmComp.Acceleration);
            _adminLogger.Add(Database.LogType.Landed, Database.LogImpact.High, $"{ToPrettyString(ent.Comp.Rider.Value)} ran over {ToPrettyString(args.OtherEntity)} dealing {_damage}");
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
        _adminLogger.Add(Database.LogType.Action, Database.LogImpact.Low, $"{ToPrettyString(args.User)} repaired the vehicle {ToPrettyString(ent.Owner)}");
        ent.Comp.isBroken = false;

        if(TryComp<VehicleBuckleComponent>(ent, out var vbComp))
        {
            _buckle.StrapSetEnabled(ent, true);
        }
        TryUpdateVisualState(ent.Owner);
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnToggleTrunk(Entity<VehicleComponent> ent, ref ToggleTrunkActionEvent args)
    {
        if (!_gameTiming.IsFirstTimePredicted) return;
        if(args.Handled) return;
        if(!TryComp<LockComponent>(ent.Owner, out var lockComp)) return;
        _adminLogger.Add(Database.LogType.Action, Database.LogImpact.Low, $"{ToPrettyString(args.Performer)} toggled the trunk from {ToPrettyString(ent.Owner)}");
        _lock.ToggleLock(ent.Owner, args.Performer, lockComp);

        if(!_lock.IsLocked(ent.Owner))
        {
            _popup.PopupPredicted(Loc.GetString("vehicle-toggle-trunk-open"), ent.Owner, null, PopupType.Small);
            _audio.PlayPredicted(lockComp.UnlockSound, ent.Owner, null);
        }
        else
        {
            _popup.PopupPredicted(Loc.GetString("vehicle-toggle-trunk-close"), ent.Owner, null, PopupType.Small);
            _audio.PlayPredicted(lockComp.LockSound, ent.Owner, null);
        }
        args.Handled = true;
    }

    private void OnDamageChanged(EntityUid ent, VehicleComponent component, DamageChangedEvent args)
    {
        if(!args.DamageIncreased || args.DamageDelta == null) return;
        if(args.Origin == ent) return;
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
                    TryRemove(passengers, ent, vcComp);
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

    private void OnExamine(Entity<VehicleComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if(ent.Comp.isBroken)
            args.PushMarkup(Loc.GetString("vehicle-examine-broken"));
    }

    private void OnCanDragDrop(Entity<VehicleComponent> ent, ref CanDropTargetEvent args)
    {
        args.CanDrop = !ent.Comp.isBroken;
        args.Handled = true;
    }

    public void TryUpdateVisualState(Entity<VehicleComponent?> entity)
    {
        if (!Resolve(entity.Owner, ref entity.Comp))
            return;

        if (_gameTiming.ApplyingState)
            return;

        var finalState = VehicleVisualState.Normal;

        if (entity.Comp.isBroken)
        {
            finalState = VehicleVisualState.Broken;
        }
        else if (entity.Comp.isMoving)
        {
            finalState = VehicleVisualState.Moving;
        }
        _appearance.SetData(entity.Owner, VehicleVisuals.VisualState, finalState);
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
            _adminLogger.Add(Database.LogType.Action, Database.LogImpact.Low, $"{ToPrettyString(args.User)} attempted to steal vehicle {ToPrettyString(ent.Owner)}");
            _popup.PopupClient(Loc.GetString("vehicle-steal-vehicle-attempt"), vehicleComp.Rider.Value, PopupType.LargeCaution);
            var ev = new VehicleUnbuckleDoAfter();
            var doAfter = new DoAfterArgs(EntityManager, args.User.Value, ent.Comp.duration, ev, ent.Owner, ent.Owner)
            {
                BreakOnMove = true,
                BreakOnDamage = true
            };
            _doAfter.TryStartDoAfter(doAfter);
        }
    }

    private void OnHornActionEvent(Entity<VehicleComponent> ent, ref HornActionEvent args)
    {
        if (!_gameTiming.IsFirstTimePredicted) return;
        if (args.Handled || ent.Comp.HornSound == null)
            return;
        if(ent.Comp.Rider == null) return;
        _audio.PlayPredicted(ent.Comp.HornSound, ent.Owner, null);
        args.Handled = true;
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
        var passenger = ent.Comp.PassengerSlot.ContainedEntities.FirstOrDefault();
        if(passenger == default) return;
        RemoveRider(passenger, ent.Owner, vehicleComp);
        TryRemove(passenger, ent.Owner, ent.Comp);

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

            if(_tags.HasTag(args.Entity, _vehicleKeyTag)) return;

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
        TryComp<ReagentDrawComponent>(riding, out var rdComp);

        var noPower =
            (vehicleComp.CellPowered && pcdComp != null && !_powerCell.HasDrawCharge(riding)) ^
            (!vehicleComp.CellPowered && rdComp != null && !_reagentDraw.HasDrawReagant(riding));

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

    private void OnPullAttempt(Entity<RiderComponent> ent, ref PullAttemptEvent args)
    {
        if(TryComp<MobStateComponent>(ent.Owner, out var mbState)
        && (mbState.CurrentState == Mobs.MobState.Critical
            || mbState.CurrentState == Mobs.MobState.Dead
            || mbState.CurrentState == Mobs.MobState.Invalid))
        {
            _buckle.Unbuckle(ent.Owner, args.PullerUid);
            return;
        }
        args.Cancelled = true;
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
            ^ HasComp<ReagentDrawComponent>(riderComp.Riding.Value))
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
            || riderComp.Riding == null
            || !TryComp<VehicleComponent>(riderComp.Riding.Value, out var vehicleComp)) return;
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
    public void SetUpRider(EntityUid rider, EntityUid vehicle, VehicleComponent vehicleComp)
    {
        var riderComp = EnsureComp<RiderComponent>(rider);
        riderComp.Riding = vehicle;
        Dirty(rider, riderComp);
        _adminLogger.Add(Database.LogType.Action, Database.LogImpact.Low, $"{ToPrettyString(rider)} entered vehicle {ToPrettyString(vehicle)}");
        if(TryComp<InputMoverComponent>(rider, out var imComp) && imComp.CanMove)
            _actionBlocker.UpdateCanMove(rider);

        foreach(var item in _handsSystem.EnumerateHeld(rider))
        {
            if(HasComp<GunComponent>(item))
                _gun.RefreshModifiers(item);
        }

        if(_whitelist.IsWhitelistFail(vehicleComp.RiderWhitelist, rider)) return;
        if(!vehicleComp.hasKeys && vehicleComp.RequireIgnition) return;
        if(vehicleComp.Rider != null) return;

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

    public void RemoveRider(EntityUid rider, EntityUid vehicle, VehicleComponent vehicleComp)
    {
        _adminLogger.Add(Database.LogType.Action, Database.LogImpact.Low, $"{ToPrettyString(rider)} exited vehicle {ToPrettyString(vehicle)}");
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
        Dirty(Vehicle, component);
        return true;
    }

    public bool CanInsert(EntityUid uid, VehicleContainerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        return component.PassengerSlot.ContainedEntities.Count() < component.Seats;
    }

    public bool TryRemove(EntityUid? Rider, EntityUid Vehicle, VehicleContainerComponent? component=null)
    {
        if(!Resolve(Vehicle, ref component))
            return false;

        if(Rider == null)
            return false;

        _container.Remove(Rider.Value, component.PassengerSlot);
        Dirty(Vehicle, component);
        return true;
    }

    private void TurnOffVehicle(EntityUid vehicle, VehicleComponent? component=null)
    {
        if(!Resolve(vehicle, ref component))
            return;

        var ev = new TurnOffVehicleEvent();
        RaiseLocalEvent(vehicle, ref ev);

        if(component.Started)
            component.Started = false;

        if(component.CellPowered && TryComp<PowerCellDrawComponent>(vehicle, out var pcdComp) && pcdComp.Enabled)
        {
            _powerCell.SetDrawEnabled((vehicle, pcdComp), false);
        }
        if(!component.CellPowered && TryComp<ReagentDrawComponent>(vehicle, out var rdComp) && rdComp.Enabled)
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
