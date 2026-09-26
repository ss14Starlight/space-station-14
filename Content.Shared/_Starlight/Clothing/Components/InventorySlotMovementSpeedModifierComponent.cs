using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Clothing.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class InventorySlotMovementSpeedModifierComponent : Component
{
    /// A list of data used to determine affected slots and their associated movement speed modifiers.
    [DataField, AutoNetworkedField] public List<SlotSpeedModifierData> SlotData;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class SlotSpeedModifierData
{
    /// <see cref="SlotFlags"/> that are tracked for this speed modifier.
    [DataField("flags")] public SlotFlags AffectedFlags = SlotFlags.NONE;

    /// If <see langword="true"/>, applies the speed modifier when clothing is NOT present in the affected slot flags.
    [DataField] public bool Inverted;

    /// Modifier that will be applied to movement speed when active.
    [DataField] public float SpeedMod;
}
