using Content.Shared._FarHorizons.Vehicles.EntitySystems;
using Content.Shared._FarHorizons.VehicleBuckle.Components;
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
using Content.Shared.Pulling.Events;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using System.Numerics;
using System.Linq;
using Content.Server.PowerCell;
using Content.Shared.PowerCell;
using Robust.Server.GameObjects;
using Content.Shared._FarHorizons.Vehicles;
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
using Content.Shared.Light.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Interaction.Components;

namespace Content.Server._FarHorizons.Vehicle;

public sealed partial class VehicleSystems : SharedVehicleSystems
{    
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedReagantDrawSystem _reagantDraw = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;

    private static readonly ProtoId<TagPrototype> _vehicleKeyTag = "VehicleKey";
    private EntityQuery<ProjectileComponent> _projQuery;
    public override void Initialize()
    {
        base.Initialize();

        _projQuery = GetEntityQuery<ProjectileComponent>();

        SubscribeLocalEvent<VehicleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VehicleComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);
        SubscribeLocalEvent<VehicleComponent, ItemSlotInsertEvent>(OnInsertEvent);
        SubscribeLocalEvent<VehicleComponent, StartCollideEvent>(HandleCollide);
        SubscribeLocalEvent<VehicleComponent, ItemSlotEjectEvent>(OnEjectEvent);
        SubscribeLocalEvent<VehicleComponent, EjectKeysDoAfter>(OnEjectKeysDoAfter);
        SubscribeLocalEvent<VehicleComponent, TurnKeysDoAfter>(OnTurnKeysDoAfter);
        SubscribeLocalEvent<VehicleComponent, ReagantContainerSlotEmptyEvent>(OnEmptyReagantContainer);
        SubscribeLocalEvent<VehicleComponent, PowerCellSlotEmptyEvent>(OnPowerCellEmpty);

        SubscribeLocalEvent<VehicleBuckleComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<VehicleBuckleComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<VehicleBuckleComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        SubscribeLocalEvent<VehicleBuckleComponent, VehicleUnbuckleDoAfter>(OnUnbuckleDoAfter);

        SubscribeLocalEvent<RiderComponent, BeingPulledAttemptEvent>(OnPullAttempt);
        SubscribeLocalEvent<RiderComponent, StunnedEvent>(OnStunned);
        SubscribeLocalEvent<RiderComponent, KnockedDownEvent>(OnKnockdown);
        SubscribeLocalEvent<RiderComponent, UpdateCanMoveEvent>(OnUpdateCanMoveEvent);
        SubscribeLocalEvent<RiderComponent, JumpActionEvent>(OnJumpActionEvent);

        SubscribeLocalEvent<TransformComponent, JetJumpActionEvent>(OnJetJumpActionEvent);

        _transform.OnGlobalMoveEvent += OnMoveEvent;
    }
    #region Vehicle Generic Events
    private void OnMapInit(Entity<VehicleComponent> ent, ref MapInitEvent args)
    {
        if(!TryComp<MovementSpeedModifierComponent>(ent.Owner, out var msmComp)) return;
        _movementSpeed.ChangeFrictionAndAcceleration(ent.Owner, ent.Comp.Friction, ent.Comp.FrictionNoInput, ent.Comp.Acceleration, msmComp);
        _appearance.SetData(ent.Owner, VehicleVisuals.AutoAnimate, false);
    }

    private void OnInsertEvent(Entity<VehicleComponent> ent, ref ItemSlotInsertEvent args)
    {
        if(ent.Comp.Rider == null) return;
        if(_tags.HasTag(args.Item, _vehicleKeyTag))
        {
            Timer.Spawn(0, () => AddActions(ent.Comp.Rider.Value, ent.Owner, ent.Comp)); // Race conditions :strangle:
        }
    }

    private void OnEjectEvent(Entity<VehicleComponent> ent, ref ItemSlotEjectEvent args)
    {
        if (!_gameTiming.IsFirstTimePredicted) return;
        if(ent.Comp.Rider == null) return;
        if(args.User == null) return;
        if(_tags.HasTag(args.Item, _vehicleKeyTag))
        {
            if(ent.Comp.Rider == args.User)
            {
                if(ent.Comp.Started)
                    ent.Comp.Started = false;
                _actionBlocker.UpdateCanMove(ent.Comp.Rider.Value);

                if(ent.Comp.TurnKeysActionEntity != null)
                    _actions.RemoveProvidedAction(ent.Comp.Rider.Value, ent.Owner, ent.Comp.TurnKeysActionEntity.Value);
                    
                if(TryComp<PowerCellDrawComponent>(ent.Owner, out var pcdComp) && pcdComp.Enabled)
                {
                    pcdComp.Enabled = false;
                    Dirty(ent.Owner, pcdComp);
                }
                if(TryComp<ReagantDrawComponent>(ent.Owner, out var rdComp) && rdComp.Enabled)
                {
                    rdComp.Enabled = false;
                    Dirty(ent.Owner, rdComp);
                    _ambientSound.SetAmbience(ent.Owner, rdComp.Enabled);
                }
                Dirty(ent.Owner, ent.Comp);
            }
            else
            {
                args.Cancelled = true;
                _popup.PopupEntity($"Someone is trying to steal the keys from the ignition.", ent.Comp.Rider.Value, PopupType.LargeCaution);
                var ev = new EjectKeysDoAfter();
                var doAfter = new DoAfterArgs(EntityManager, args.User.Value, ent.Comp.timeToStealKeys, ev, ent.Owner, ent.Owner)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                    CancelDuplicate = false

                };
                _doAfter.TryStartDoAfter(doAfter);
            }
        }
    }

    private void HandleCollide(Entity<VehicleComponent> ent, ref StartCollideEvent args)
    {
        if(ent.Comp.Rider == null) return;
        var rider = ent.Comp.Rider.Value;
        
        if(TryComp<VehicleBuckleComponent>(ent.Owner, out var vehicleBuckleComp))
        {
            if(!vehicleBuckleComp.ejectOnCrash) return;

            if (!args.OurFixture.Hard || !args.OtherFixture.Hard) return;

            var speed = args.OurBody.LinearVelocity.Length();

            if (speed < vehicleBuckleComp.SpeedToEjectOnCrash) return;
            
            if(!TryComp<PhysicsComponent>(ent.Owner, out var vehiclePhys) || !TryComp<PhysicsComponent>(rider, out var riderPhys))
                return;

            if(TryComp<BuckleComponent>(rider, out var buckleComp))
                if(_buckle.TryUnbuckle(rider, null, buckleComp))
                {
                    var riderXform = Transform(rider);
                    _stun.TryCrawling(rider, TimeSpan.FromSeconds(3));
                    _throwing.TryThrow(rider, vehiclePhys.LinearVelocity, riderPhys, riderXform, _projQuery, vehiclePhys.LinearVelocity.Length(), playSound: false);
                }
        }
    }

    private void OnGetAdditionalAccess(Entity<VehicleComponent> ent, ref GetAdditionalAccessEvent args)
    {
        if (ent.Comp.Rider == null) return;

        args.Entities.Add(ent.Comp.Rider.Value);
    }

    protected override void OnTurnKeysEvent(Entity<VehicleComponent> ent, ref TurnKeysEvent args)
    {
        if(ent.Comp.Rider == null) return;
        if(!ent.Comp.Started)
        {
            _popup.PopupEntity($"You turn the keys to start the vehicle.", ent.Owner, PopupType.Medium);
        }
        if(ent.Comp.Started)
            _popup.PopupEntity($"You turn the keys to stop the vehicle.", ent.Owner, PopupType.Medium);
        
        var ev = new TurnKeysDoAfter();
        var doAfter = new DoAfterArgs(EntityManager, ent.Comp.Rider.Value, ent.Comp.startupTime, ev, ent.Owner)
        {
            BreakOnMove = true
        };
        _doAfter.TryStartDoAfter(doAfter);
        args.Handled = true;
    }

    private void OnEmptyReagantContainer(Entity<VehicleComponent> ent, ref ReagantContainerSlotEmptyEvent args)
    {
        if(!TryComp<ReagantDrawComponent>(ent.Owner, out var rdComp)) return;

        ent.Comp.Started = false;
        rdComp.Enabled = false;
        _ambientSound.SetAmbience(ent.Owner, false);
        Dirty(ent.Owner, rdComp);
        Dirty(ent.Owner, ent.Comp);

        if(ent.Comp.Rider != null)  
            _actionBlocker.UpdateCanMove(ent.Owner);
    }

    private void OnPowerCellEmpty(Entity<VehicleComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        if(!TryComp<PowerCellDrawComponent>(ent.Owner, out var pcdComp)) return;

        ent.Comp.Started = false;
        pcdComp.Enabled = false;
        _ambientSound.SetAmbience(ent.Owner, false);
        Dirty(ent.Owner, pcdComp);
        Dirty(ent.Owner, ent.Comp);

        if(ent.Comp.Rider != null)  
            _actionBlocker.UpdateCanMove(ent.Owner);
    }

    #endregion
    #region VehicleBuckle Events
    private void OnStrapped(Entity<VehicleBuckleComponent> ent, ref StrappedEvent args)
    {
        if(!TryComp<VehicleComponent>(ent, out var vehicleComp)) return;
        if(vehicleComp.Rider == null)
        {
            EnsureComp<RiderComponent>(args.Buckle.Owner);
            Comp<RiderComponent>(args.Buckle.Owner).Riding = ent.Owner;

            _mover.SetRelay(args.Buckle.Owner, ent.Owner);
            vehicleComp.Rider = args.Buckle.Owner;
            Dirty(ent.Owner, vehicleComp);
            Dirty(ent.Owner, ent.Comp);

            _actionBlocker.UpdateCanMove(args.Buckle.Owner);
            AddActions(vehicleComp.Rider.Value, ent.Owner, vehicleComp);

            for (var i = 0; i < vehicleComp.HandsNeeded; i++)
            {
                if (_virtualItem.TrySpawnVirtualItem(ent.Owner, vehicleComp.Rider.Value, out var virtItem))
                {
                    EnsureComp<UnremoveableComponent>(virtItem.Value);
                    _handsSystem.TryForcePickupAnyHand(vehicleComp.Rider.Value, virtItem.Value);
                }
            }
        }
    }

    private void OnUnstrapAttempt(Entity<VehicleBuckleComponent> ent, ref UnstrapAttemptEvent args)
    {
        if(!TryComp<VehicleComponent>(ent.Owner, out var vehicleComp)) return;
        if(args.User == null || !args.Popup) return;
        if(vehicleComp.Rider == null) return;
        if (vehicleComp.Rider != args.User)
        {
            args.Cancelled = true;
            _popup.PopupEntity($"Someone starts to remove you from the driver seat.", vehicleComp.Rider.Value, PopupType.LargeCaution);
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

        if(HasComp<RelayInputMoverComponent>(args.Buckle.Owner))
            RemComp<RelayInputMoverComponent>(args.Buckle.Owner);
        if(HasComp<RiderComponent>(args.Buckle.Owner))
            RemComp<RiderComponent>(args.Buckle.Owner);
        vehicleComp.Rider = null;
        _actionBlocker.UpdateCanMove(args.Buckle.Owner);
        _actions.RemoveProvidedActions(args.Buckle.Owner, ent.Owner);
        
        for (var i = 0; i < vehicleComp.HandsNeeded; i++)
        {
            _virtualItem.DeleteInHandsMatching(args.Buckle.Owner, ent.Owner);
        }

        Dirty(ent.Owner, vehicleComp);
        Dirty(ent.Owner, ent.Comp);
    }

    #endregion
    #region Rider Events
    private void OnPullAttempt(Entity<RiderComponent> ent, ref BeingPulledAttemptEvent args)
    {
        if (ent.Owner != args.Puller)
            args.Cancel();
    }

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

        if (vehicleComp.requireIgnition && !vehicleComp.Started)
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

        if (pcdComp?.Enabled == vehicleComp.Started)
        {
            pcdComp.Enabled = false;
            Dirty(riding, pcdComp);
        }

        if (rdComp?.Enabled == true)
        {
            rdComp.Enabled = vehicleComp.Started;
            _ambientSound.SetAmbience(riding, false);
            Dirty(riding, rdComp);
        }

        Dirty(riding, vehicleComp);
        args.Cancel();
    }

    #endregion
    #region Misc Events

    private void OnJetJumpActionEvent(Entity<TransformComponent> ent, ref JetJumpActionEvent args)
    {
        if(!TryComp<BuckleComponent>(ent.Comp.ParentUid, out var buckleComp)) return;
        _buckle.Unbuckle((ent.Comp.ParentUid, buckleComp), ent.Comp.ParentUid);
    }

    private void OnMoveEvent(ref MoveEvent ev)
    {
        var vehicle = ev.Entity.Owner; 
        if(!TryComp<VehicleComponent>(vehicle, out var vehicleComp)) return;
        if(!TryComp<PhysicsComponent>(vehicle, out var vehiclePhys)) return;

        if(Math.Abs(vehiclePhys.LinearVelocity.X) > 0.3 || Math.Abs(vehiclePhys.LinearVelocity.Y) > 0.3)
            _appearance.SetData(vehicle, VehicleVisuals.AutoAnimate, true);
        if(Math.Abs(vehiclePhys.LinearVelocity.X) < 0.3 && Math.Abs(vehiclePhys.LinearVelocity.Y) < 0.3)
            _appearance.SetData(vehicle, VehicleVisuals.AutoAnimate, false);

        if( vehicleComp.Rider == null) return;
        var rider = vehicleComp.Rider.Value;
        if(!TryComp<PhysicsComponent>(rider, out var riderPhys)) return;
        var riderTransform = Transform(rider);
        if(riderTransform.ParentUid !=  vehicle) return;

        if(HasComp<VehicleBuckleComponent>(vehicle) && TryComp<StrapComponent>(vehicle, out var strapComp))
        {
            if(riderTransform.LocalPosition.X != 0+strapComp.BuckleOffset.X || riderTransform.LocalPosition.Y != 0+strapComp.BuckleOffset.Y)
                _transform.SetLocalPosition(rider, new Vector2(0f+strapComp.BuckleOffset.X, 0f+strapComp.BuckleOffset.Y), riderTransform);
            if(riderTransform.LocalRotation != 0)
                _transform.SetLocalRotation(rider, 0f, riderTransform);
        }
        
        if(!TryComp<MovementSpeedModifierComponent>(vehicle, out var moveComp)) return;
        if(Math.Abs(vehiclePhys.LinearVelocity.X) > 1.05*moveComp.CurrentSprintSpeed || 
            Math.Abs(vehiclePhys.LinearVelocity.Y) > 1.05*moveComp.CurrentSprintSpeed)
        {
            if(TryComp<BuckleComponent>(rider, out var buckleComp))
                if(_buckle.TryUnbuckle(rider, null, buckleComp))
                {
                    _stun.TryCrawling(rider, TimeSpan.FromSeconds(3));
                    _throwing.TryThrow(rider, vehiclePhys.LinearVelocity*riderPhys.Mass, riderPhys, riderTransform, _projQuery, vehiclePhys.LinearVelocity.Length(), playSound: false);
                }
        }

        if((HasComp<PowerCellDrawComponent>(vehicle) && !_powerCell.HasDrawCharge(vehicle)) 
        || (HasComp<ReagantDrawComponent>(vehicle) && !_reagantDraw.HasDrawReagant(vehicle)))
            _actionBlocker.UpdateCanMove(rider);
    }
    
    #endregion
    #region DoAfters
    private void OnEjectKeysDoAfter(Entity<VehicleComponent> ent, ref EjectKeysDoAfter args)
    {
        if(args.Cancelled) return;
        if(TryComp<ContainerManagerComponent>(ent.Owner, out var container))
        {
            var keys = container.Containers.Values.FirstOrDefault(x => _tags.HasTag(x.ContainedEntities.First(), _vehicleKeyTag))!.ContainedEntities.First();
            _handsSystem.PickupOrDrop(args.User, keys);
            if(ent.Comp.Started)
                ent.Comp.Started = false;
            if(ent.Comp.Rider == null) return;
            _actionBlocker.UpdateCanMove(ent.Comp.Rider.Value);

            if(ent.Comp.TurnKeysActionEntity != null)
                _actions.RemoveProvidedAction(ent.Comp.Rider.Value, ent.Owner, ent.Comp.TurnKeysActionEntity.Value);

            if(TryComp<PowerCellDrawComponent>(ent.Owner, out var pcdComp) && pcdComp.Enabled)
            {
                pcdComp.Enabled = false;
                Dirty(ent.Owner, pcdComp);
            }   
            if(TryComp<ReagantDrawComponent>(ent.Owner, out var rdComp) && rdComp.Enabled)
            {
                rdComp.Enabled = false;
                _ambientSound.SetAmbience(ent.Owner, rdComp.Enabled);
                Dirty(ent.Owner, rdComp);
            }
            Dirty(ent.Owner, ent.Comp);
        }
    }

    private void OnTurnKeysDoAfter(Entity<VehicleComponent> ent, ref TurnKeysDoAfter args)
    {
        if(args.Cancelled) return;
        
        if(!ent.Comp.Started)
            if((HasComp<PowerCellDrawComponent>(ent.Owner) && !_powerCell.HasDrawCharge(ent.Owner)) 
            || (HasComp<ReagantDrawComponent>(ent.Owner) && !_reagantDraw.HasDrawReagant(ent.Owner)))
                return;

        ent.Comp.Started = !ent.Comp.Started;
        if(TryComp<PowerCellDrawComponent>(ent.Owner, out var pcdComp))
        {
            pcdComp.Enabled = ent.Comp.Started;
        }
        if(TryComp<ReagantDrawComponent>(ent.Owner, out var rdComp))
        {
            rdComp.Enabled = ent.Comp.Started;
            Dirty(ent.Owner, rdComp);
            _ambientSound.SetAmbience(ent.Owner, ent.Comp.Started);
        }

        if(ent.Comp.Rider != null)
            _actionBlocker.UpdateCanMove(ent.Comp.Rider.Value);

        Dirty(ent.Owner, ent.Comp);
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

    #endregion
    #region Functions
        private void AddActions(EntityUid rider, EntityUid vehicle, VehicleComponent? component=null)
    {
        if (!Resolve(vehicle, ref component))
            return;

        if(component.requireIgnition && TryComp(vehicle, out ItemSlotsComponent? itemComp)
                && itemComp.Slots.Values.Any(slot =>
                    slot.ContainerSlot?.ContainedEntity is EntityUid item
                    && _tags.HasTag(item, _vehicleKeyTag)))
            _actions.AddAction(rider, ref component.TurnKeysActionEntity, component.TurnKeysAction, vehicle);

        if(TryComp<UnpoweredFlashlightComponent>(vehicle, out var flashComp))
            _actions.AddAction(rider, ref flashComp.ToggleActionEntity, flashComp.ToggleAction, vehicle);

        if(component.HornSound != null)
            _actions.AddAction(rider, ref component.HornVehicleActionEntity, component.HornVehicleAction, vehicle);
    }
    #endregion
}