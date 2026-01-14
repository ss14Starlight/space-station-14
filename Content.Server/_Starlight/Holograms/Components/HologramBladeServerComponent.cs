namespace Content.Server._Starlight.Holograms;

/// <summary>
///     A blade server that stores hologram data via brain and body chips.
///     Consoles read from these servers to populate the hologram list.
/// </summary>
[RegisterComponent]
public sealed partial class HologramBladeServerComponent : Component
{
    /// <summary>
    ///     Slot IDs for the brain chip.
    /// </summary>
    [DataField]
    public string BrainChipSlot = "hologram_brain_chip";

    /// <summary>
    ///     Slot ID for the body chip.
    /// </summary>
    [DataField]
    public string BodyChipSlot = "hologram_body_chip";

    /// <summary>
    ///     Whether this blade server is currently powered and functional.
    /// </summary>
    [ViewVariables]
    public bool IsPowered = false;
}
