using Content.Shared.Actions;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Clothing;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Lock;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Repairable;
using Content.Shared.Tools;
using Content.Shared.Stunnable;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Vehicle;

public sealed partial class VehicleRideSystem : EntitySystem
{
    [Dependency] private VehicleSystem _vehicles = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedWieldableSystem _wield = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private const SlotFlags WeightSlots = SlotFlags.FEET | SlotFlags.HEAD | SlotFlags.EYES |
        SlotFlags.GLOVES | SlotFlags.MASK | SlotFlags.NECK | SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING;

    [SubscribeLocalEvent]
    private void OnOperatorChanged(Entity<VehicleRideComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (!_net.IsClient)
        {
            _actions.RemoveAction(ent.Comp.HornActionEntity);
            _actions.RemoveAction(ent.Comp.TrunkActionEntity);
        }

        if (args.NewOperator is { } driver)
        {
            if (!_net.IsClient)
            {
                if (ent.Comp.HornSound != null)
                    _actions.AddAction(driver, ref ent.Comp.HornActionEntity, ent.Comp.HornAction, ent.Owner);
                if (ent.Comp.TrunkAction is { } trunk)
                    _actions.AddAction(driver, ref ent.Comp.TrunkActionEntity, trunk, ent.Owner);
                Dirty(ent);
            }

            if (ent.Comp.DisallowWielding)
            {
                foreach (var held in _hands.EnumerateHeld(driver))
                {
                    if (TryComp<WieldableComponent>(held, out var wieldable))
                        _wield.TryUnwield(held, wieldable, driver);
                }
            }

            RefreshHeldGuns(driver);
        }

        if (args.OldOperator is { } oldDriver)
        {
            if (!_net.IsClient)
                _actions.RemoveProvidedActions(oldDriver, ent.Owner);
            RefreshHeldGuns(oldDriver);
        }

        if (ent.Comp.RiderSpeedAffectsVehicle && !TerminatingOrDeleted(ent))
            _movementSpeed.RefreshMovementSpeedModifiers(ent);
    }

    [SubscribeLocalEvent]
    private void OnHorn(EntityUid owner, VehicleRideComponent component, HornActionEvent args)
    {
        var ent = new Entity<VehicleRideComponent>(owner, component);
        if (args.Handled || !_timing.IsFirstTimePredicted || _vehicles.GetOperatorOrNull(ent.Owner) != args.Performer)
            return;

        _audio.PlayPredicted(ent.Comp.HornSound, ent, args.Performer);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnTrunk(EntityUid owner, VehicleRideComponent component, ToggleTrunkActionEvent args)
    {
        var ent = new Entity<VehicleRideComponent>(owner, component);
        if (args.Handled || _vehicles.GetOperatorOrNull(ent.Owner) != args.Performer ||
            !TryComp<LockComponent>(ent, out var locked))
            return;

        if (locked.Locked)
            _lock.Unlock(ent, args.Performer, locked);
        else
            _lock.Lock(ent, args.Performer, locked);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnUnstrapAttempt(Entity<VehicleRideComponent> ent, ref UnstrapAttemptEvent args)
    {
        if (args.Cancelled || !args.Popup || args.User is not { } user ||
            ent.Comp.UnbuckleDelay is not { } delay ||
            _vehicles.GetOperatorOrNull(ent.Owner) is not { } driver || user == driver ||
            args.Buckle.Owner != driver || _mobState.IsIncapacitated(driver))
            return;

        args.Cancelled = true;
        if (_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, delay,
                new VehicleUnbuckleDoAfter(GetNetEntity(driver)), ent.Owner, target: ent.Owner)
            { BreakOnMove = true, BreakOnDamage = true }))
        {
            _popup.PopupClient(Loc.GetString("vehicle-steal-vehicle-attempt"), ent, driver);
        }
    }

    [SubscribeLocalEvent]
    private void OnDriverRemoved(EntityUid owner, VehicleRideComponent component, VehicleUnbuckleDoAfter args)
    {
        var ent = new Entity<VehicleRideComponent>(owner, component);
        if (args.Cancelled || args.Handled || !_interaction.InRangeUnobstructed(args.User, ent.Owner) ||
            _vehicles.GetOperatorOrNull(ent.Owner) is not { } driver || GetNetEntity(driver) != args.Driver)
            return;

        _buckle.Unbuckle(driver, args.User);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnStunned(Entity<VehicleOperatorComponent> ent, ref StunnedEvent args)
    {
        if (TryComp<VehicleRideComponent>(ent.Comp.Vehicle, out var ride) && ride.DismountOnStun)
            _buckle.Unbuckle(ent.Owner, ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnKnockedDown(Entity<VehicleOperatorComponent> ent, ref KnockedDownEvent args)
    {
        if (TryComp<VehicleRideComponent>(ent.Comp.Vehicle, out var ride) && ride.DismountOnKnockdown)
            _buckle.Unbuckle(ent.Owner, ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnPullAttempt(EntityUid owner, VehicleOperatorComponent component, PullAttemptEvent args)
    {
        var ent = new Entity<VehicleOperatorComponent>(owner, component);
        if (!HasComp<VehicleRideComponent>(ent.Comp.Vehicle) || !HasComp<StrapVehicleComponent>(ent.Comp.Vehicle))
            return;

        if (_mobState.IsIncapacitated(ent))
            _buckle.Unbuckle(ent.Owner, args.PullerUid);
        else
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnRiderSpeedRefresh(Entity<VehicleOperatorComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Vehicle is not { } vehicle ||
            TerminatingOrDeleted(vehicle) ||
            !TryComp<VehicleRideComponent>(vehicle, out var ride) ||
            !ride.RiderSpeedAffectsVehicle ||
            _vehicles.GetOperatorOrNull(vehicle) != ent.Owner)
            return;

        _movementSpeed.RefreshMovementSpeedModifiers(vehicle);
    }

    [SubscribeLocalEvent]
    private void OnRefreshSpeed(EntityUid owner, VehicleRideComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        var ent = new Entity<VehicleRideComponent>(owner, component);
        if (!ent.Comp.RiderSpeedAffectsVehicle || _vehicles.GetOperatorOrNull(ent.Owner) is not { } driver)
            return;

        var clothing = new ClothingSpeedModifierQueryEvent(WeightSlots);
        RaiseLocalEvent(driver, clothing);
        args.ModifySpeed(clothing.TotalWalkCoefficient, clothing.TotalSprintCoefficient);
    }

    [SubscribeLocalEvent]
    private void OnWieldAttempt(Entity<VehicleOperatorComponent> ent, ref WieldAttemptEvent args)
    {
        if (TryComp<VehicleRideComponent>(ent.Comp.Vehicle, out var ride) && ride.DisallowWielding)
            args.Cancel();
    }

    [SubscribeLocalEvent]
    private void OnRecoil(Entity<VehicleOperatorComponent> ent, ref ShooterImpulseEvent args)
    {
        if (!TryComp<VehicleRideComponent>(ent.Comp.Vehicle, out var ride) || !ride.GunStaminaRecoil ||
            !TryComp<StaminaComponent>(ent, out var stamina))
            return;

        foreach (var held in _hands.EnumerateHeld(ent.Owner))
        {
            if (HasComp<GunComponent>(held) && HasComp<WieldableComponent>(held))
            {
                _stamina.TakeStaminaDamage(ent, stamina.CritThreshold * 0.1f, component: stamina);
                break;
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnGunModifiers(Entity<GunComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (!TryComp<VehicleRideComponent>(_vehicles.GetRideVehicle(Transform(ent).ParentUid), out var ride))
            return;

        var penalty = Angle.FromDegrees(ride.GunSpreadPenalty);
        args.MinAngle += penalty;
        args.MaxAngle += penalty;
    }

    [SubscribeLocalEvent]
    private void OnHandEquipped(EntityUid owner, VehicleOperatorComponent component, DidEquipHandEvent args)
    {
        var ent = new Entity<VehicleOperatorComponent>(owner, component);
        if (HasComp<GunComponent>(args.Equipped))
            _gun.RefreshModifiers(args.Equipped);
    }

    private void RefreshHeldGuns(EntityUid user)
    {
        if (TerminatingOrDeleted(user))
            return;

        foreach (var item in _hands.EnumerateHeld(user))
        {
            if (HasComp<GunComponent>(item))
                _gun.RefreshModifiers(item);
        }
    }

    [SubscribeLocalEvent]
    private void OnCanRun(Entity<VehicleHullComponent> ent, ref VehicleCanRunEvent args)
        => args.CanRun &= !ent.Comp.Broken;

    [SubscribeLocalEvent]
    private void OnEntryAttempt(Entity<VehicleHullComponent> ent, ref ContainerVehicleEntryAttemptEvent args)
        => args.Cancelled |= ent.Comp.Broken;

    [SubscribeLocalEvent]
    private void OnBreak(EntityUid uid, VehicleHullComponent component, BreakageEventArgs args)
        => SetBroken((uid, component), true);

    [SubscribeLocalEvent]
    private void OnRepaired(Entity<VehicleHullComponent> ent, ref RepairedEvent args)
    {
        if (_net.IsServer)
            SetBroken(ent, false);
    }

    [SubscribeLocalEvent]
    private void OnExamineHull(Entity<VehicleHullComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.Broken)
            return;

        args.PushMarkup(Loc.GetString("vehicle-examine-broken"));
        if (TryComp<RepairableComponent>(ent, out var repairable) &&
            _prototypes.TryIndex<ToolQualityPrototype>(repairable.QualityNeeded, out var quality))
        {
            args.PushMarkup(Loc.GetString("vehicle-examine-repair", ("quality", Loc.GetString(quality.ToolName))));
        }
    }

    private void SetBroken(Entity<VehicleHullComponent> ent, bool broken)
    {
        ent.Comp.Broken = broken;
        Dirty(ent);
        _appearance.SetData(ent.Owner, VehicleHullVisuals.Broken, broken);
        if (TryComp<StrapComponent>(ent, out var strap))
            _buckle.StrapSetEnabled(ent, !broken, strap);
        _vehicles.RefreshCanRun(ent.Owner);
    }
}
