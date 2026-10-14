//ReSharper disable CheckNamespace

using Content.Shared.DeviceNetwork.Components;

namespace Content.Shared.DeviceNetwork.Systems;

public abstract partial class SharedDeviceListSystem
{
    /// <summary>
    /// Basically copied from <see cref="NetworkConfiguratorSystem.TryAddNetworkDevice"/>
    /// </summary>
    /// <param name="list">the entity to try and add target to</param>
    /// <param name="target">the entity you are tring to add to the list</param>
    /// <returns></returns>
    public bool TryAddDeviceToList(Entity<DeviceListComponent> list, Entity<DeviceNetworkComponent> target)
    {
        var devList = list.Comp.Devices;
        var device = target.Comp;
        if (!device.SavableAddress)
            return false; //You are in c# you change it.

        if (devList.Contains(target))
            return false; //allready linked

        devList.Add(target);
        Dirty(list);
        return true;
    }
}
