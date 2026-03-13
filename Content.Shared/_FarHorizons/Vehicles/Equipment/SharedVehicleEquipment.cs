using Robust.Shared.Serialization;
using Content.Shared._FarHorizons.Vehicles.Components;
using Content.Shared.DoAfter;

namespace Content.Shared._FarHorizons.Vehicles.Equipment;

[Serializable, NetSerializable]
public enum VehicleEquipmentUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class UninstallPartMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Part;
    public readonly EquipmentType Slot;
    public UninstallPartMessage(NetEntity part, EquipmentType slot)
    {
        Part = part;
        Slot = slot;
    }
}

[Serializable, NetSerializable]
public sealed partial class UninstallDoAfter : SimpleDoAfterEvent
{
    public readonly NetEntity Part;
    public EquipmentType Slot;
    public UninstallDoAfter(NetEntity part, EquipmentType slot)
    {
        Part = part;
        Slot = slot;
    }
}

[Serializable, NetSerializable]
public sealed partial class InstallDoAfter : SimpleDoAfterEvent
{
    public readonly NetEntity Part;
    public InstallDoAfter(NetEntity part)
        => Part = part;
}