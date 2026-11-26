using System.Linq;
using Content.Server._Starlight.Vehicle.Atmos;
using Content.Server.Destructible;
using Content.Shared._Starlight.ReagentDraw.Components;
using Content.Shared._FarHorizons.Vehicles.Components;
using Content.Shared._FarHorizons.Vehicles.Equipment;
using Content.Shared._Starlight.Vehicle;
using Content.Shared.Actions;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Vehicle.Equipment;

public sealed partial class VehicleEquipmentSystems : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private VehicleSystem _vehicles = default!;
    [Dependency] private VehicleIgnitionSystem _ignition = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private VehicleAtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private UnpoweredFlashlightSystem _flashlight = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private IRobustRandom _random = default!;

    [SubscribeLocalEvent(after: [typeof(MovementSpeedModifierSystem)])]
    private void OnMapInit(Entity<VehicleModsComponent> ent, ref MapInitEvent args)
    {
        var container = _containers.EnsureContainer<Container>(ent.Owner, ent.Comp.ModContainer);
        container.OccludesLight = false;
        ent.Comp.MaxIntegrity = _destructible.DestroyedAt(ent);
        ent.Comp.OriginalArmor = CompOrNull<DamageableComponent>(ent)?.DamageModifierSetId;

        foreach (var slot in ent.Comp.EquipmentSlots.GetFlags())
            ent.Comp.Equipment.TryAdd(slot, null);

        foreach (var proto in ent.Comp.StartingEquipment)
        {
            var item = Spawn(proto, Transform(ent).Coordinates);
            if (!TryComp<VehicleEquipmentComponent>(item, out var part) || !TryInstall(ent, (item, part)))
                QueueDel(item);
        }

        RefreshParts(ent);
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnInstall(EntityUid owner, VehicleModsComponent component, InteractUsingEvent args)
    {
        var ent = new Entity<VehicleModsComponent>(owner, component);
        if (args.Handled || !TryComp<VehicleEquipmentComponent>(args.Used, out var part) ||
            !CanInstall(ent, part))
            return;

        if (IsRunning(ent))
        {
            _popup.PopupEntity(Loc.GetString("vehicle-maintenance-running"), ent, args.User);
            return;
        }

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, part.InstallTime,
            new InstallDoAfter(GetNetEntity(args.Used)), ent.Owner, ent.Owner, args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
        });
    }

    [SubscribeLocalEvent]
    private void OnInstallCompleted(EntityUid owner, VehicleModsComponent component, InstallDoAfter args)
    {
        var ent = new Entity<VehicleModsComponent>(owner, component);
        if (args.Handled || args.Cancelled || IsRunning(ent) ||
            !_interaction.InRangeUnobstructed(args.User, ent.Owner) ||
            !TryGetEntity(args.Part, out var item) || item is not { } uid ||
            !_hands.IsHolding(args.User, uid) || !TryComp<VehicleEquipmentComponent>(uid, out var part))
            return;

        args.Handled = TryInstall(ent, (uid, part));
    }

    private static bool CanInstall(Entity<VehicleModsComponent> vehicle, VehicleEquipmentComponent part)
    {
        return (vehicle.Comp.VehicleType & part.AllowedVehicles) != 0 &&
            vehicle.Comp.Equipment.TryGetValue(part.Slot, out var existing) && existing == null;
    }

    private bool TryInstall(Entity<VehicleModsComponent> ent, Entity<VehicleEquipmentComponent> part)
    {
        if (!CanInstall(ent, part.Comp) ||
            !_containers.TryGetContainer(ent.Owner, ent.Comp.ModContainer, out var container) ||
            !_containers.Insert(part.Owner, container))
            return false;

        ent.Comp.Equipment[part.Comp.Slot] = part.Owner;
        _appearance.SetData(part.Owner, EquipmentVisuals.Hidden, true);
        RefreshParts(ent);

        if (_vehicles.GetOperatorOrNull(ent.Owner) is { } driver)
            GivePartAction(part, driver);

        Dirty(ent);
        return true;
    }

    [SubscribeLocalEvent]
    private void OnUninstall(EntityUid owner, VehicleModsComponent component, UninstallPartMessage args)
    {
        var ent = new Entity<VehicleModsComponent>(owner, component);
        if (!TryGetEntity(args.Part, out var uid) || uid is not { } item ||
            !IsInstalled(ent, args.Slot, item) || !TryComp<VehicleEquipmentComponent>(item, out var part) ||
            !CanReach(ent, args.Actor))
            return;

        if (IsRunning(ent))
        {
            _popup.PopupEntity(Loc.GetString("vehicle-maintenance-running"), ent, args.Actor);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.Actor, part.InstallTime,
            new UninstallDoAfter(args.Part, args.Slot), ent.Owner, target: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        });
    }

    [SubscribeLocalEvent]
    private void OnUninstallCompleted(EntityUid owner, VehicleModsComponent component, UninstallDoAfter args)
    {
        var ent = new Entity<VehicleModsComponent>(owner, component);
        if (args.Handled || args.Cancelled || IsRunning(ent) || !CanReach(ent, args.User) ||
            !TryGetEntity(args.Part, out var uid) || uid is not { } item || !IsInstalled(ent, args.Slot, item) ||
            !_containers.TryGetContainer(ent.Owner, ent.Comp.ModContainer, out var container) ||
            !_containers.Remove(item, container))
            return;

        _hands.PickupOrDrop(args.User, item);
        args.Handled = true;
    }

    private bool IsRunning(EntityUid vehicle) => CompOrNull<VehicleIgnitionComponent>(vehicle)?.Started == true;

    private bool CanReach(EntityUid vehicle, EntityUid user) =>
        _vehicles.GetOperatorOrNull(vehicle) == user || _interaction.InRangeUnobstructed(user, vehicle);

    private static bool IsInstalled(Entity<VehicleModsComponent> ent, EquipmentType slot, EntityUid item) =>
        ent.Comp.Equipment.TryGetValue(slot, out var installed) && installed == item;

    [SubscribeLocalEvent]
    private void OnPartRemoved(Entity<VehicleModsComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ModContainer || TerminatingOrDeleted(ent))
            return;

        var removed = args.Entity;
        var slot = ent.Comp.Equipment.FirstOrDefault(pair => pair.Value == removed).Key;
        if (slot == EquipmentType.NONE)
            return;

        ent.Comp.Equipment[slot] = null;
        if (TryComp<VehicleEquipmentComponent>(args.Entity, out var part) && !TerminatingOrDeleted(args.Entity))
        {
            DisablePart((args.Entity, part));
            _appearance.SetData(args.Entity, EquipmentVisuals.Hidden, false);
        }

        if (slot == EquipmentType.ENGINE)
            _ignition.Stop(ent);

        RefreshParts(ent);
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnOperatorChanged(Entity<VehicleModsComponent> ent, ref VehicleOperatorSetEvent args)
    {
        foreach (var item in ent.Comp.Equipment.Values)
        {
            if (item is not { } uid || !TryComp<VehicleEquipmentComponent>(uid, out var part))
                continue;

            if (args.OldOperator != null)
                _actions.RemoveAction(part.ActionEntity);

            if (args.NewOperator is { } driver)
                GivePartAction((uid, part), driver);
        }
    }

    private void GivePartAction(Entity<VehicleEquipmentComponent> part, EntityUid driver)
    {
        if (part.Comp.Broken || part.Comp.ActionProto is not { } action)
            return;

        _actions.AddAction(driver, ref part.Comp.ActionEntity, action, part.Owner);
        if (part.Comp.Slot == EquipmentType.LIGHTBAR && part.Comp.ActionEntity is { } siren)
            _actions.SetToggled(siren, _toggle.IsActivated(part.Owner));

        Dirty(part);
    }

    private void RefreshParts(Entity<VehicleModsComponent> ent)
    {
        var engine = WorkingPart(ent, EquipmentType.ENGINE);
        if (ent.Comp.EquipmentSlots.HasFlag(EquipmentType.ENGINE) &&
            TryComp<MovementSpeedModifierComponent>(ent, out var vehicleSpeed))
        {
            var motor = CompOrNull<MovementSpeedModifierComponent>(engine);
            _movement.ChangeBaseSpeed(ent, motor?.BaseWalkSpeed ?? 0, motor?.BaseSprintSpeed ?? 0,
                vehicleSpeed.Acceleration, vehicleSpeed, vehicleSpeed.MinSpeedMod, vehicleSpeed.MaxSpeedMod);

            if (TryComp<PowerCellDrawComponent>(engine, out var electric))
                _powerCell.SetDrawRate(ent.Owner, electric.DrawRate);
            if (HasComp<VehicleIgnitionComponent>(ent) &&
                TryComp<ReagentDrawComponent>(engine, out var gas) &&
                TryComp<ReagentDrawComponent>(ent, out var draw))
            {
                draw.DrainRate = gas.DrainRate;
                Dirty(ent.Owner, draw);
            }
        }

        var armor = CompOrNull<DamageableComponent>(WorkingPart(ent, EquipmentType.ARMOR));
        _damage.SetDamageModifierSetId(ent.Owner, armor?.DamageModifierSetId ?? ent.Comp.OriginalArmor);

        if (ent.Comp.EquipmentSlots.HasFlag(EquipmentType.TIRES))
        {
            var tires = CompOrNull<MovementSpeedModifierComponent>(WorkingPart(ent, EquipmentType.TIRES));
            var friction = tires?.BaseFriction ?? 20f;
            _movement.ChangeBaseFriction(ent, friction, friction, tires?.BaseAcceleration ?? 0.5f);
        }

        _movement.RefreshMovementSpeedModifiers(ent);
        _movement.RefreshFrictionModifiers(ent);
        _movement.RefreshWeightlessModifiers(ent);
    }

    private EntityUid? WorkingPart(Entity<VehicleModsComponent> vehicle, EquipmentType slot)
    {
        return vehicle.Comp.Equipment.TryGetValue(slot, out var part) &&
            TryComp<VehicleEquipmentComponent>(part, out var component) && !component.Broken ? part : null;
    }

    [SubscribeLocalEvent]
    private void OnWeightless(Entity<VehicleModsComponent> ent, ref RefreshWeightlessModifiersEvent args)
    {
        if (!ent.Comp.EquipmentSlots.HasFlag(EquipmentType.THURSTERS))
            return;

        if (TryComp<MovementSpeedModifierComponent>(WorkingPart(ent, EquipmentType.THURSTERS), out var thrusters))
        {
            args.WeightlessAcceleration = thrusters.BaseWeightlessAcceleration;
            args.WeightlessModifier = thrusters.BaseWeightlessModifier;
            args.WeightlessFriction = thrusters.BaseWeightlessFriction;
            args.WeightlessFrictionNoInput = thrusters.BaseWeightlessFriction;
        }
        else
        {
            args.WeightlessAcceleration = 0;
            args.WeightlessModifier = 0;
        }
    }

    [SubscribeLocalEvent]
    private void OnShutoff(Entity<VehicleModsComponent> ent, ref VehicleIgnitionChangedEvent args)
    {
        if (!args.Started && WorkingPart(ent, EquipmentType.VENTFAN) is { } item &&
            TryComp<VehicleFanModComponent>(item, out var fan))
            _atmosphere.SetFanState((item, fan), FanState.Off);
    }

    [SubscribeLocalEvent]
    private void OnHullDamaged(EntityUid owner, VehicleModsComponent component, DamageChangedEvent args)
    {
        var ent = new Entity<VehicleModsComponent>(owner, component);
        if (!args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        foreach (var item in ent.Comp.Equipment.Values.ToArray())
        {
            if (item is { } uid && TryComp<VehicleEquipmentComponent>(uid, out var part) &&
                !part.Broken && _random.Prob(part.DamageChance))
                _damage.TryChangeDamage(uid, delta * part.DamageTransfer, origin: args.Origin);
        }
    }

    [SubscribeLocalEvent]
    private void OnPartBroken(EntityUid owner, VehicleEquipmentComponent component, BreakageEventArgs args)
    {
        var ent = new Entity<VehicleEquipmentComponent>(owner, component);
        if (ent.Comp.Broken)
            return;

        ent.Comp.Broken = true;
        Dirty(ent);
        DisablePart(ent);

        if (!_containers.TryGetContainingContainer(ent.Owner, out var container) ||
            !TryComp<VehicleModsComponent>(container.Owner, out var mods) ||
            mods.ModContainer != container.ID || !IsInstalled((container.Owner, mods), ent.Comp.Slot, ent))
            return;

        if (ent.Comp.Slot == EquipmentType.ENGINE)
            _ignition.Stop(container.Owner);

        RefreshParts((container.Owner, mods));
    }

    private void DisablePart(Entity<VehicleEquipmentComponent> part)
    {
        _actions.RemoveAction(part.Comp.ActionEntity);
        if (TryComp<UnpoweredFlashlightComponent>(part, out var light))
            _flashlight.SetLight((part.Owner, light), false, quiet: true);
        if (TryComp<ItemToggleComponent>(part, out var toggle))
            _toggle.TryDeactivate((part.Owner, toggle));
        if (TryComp<VehicleFanModComponent>(part, out var fan))
            _atmosphere.SetFanState((part.Owner, fan), FanState.Off);
    }

    [SubscribeLocalEvent]
    private void OnToggleSiren(EntityUid owner, VehicleEquipmentComponent component, VehicleSirenToggleEvent args)
    {
        var ent = new Entity<VehicleEquipmentComponent>(owner, component);
        if (args.Handled || ent.Comp.Broken ||
            !_containers.TryGetContainingContainer(ent.Owner, out var container) ||
            _vehicles.GetOperatorOrNull(container.Owner) != args.Performer)
            return;

        args.Handled = _toggle.Toggle(ent.Owner, args.Performer, predicted: false);
        args.Toggle = args.Handled;
    }
}
