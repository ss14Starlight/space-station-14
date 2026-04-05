using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Silicons.IPC;

[Serializable, NetSerializable]
public enum IPCUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class IPCEjectBrainBuiMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class IPCEjectBatteryBuiMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class IPCSetNameBuiMessage(string name) : BoundUserInterfaceMessage
{
    public string Name = name;
}

[Serializable, NetSerializable]
public sealed class IPCHealthMessage(FixedPoint2 bloodLevel) : BoundUserInterfaceMessage
{
    public FixedPoint2 BloodLevel = bloodLevel;
}
