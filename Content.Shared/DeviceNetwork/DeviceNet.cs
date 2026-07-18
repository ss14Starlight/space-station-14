using Robust.Shared.Random;
using Content.Shared.DeviceNetwork.Components;

namespace Content.Shared.DeviceNetwork;

/// <summary>
///     Data class for storing and retrieving information about devices connected to a device network.
/// </summary>
/// <remarks>
///     This basically just makes <see cref="DeviceNetworkComponent"/> accessible via their addresses and frequencies on
///     some network.
/// </remarks>
public sealed class DeviceNet
{
    /// <summary>
    ///     Devices, mapped by their "Address", which is just an int that gets converted to Hex for displaying to users.
    ///     This dictionary contains all devices connected to this network, though they may not be listening to any
    ///     specific frequency.
    /// </summary>
    public readonly Dictionary<string, Entity<DeviceNetworkComponent>> Devices = new();

    /// <summary>
    ///     Devices listening on a given frequency.
    /// </summary>
    public readonly Dictionary<uint, HashSet<Entity<DeviceNetworkComponent>>> ListeningDevices = new();

    /// <summary>
    ///     Devices listening to all packets on a given frequency, regardless of the intended recipient.
    /// </summary>
    public readonly Dictionary<uint, HashSet<Entity<DeviceNetworkComponent>>> ReceiveAllDevices = new();

    private readonly IRobustRandom _random;
    public readonly int NetId;

    public DeviceNet(int netId, IRobustRandom random)
    {
        _random = random;
        NetId = netId;
    }

    /// <summary>
    ///     Add a device to the network.
    /// </summary>
    public bool Add(Entity<DeviceNetworkComponent> device)
    {
        if (device.Comp.CustomAddress)
        {
            // Only add if the device's existing address is available.
            if (!Devices.TryAdd(device.Comp.Address, device))
                return false;
        }
        else
        {
            // Randomly generate a new address if the existing random one is invalid. Otherwise, keep the existing address
            if (string.IsNullOrWhiteSpace(device.Comp.Address) || Devices.ContainsKey(device.Comp.Address))
                device.Comp.Address = GenerateValidAddress(device.Comp.Prefix);

            Devices[device.Comp.Address] = device;
        }

        if (device.Comp.ReceiveFrequency is not uint freq)
            return true;

        if (!ListeningDevices.TryGetValue(freq, out var devices))
            ListeningDevices[freq] = devices = new();

        devices.Add(device);

        if (!device.Comp.ReceiveAll)
            return true;

        if (!ReceiveAllDevices.TryGetValue(freq, out var receiveAlldevices))
            ReceiveAllDevices[freq] = receiveAlldevices = new();

        receiveAlldevices.Add(device);
        return true;
    }

    /// <summary>
    ///     Remove a device from the network.
    /// </summary>
    public bool Remove(Entity<DeviceNetworkComponent> device)
    {
        if (device.Comp.Address == null || !Devices.Remove(device.Comp.Address))
            return false;

        if (device.Comp.ReceiveFrequency is not uint freq)
            return true;

        if (ListeningDevices.TryGetValue(freq, out var listening))
        {
            listening.Remove(device);
            if (listening.Count == 0)
                ListeningDevices.Remove(freq);
        }

        if (device.Comp.ReceiveAll && ReceiveAllDevices.TryGetValue(freq, out var receiveAll))
        {
            receiveAll.Remove(device);
            if (receiveAll.Count == 0)
                ListeningDevices.Remove(freq);
        }

        return true;
    }

    /// <summary>
    ///     Give an existing device a new randomly generated address. Useful if the device's address prefix was updated
    ///     and they want a new address to reflect that, or something like that.
    /// </summary>
    public bool RandomizeAddress(string oldAddress, string? prefix = null)
    {
        if (!Devices.Remove(oldAddress, out var device))
            return false;

        device.Comp.Address = GenerateValidAddress(prefix ?? device.Comp.Prefix);
        device.Comp.CustomAddress = false;
        Devices[device.Comp.Address] = device;
        return true;
    }

    /// <summary>
    ///     Update the address of an existing device.
    /// </summary>
    public bool UpdateAddress(string oldAddress, string newAddress)
    {
        if (Devices.ContainsKey(newAddress))
            return false;

        if (!Devices.Remove(oldAddress, out var device))
            return false;

        device.Comp.Address = newAddress;
        device.Comp.CustomAddress = true;
        Devices[newAddress] = device;
        return true;
    }

    /// <summary>
    ///     Make an existing network device listen to a new frequency.
    /// </summary>
    public bool UpdateReceiveFrequency(string address, uint? newFrequency)
    {
        if (!Devices.TryGetValue(address, out var device))
            return false;

        if (device.Comp.ReceiveFrequency == newFrequency)
            return true;

        if (device.Comp.ReceiveFrequency is uint freq)
        {
            if (ListeningDevices.TryGetValue(freq, out var listening))
            {
                listening.Remove(device);
                if (listening.Count == 0)
                    ListeningDevices.Remove(freq);
            }

            if (device.Comp.ReceiveAll && ReceiveAllDevices.TryGetValue(freq, out var receiveAll))
            {
                receiveAll.Remove(device);
                if (receiveAll.Count == 0)
                    ListeningDevices.Remove(freq);
            }
        }

        device.Comp.ReceiveFrequency = newFrequency;

        if (newFrequency == null)
            return true;

        if (!ListeningDevices.TryGetValue(newFrequency.Value, out var devices))
            ListeningDevices[newFrequency.Value] = devices = new();

        devices.Add(device);

        if (!device.Comp.ReceiveAll)
            return true;

        if (!ReceiveAllDevices.TryGetValue(newFrequency.Value, out var receiveAlldevices))
            ReceiveAllDevices[newFrequency.Value] = receiveAlldevices = new();

        receiveAlldevices.Add(device);
        return true;
    }

    /// <summary>
    ///     Make an existing network device listen to a new frequency.
    /// </summary>
    public bool UpdateReceiveAll(string address, bool receiveAll)
    {
        if (!Devices.TryGetValue(address, out var device))
            return false;

        if (device.Comp.ReceiveAll == receiveAll)
            return true;

        device.Comp.ReceiveAll = receiveAll;

        if (device.Comp.ReceiveFrequency is not uint freq)
            return true;

        // remove or add to set of listening devices

        HashSet<Entity<DeviceNetworkComponent>>? devices;
        if (receiveAll)
        {
            if (!ReceiveAllDevices.TryGetValue(freq, out devices))
                ReceiveAllDevices[freq] = devices = new();
            devices.Add(device);
        }
        else if (ReceiveAllDevices.TryGetValue(freq, out devices))
        {
            devices.Remove(device);
            if (devices.Count == 0)
                ReceiveAllDevices.Remove(freq);
        }

        return true;
    }

    /// <summary>
    ///     Generates a valid address by randomly generating one and checking if it already exists on the network.
    /// </summary>
    private string GenerateValidAddress(string? prefix)
    {
        prefix = string.IsNullOrWhiteSpace(prefix) ? null : Loc.GetString(prefix);
        string address;
        do
        {
            var num = _random.Next();
            address = $"{prefix}{num >> 16:X4}-{num & 0xFFFF:X4}";
        }
        while (Devices.ContainsKey(address));

        return address;
    }
}
