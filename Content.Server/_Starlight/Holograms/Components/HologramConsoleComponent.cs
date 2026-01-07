namespace Content.Server._Starlight.Holograms.Components;

/// <summary>
/// Console that allows hologram disk management and projector selection.
/// Supports both stationary and portable (briefcase) modes.
/// </summary>
[RegisterComponent]
public sealed partial class HologramConsoleComponent : Component
{
    /// <summary>
    /// The hologram server this console is linked to
    /// </summary>
    [DataField("linkedServer")]
    public EntityUid? LinkedServer;

    /// <summary>
    /// Maximum range to search for a server if not linked
    /// </summary>
    [DataField("searchRange")]
    public float SearchRange = 5f;

    /// <summary>
    /// Container slot ID for the hologram disk
    /// </summary>
    [DataField("diskSlot")]
    public string? DiskSlot = "hologram_disk_slot";

    /// <summary>
    /// Maximum number of holograms that can be projected simultaneously (portable only)
    /// </summary>
    [DataField("maxActiveHolograms")]
    public int MaxActiveHolograms = 2;

    /// <summary>
    /// Whether holograms are allowed to hold/carry this device (portable only)
    /// </summary>
    [DataField("allowHologramCarry")]
    public bool AllowHologramCarry = false;

    /// <summary>
    /// Power draw per active hologram in watts (portable only)
    /// </summary>
    [DataField("powerDrawPerHologram")]
    public float PowerDrawPerHologram = 50f;

    /// <summary>
    /// Dictionary mapping disk UIDs to their spawned hologram UIDs (portable mode)
    /// </summary>
    [DataField("activeHolograms")]
    public Dictionary<EntityUid, EntityUid> ActiveHolograms = new();

    /// <summary>
    /// EntityUid of the hologram that was called from disk in exclusive mode (portable only)
    /// </summary>
    [DataField("calledHologram")]
    public EntityUid? CalledHologram;
}

