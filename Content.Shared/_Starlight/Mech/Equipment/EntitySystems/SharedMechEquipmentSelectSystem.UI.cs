
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Mech.Equipment.EntitySystems;

/// <summary>
/// Sent when an active mech equipment has been selected
/// </summary>
/// <param name="selectedEquipment"></param>
[Serializable, NetSerializable]
public sealed class MechActiveEquipmentSelectMessage(NetEntity? selectedEquipment) : BoundUserInterfaceMessage
{
    /// <summary>
    /// The uid of the equipment to select
    /// </summary>
    public readonly NetEntity? SelectedEquipment = selectedEquipment;
}

[Serializable, NetSerializable]
public enum MechEquipmentSelectUiKey : byte
{
    Key,
}
