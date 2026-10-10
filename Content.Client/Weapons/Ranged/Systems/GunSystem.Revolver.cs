using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [SubscribeLocalEvent]
    private void OnRevolverEntRemove(Entity<RevolverAmmoProviderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != RevolverContainer)
            return;

        // <See ChamberMagazineAmmoProvider>
        if (!IsClientSide(args.Entity))
            return;

        QueueDel(args.Entity);
    }

    [SubscribeLocalEvent]
    private void OnRevolverAmmoUpdate(Entity<RevolverAmmoProviderComponent> ent, ref UpdateAmmoCounterEvent args)
    {
        if (args.Control is not RevolverStatusControl control) return;
        control.Update(ent.Comp.CurrentIndex, ent.Comp.Chambers);
    }

    [SubscribeLocalEvent]
    private void OnRevolverCounter(Entity<RevolverAmmoProviderComponent> ent, ref AmmoCounterControlEvent args)
    {
        args.Control = new RevolverStatusControl();
    }
}
