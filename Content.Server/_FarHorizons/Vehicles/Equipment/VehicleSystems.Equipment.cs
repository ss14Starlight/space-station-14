using Content.Shared._FarHorizons.Vehicles.Components;
using Content.Shared.Actions;
using Content.Shared.Light.Components;
using Content.Shared.Coordinates;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Toggleable;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Components;
using Robust.Shared.Timing;
using Content.Shared.PowerCell.Components;
using Content.Shared.PowerCell;
using Content.Shared._FarHorizons.ReagantDraw.Components;
using Content.Shared.UserInterface;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Server._FarHorizons.Vehicles.Atmos;
using Content.Shared._FarHorizons.Vehicles.Events;
using Content.Shared._FarHorizons.Vehicles.Equipment;
using Robust.Shared.Utility;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Destructible;
using Robust.Shared.Random;
using Content.Server.Destructible;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;

namespace Content.Server._FarHorizons.Vehicles.Equipment;
public sealed partial class VehicleEquipmentSystems : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly VehicleAtmosphereSystem _vAtmos = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private float _frictionModifier;
    private float _airfrictionModifier;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleModsComponent, ComponentStartup>(OnCompStartup);

        SubscribeLocalEvent<VehicleModsComponent, InteractUsingEvent>(OnInstallAttempt);
        SubscribeLocalEvent<VehicleModsComponent, InstallDoAfter>(OnInstallDoAfter);
        SubscribeLocalEvent<VehicleEquipmentComponent, InstalledVehicleEquipment>(OnVehicleEquipmentInstalled);
        SubscribeLocalEvent<MovementSpeedModifierComponent, InstalledVehicleEquipment>(OnMovementInstalled);
        SubscribeLocalEvent<PowerCellDrawComponent, InstalledVehicleEquipment>(OnElectricEngineInstalled);
        SubscribeLocalEvent<ReagantDrawComponent, InstalledVehicleEquipment>(OnGasEngineInstalled);
        SubscribeLocalEvent<DamageableComponent, InstalledVehicleEquipment>(OnArmorInstalled);
        SubscribeLocalEvent<PointLightComponent, InstalledVehicleEquipment>(OnLightInstalled);

        SubscribeLocalEvent<VehicleModsComponent, UninstallPartMessage>(OnUninstallPart);
        SubscribeLocalEvent<VehicleModsComponent, UninstallDoAfter>(OnUninstallDoAfter);
        SubscribeLocalEvent<VehicleEquipmentComponent, UnInstalledVehicleEquipment>(OnVehicleEquipmentUnInstalled);
        SubscribeLocalEvent<MovementSpeedModifierComponent, UnInstalledVehicleEquipment>(OnMovementUnInstalled);
        SubscribeLocalEvent<PowerCellDrawComponent, UnInstalledVehicleEquipment>(OnElectricEngineUnInstalled);
        SubscribeLocalEvent<ReagantDrawComponent, UnInstalledVehicleEquipment>(OnGasEngineUnInstalled);
        SubscribeLocalEvent<DamageableComponent, UnInstalledVehicleEquipment>(OnArmorUnInstalled);
        SubscribeLocalEvent<PointLightComponent, UnInstalledVehicleEquipment>(OnLightUnInstalled);

        SubscribeLocalEvent<RiderComponent, AddRiderActions>(OnAddActions);
        SubscribeLocalEvent<RiderComponent, RemoveRiderActions>(OnRemoveActions);
        SubscribeLocalEvent<ItemToggleComponent, ToggleActionEvent>(OnSirenToggle);
        SubscribeLocalEvent<VehicleComponent, ToggleIntrinsicUIEvent>(OnActionToggle);
        SubscribeLocalEvent<VehicleEquipmentComponent, GetItemActionsEvent>(OnGetActions);

        SubscribeLocalEvent<VehicleModsComponent, GridUidChangedEvent>(GridUiChanged);
        SubscribeLocalEvent<VehicleModsComponent, RefreshFrictionModifiersEvent>(OnFrictionRefresh);
        SubscribeLocalEvent<VehicleModsComponent, RefreshWeightlessModifiersEvent>(OnWeightlessRefresh);
        SubscribeLocalEvent<VehicleModsComponent, TurnOffVehicleEvent>(OnVehicleShutoff);
        SubscribeLocalEvent<VehicleModsComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<VehicleEquipmentComponent, BreakageEventArgs>(OnBreakageEvent);

        Subs.CVar(_configManager, CCVars.TileFrictionModifier, value => _frictionModifier = value, true);
        Subs.CVar(_configManager, CCVars.AirFriction, value => _airfrictionModifier = value, true);
    }

    private void OnCompStartup(Entity<VehicleModsComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.ModSlot = _container.EnsureContainer<Container>(ent.Owner, ent.Comp.ModContainer);

        var slots = ent.Comp.EquipmentSlots.GetFlags().ToArray();
        foreach(var slot in slots)
        {
            ent.Comp.Equipment.Add(slot, null);
        }
    
        if(ent.Comp.StartingEquipment.Count > 0)
        {
            foreach(var itemProto in ent.Comp.StartingEquipment)
            {
                if (_proto.TryIndex<EntityPrototype>(itemProto, out var proto))
                    if (!proto.Components.ContainsKey("VehicleEquipment"))
                        continue;
                
                var item = SpawnAtPosition(itemProto, ent.Owner.ToCoordinates());
                if(!CheckandAssign(item, ent.Comp))
                    QueueDel(item);
                    
                if(HasComp<PointLightComponent>(item))
                {
                    _transform.SetParent(item, ent.Owner);
                    _transform.SetLocalRotation(item, Angle.Zero);
                }
                else
                    _container.Insert(item, ent.Comp.ModSlot);
                ent.Comp.SpawnedEquipment.Add(item);
                var ev = new InstalledVehicleEquipment{Vehicle =  ent.Owner};
                RaiseLocalEvent(item, ev);
            }
        }
        Dirty(ent.Owner, ent.Comp);
    }

    #region Install Section
    private void OnInstallAttempt(Entity<VehicleModsComponent> ent, ref InteractUsingEvent args)
    {
        if(!args.Handled 
            && TryComp<VehicleEquipmentComponent>(args.Used, out var veComp) 
            && TryComp<VehicleComponent>(ent.Owner, out var vehicle))
        {
            if(vehicle.Started)
            {
                _popupSystem.PopupCursor("Turn off vehicle before performing any maintenance.", args.User, PopupType.SmallCaution);
                return;
            }
            _audio.PlayPredicted(new SoundPathSpecifier("/Audio/Items/drill_use.ogg"), ent.Owner, null);
            var installEV = new InstallDoAfter(GetNetEntity(args.Used));
            var installDoAfter = new DoAfterArgs(EntityManager, args.User, veComp.InstallandRemoveTime, installEV, ent.Owner)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = true
            };
            _doAfter.TryStartDoAfter(installDoAfter);
            args.Handled = true;   
        }
    }

    private void OnInstallDoAfter(Entity<VehicleModsComponent> ent, ref InstallDoAfter args)
    {
        if(args.Cancelled) return;
        var part = GetEntity(args.Part);
        if (part is { } uid)
        {
            if(CheckandAssign(uid, ent.Comp))
                if(HasComp<PointLightComponent>(part))
                {
                    _transform.SetParent(part, ent.Owner);
                    _transform.SetLocalRotation(part, Angle.Zero);
                }
                else
                    _container.Insert(part, ent.Comp.ModSlot);

            var ev = new InstalledVehicleEquipment{Vehicle =  ent.Owner};
            RaiseLocalEvent(uid, ev);
            Dirty(ent.Owner, ent.Comp);
        }
    }

    private void OnVehicleEquipmentInstalled(Entity<VehicleEquipmentComponent> ent, ref InstalledVehicleEquipment args)
    {
            _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.ActionProto);
            Dirty(ent.Owner, ent.Comp);
            
            if(ent.Comp.ActionEntity == null)
                return;
            if(TryComp<DestructibleComponent>(ent.Owner, out var destructibleComp) && destructibleComp.IsBroken)
                return;
            var xForm = Transform(ent.Owner);
            if(!TryComp<VehicleComponent>(xForm.ParentUid, out var vehicle) || vehicle.Rider == null)
                return;
            _actions.GrantContainedAction(vehicle.Rider.Value, ent.Owner, ent.Comp.ActionEntity.Value);
    }

    private void OnMovementInstalled(Entity<MovementSpeedModifierComponent> ent, ref InstalledVehicleEquipment args)
    {
        if(!TryComp<VehicleEquipmentComponent>(ent.Owner, out var veComp) 
            || !TryComp<MovementSpeedModifierComponent>(args.Vehicle, out var msmComp)) return;
        var Vehicle = args.Vehicle;
        Timer.Spawn(0, () =>
        {
            switch(veComp.Slot)
            {
                case EquipmentType.TIRES:
                    _movementSpeed.RefreshFrictionModifiers(Vehicle);
                    break;
                case EquipmentType.ENGINE:
                    _movementSpeed.ChangeBaseSpeed(Vehicle, ent.Comp.BaseWalkSpeed, ent.Comp.BaseSprintSpeed, msmComp.Acceleration);
                    break;
                case EquipmentType.THURSTERS:
                    _meta.AddFlag(Vehicle, MetaDataFlags.ExtraTransformEvents);
                    break;
            }
        });
    }

    private void OnElectricEngineInstalled(Entity<PowerCellDrawComponent> ent, ref InstalledVehicleEquipment args) 
        => _powerCell.SetDrawRate(args.Vehicle, ent.Comp.DrawRate);

    private void OnGasEngineInstalled(Entity<ReagantDrawComponent> ent, ref InstalledVehicleEquipment args)
    {
        if(!TryComp<ReagantDrawComponent>(args.Vehicle, out var rdComp)) return;
        rdComp.DrainRate = ent.Comp.DrainRate;
        Dirty(args.Vehicle, rdComp);
    }

    private void OnArmorInstalled(Entity<DamageableComponent> ent, ref InstalledVehicleEquipment args)
        => _damage.SetDamageModifierSetId(args.Vehicle, ent.Comp.DamageModifierSetId);

    private void OnLightInstalled(Entity<PointLightComponent> ent, ref InstalledVehicleEquipment args)
    {
        var xForm = Transform(ent.Owner);
        if(HasComp<VehicleModsComponent>(xForm.ParentUid))
            _appearance.SetData(ent.Owner, EquipmentVisuals.Hidden, true);
    }

    #endregion

    #region Uninstall Section
    private void OnUninstallPart(Entity<VehicleModsComponent> ent, ref UninstallPartMessage args)
    {
        var part = GetEntity(args.Part);
        if(!TryComp<VehicleEquipmentComponent>(part, out var veComp) || !TryComp<VehicleComponent>(ent.Owner, out var vehicle))
            return;
        
        if(vehicle.Started)
        {
            _popupSystem.PopupCursor("Turn off vehicle before performing any maintenance.", args.Actor, PopupType.SmallCaution);
            return;
        }

        _audio.PlayPredicted(new SoundPathSpecifier("/Audio/Items/drill_use.ogg"), ent.Owner, null);
        var uninstallEV = new UninstallDoAfter(args.Part, args.Slot);
        var uninstallDoAfter = new DoAfterArgs(EntityManager, args.Actor, veComp.InstallandRemoveTime, uninstallEV, ent.Owner)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = true
        };
        _doAfter.TryStartDoAfter(uninstallDoAfter);
    }

    private void OnUninstallDoAfter(Entity<VehicleModsComponent> ent, ref UninstallDoAfter args)
    {
        if(args.Cancelled) return;
        var part = GetEntity(args.Part);
        if (part is { } uid)
        {
            _container.Remove(uid, ent.Comp.ModSlot);
            _hands.TryForcePickupAnyHand(args.User, uid);
            ent.Comp.Equipment[args.Slot] = null;
            Dirty(ent.Owner, ent.Comp);

            var ev = new UnInstalledVehicleEquipment{Vehicle =  ent.Owner};
            RaiseLocalEvent(part, ev);
        }
    }

    private void OnVehicleEquipmentUnInstalled(Entity<VehicleEquipmentComponent> ent, ref UnInstalledVehicleEquipment args)
    {
        if(ent.Comp.ActionEntity == null)
            return;

        var xForm = Transform(ent.Owner);
        if(TryComp<VehicleComponent>(xForm.ParentUid, out var vehicle) && vehicle.Rider != null)
            _actions.GrantContainedAction(vehicle.Rider.Value, ent.Owner, ent.Comp.ActionEntity.Value);

        _actions.RemoveAction(ent.Comp.ActionEntity.Value);
        ent.Comp.ActionEntity = null;
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnMovementUnInstalled(Entity<MovementSpeedModifierComponent> ent, ref UnInstalledVehicleEquipment args)
    {
        if(!TryComp<VehicleEquipmentComponent>(ent.Owner, out var veComp) 
            || !TryComp<MovementSpeedModifierComponent>(args.Vehicle, out var msmComp)) return;
        var Vehicle = args.Vehicle;
        Timer.Spawn(0, () =>
        {
            switch(veComp.Slot)
            {
                case EquipmentType.TIRES:
                    _movementSpeed.RefreshFrictionModifiers(Vehicle);
                    break;
                case EquipmentType.ENGINE:
                    _movementSpeed.ChangeBaseSpeed(Vehicle, 0, 0, msmComp.Acceleration);
                    break;
                case EquipmentType.THURSTERS:
                    _meta.RemoveFlag(Vehicle, MetaDataFlags.ExtraTransformEvents);
                    break;
            }
        });
    }

    private void OnElectricEngineUnInstalled(Entity<PowerCellDrawComponent> ent, ref UnInstalledVehicleEquipment args) 
        => _powerCell.SetDrawRate(args.Vehicle, 999999);

    private void OnGasEngineUnInstalled(Entity<ReagantDrawComponent> ent, ref UnInstalledVehicleEquipment args)
    {
        if(!TryComp<ReagantDrawComponent>(args.Vehicle, out var rdComp)) return;
        rdComp.DrainRate = 999999;
        Dirty(args.Vehicle, rdComp);
    }

    private void OnArmorUnInstalled(Entity<DamageableComponent> ent, ref UnInstalledVehicleEquipment args)
        => _damage.SetDamageModifierSetId(args.Vehicle, null);

    private void OnLightUnInstalled(Entity<PointLightComponent> ent, ref UnInstalledVehicleEquipment args)
    {
        var xForm = Transform(ent.Owner);
        if(!HasComp<VehicleModsComponent>(xForm.ParentUid))
            _appearance.SetData(ent.Owner, EquipmentVisuals.Hidden, false);
    }

    #endregion

    #region Actions
    private void OnAddActions(Entity<RiderComponent> ent, ref AddRiderActions args)
    {
        if(ent.Comp.Riding == null) return;
        var vehicle = ent.Comp.Riding.Value;
        if(!TryComp<VehicleModsComponent>(vehicle, out var vmComp) || vmComp.SpawnedEquipment.Count == 0) return;
        foreach(var item in vmComp.SpawnedEquipment)
        {
            if(!TryComp<VehicleEquipmentComponent>(item, out var veComp) || veComp.ActionEntity == null)
                continue;
            if(TryComp<DestructibleComponent>(item, out var destructibleComp) && destructibleComp.IsBroken)
                continue;
            _actions.GrantContainedAction(ent.Owner, item, veComp.ActionEntity.Value);
        }
    }
    private void OnRemoveActions(Entity<RiderComponent> ent, ref RemoveRiderActions args)
    {
        if(ent.Comp.Riding == null) return;
        var vehicle = ent.Comp.Riding.Value;
        if(!TryComp<VehicleModsComponent>(vehicle, out var vmComp) || vmComp.SpawnedEquipment.Count == 0) return;
        foreach(var item in vmComp.SpawnedEquipment)
        {
            if(!TryComp<VehicleEquipmentComponent>(item, out var veComp) || veComp.ActionEntity == null)
                continue;
            _actions.RemoveProvidedAction(ent.Owner, item, veComp.ActionEntity.Value);
        }
    }

    private void OnSirenToggle(Entity<ItemToggleComponent> ent, ref ToggleActionEvent args)
    {
        if(args.Handled) return;
        if(!TryComp<UnpoweredFlashlightComponent>(ent.Owner, out var flashComp) || !HasComp<ItemToggleComponent>(ent.Owner)) return;
        flashComp.LightOn = !flashComp.LightOn; 
        var toggleUsed = new ItemToggledEvent(false, Activated: flashComp.LightOn, args.Performer);
        RaiseLocalEvent(ent.Owner, ref toggleUsed);
        args.Handled = true;
    }

    private void OnActionToggle(EntityUid uid, VehicleComponent component, ToggleIntrinsicUIEvent args)
    {
        if (args.Key == null)
            return;
        if(component.Rider == null)
            return;
        args.Handled = _ui.TryToggleUi(uid, args.Key, component.Rider.Value);
    }

    private void OnGetActions(Entity<VehicleEquipmentComponent> ent, ref GetItemActionsEvent args)
    {
        var xForm = Transform(ent.Owner);
        if(!HasComp<VehicleModsComponent>(xForm.ParentUid))
            args.Cancel();
    }
    #endregion

    #region Functions

    private bool CheckandAssign(EntityUid item, VehicleModsComponent vmComp, VehicleEquipmentComponent? veComp=null)
    {
        if(!Resolve(item, ref veComp))
            return false;

        if((veComp.AllowedVehicles & vmComp.VehicleType) == 0)
            return false;

        foreach(var slot in vmComp.Equipment)
        {
            if((slot.Key & veComp.Slot) != 0 && slot.Value == null)
            {
                vmComp.Equipment[slot.Key] = item;
                return true;
            }
        }
        return false;
    }
    #endregion

    #region Events
    private void OnFrictionRefresh(Entity<VehicleModsComponent> ent, ref RefreshFrictionModifiersEvent args)
    {
        if(!TryComp<DestructibleComponent>(ent.Owner, out var destructible))
            return;

        if(ent.Comp.Equipment[EquipmentType.TIRES] == null || destructible.IsBroken)
        {
            args.Acceleration = 0.5f;
            args.Friction = 16/_frictionModifier;
            args.FrictionNoInput = 16/_frictionModifier; 
        }
        else
        {
            if(!TryComp<MovementSpeedModifierComponent>(ent.Comp.Equipment[EquipmentType.TIRES], out var msmComp)) return;
            args.Acceleration = msmComp.BaseAcceleration;
            args.Friction = msmComp.BaseFriction/_frictionModifier;
            args.FrictionNoInput = msmComp.BaseFriction/_frictionModifier;   
        }
    }

    private void OnWeightlessRefresh(Entity<VehicleModsComponent> ent, ref RefreshWeightlessModifiersEvent args)
    {
        if(ent.Comp.Equipment[EquipmentType.THURSTERS] == null 
            || !TryComp<MovementSpeedModifierComponent>(ent.Comp.Equipment[EquipmentType.THURSTERS], out var msmComp)) return;
        var xForm = Transform(ent.Owner);
        if(xForm.GridUid == null)
        {
            args.WeightlessAcceleration = msmComp.BaseWeightlessAcceleration;
            args.WeightlessModifier = msmComp.BaseWeightlessModifier;
            args.WeightlessFriction = msmComp.BaseWeightlessFriction/_airfrictionModifier;
            args.WeightlessFrictionNoInput = msmComp.BaseWeightlessFriction/_airfrictionModifier;
        }
    }

    private void OnVehicleShutoff(Entity<VehicleModsComponent> ent, ref TurnOffVehicleEvent args)
    {
        if(TryComp<VehicleFanModComponent>(ent.Comp.Equipment[EquipmentType.VENTFAN], out var fanComp))
            _vAtmos.SetFanState(ent, fanComp, FanState.Off);
    }

    private void GridUiChanged(Entity<VehicleModsComponent> ent, ref GridUidChangedEvent args)
        => _movementSpeed.RefreshWeightlessModifiers(ent.Owner);

    private void OnDamageChanged(Entity<VehicleModsComponent> ent, ref DamageChangedEvent args)
    {
        if(!args.DamageIncreased || args.DamageDelta == null) return;
        if(ent.Comp.Equipment.Count > 0)
        {
            foreach (var (slot, item) in ent.Comp.Equipment)
            {
                if(item == null || !TryComp<VehicleEquipmentComponent>(item, out var veComp))
                    continue;
                if(_random.Prob(veComp.damageChance))
                    continue;

                _damage.TryChangeDamage(item.Value, args.DamageDelta*veComp.damageTransfer, true);
            }
        }
    }

    private void OnBreakageEvent(Entity<VehicleEquipmentComponent> ent, ref BreakageEventArgs args)
    {
        var xForm = Transform(ent.Owner);
        if(xForm.GridUid == xForm.ParentUid)
            return;
         
        if(ent.Comp.ActionEntity != null && TryComp<VehicleComponent>(ent.Owner, out var vehicleComp))
        {
            if(vehicleComp.Rider != null)
                _actions.RemoveAction(vehicleComp.Rider.Value, ent.Comp.ActionEntity);   
            QueueDel(ent.Comp.ActionEntity);
            ent.Comp.ActionEntity = null;
            Dirty(ent.Owner, ent.Comp);
        }

        if(TryComp<DestructibleComponent>(ent.Owner, out var destructible))
            destructible.IsBroken = true;

        var ev = new UnInstalledVehicleEquipment{Vehicle =  xForm.ParentUid};
        RaiseLocalEvent(ent.Owner, ev);
    }

    #endregion
}