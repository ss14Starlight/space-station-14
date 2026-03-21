using Content.Shared.Power;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Content.Shared._Starlight.Weapons.Ranged.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Containers;
using Content.Shared.Mech.EntitySystems;

namespace Content.Shared.Weapons.Ranged.Systems; //Wrong namespace for this, but it needs to be in the same namespace as GunSystem

public abstract partial class SharedGunSystem
{
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedMechSystem _mech = default!;
    protected virtual void InitializeMech()
    {
        SubscribeLocalEvent<MechAmmoProviderComponent, EntGotInsertedIntoContainerMessage>(OnGunInstalled);
        SubscribeLocalEvent<MechAmmoProviderComponent, EntGotRemovedFromContainerMessage>(OnGunRemoved);
        SubscribeLocalEvent<MechAmmoProviderComponent, TakeAmmoEvent>(OnMechTakeAmmo);
        SubscribeLocalEvent<MechAmmoProviderComponent, GetAmmoCountEvent>(OnMechAmmoCount);
    }

    private void OnGunInstalled(Entity<MechAmmoProviderComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (HasComp<MechComponent>(args.Container.Owner))
        {
            ent.Comp.Mech = args.Container.Owner;
            Dirty(ent, ent.Comp);
        }
    }

    private void OnGunRemoved(Entity<MechAmmoProviderComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        ent.Comp.Mech = null;
        Dirty(ent, ent.Comp);
    }

    private void OnMechTakeAmmo(Entity<MechAmmoProviderComponent> ent, ref TakeAmmoEvent args)
    {
        if (!ent.Comp.Mech.HasValue || !TryComp(ent.Comp.Mech.Value, out MechComponent? mechComp))
            return;
        
        if (mechComp.BatterySlot.ContainedEntity != null &&
            TryComp(mechComp.BatterySlot.ContainedEntity.Value, out BatteryComponent? batteryComp))
        {
            var shots = Math.Min(_battery.GetRemainingUses(mechComp.BatterySlot.ContainedEntity.Value , ent.Comp.FireCost), args.Shots);

            for (var i = 0; i < shots; i++)
            {
                if(!_mech.TryChangeEnergy(ent.Comp.Mech.Value, ent.Comp.FireCost))
                    break;
                    
                args.Ammo.Add(GetShootable(ent, args.Coordinates));
            }
        }
    }

    private (EntityUid? Entity, IShootable) GetShootable(MechAmmoProviderComponent component, EntityCoordinates coordinates)
    {

        var ent = Spawn(component.Prototype, coordinates);
        return (ent, EnsureShootable(ent));
    }

    private void OnMechAmmoCount(Entity<MechAmmoProviderComponent> ent, ref GetAmmoCountEvent args)
    {
        if (!ent.Comp.Mech.HasValue || !TryComp(ent.Comp.Mech.Value, out MechComponent? mechComp))
            return;
        
        if (mechComp.BatterySlot.ContainedEntity != null &&
            TryComp(mechComp.BatterySlot.ContainedEntity.Value, out BatteryComponent? batteryComp))
        {
            args.Count = _battery.GetRemainingUses(mechComp.BatterySlot.ContainedEntity.Value , ent.Comp.FireCost);
            args.Capacity = _battery.GetMaxUses(mechComp.BatterySlot.ContainedEntity.Value , ent.Comp.FireCost);
        }
    }
}
