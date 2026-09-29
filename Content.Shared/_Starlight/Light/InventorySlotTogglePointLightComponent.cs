using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Light;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class InventorySlotTogglePointLightComponent : Component
{
    /// <summary>
    /// The <see cref="SharedPointLightComponent"/> is toggled OFF when clothing is equipped to this slot,
    /// and turned ON when unequipped.
    /// </summary>
    /// <remarks>
    /// <see cref="OnSlots"/> takes priority over this.
    /// </remarks>
    [DataField, AutoNetworkedField] public SlotFlags OffSlots = SlotFlags.NONE;

    /// <summary>
    /// The <see cref="SharedPointLightComponent"/> is toggled ON when clothing is equipped to this slot,
    /// and turned OFF when unequipped.
    /// </summary>
    /// <remarks>
    /// This takes priority over <see cref="OffSlots"/>.
    /// </remarks>
    [DataField, AutoNetworkedField] public SlotFlags OnSlots = SlotFlags.NONE;

    /// The default state of the <see cref="SharedPointLightComponent"/> when no applicable clothing is equipped.
    [DataField, AutoNetworkedField] public bool DefaultState = true;
}
