namespace Content.Server._Starlight.Holograms.Components;

/// <summary>
/// Console that allows hologram blade server management and projector selection.
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
    /// Maximum range to search for a server if not linked.
    /// TODO: Replace with grid connection.
    /// </summary>
    [DataField("searchRange")]
    public float SearchRange = 5f;

    /// <summary>
    /// Maximum range to search for hologram blade servers.
    /// Consoles read blade servers with both chips inserted to populate hologram list.
    /// </summary>
    [DataField("bladeServerScanRange")]
    public float BladeServerScanRange = 15f;

    /// <summary>
    /// Maximum number of holograms that can be projected simultaneously (Requires item component)
    /// </summary>
    [DataField("maxActiveHolograms")]
    public int MaxActiveHolograms = 2;

    /// <summary>
    /// Whether holograms are allowed to hold/carry this device (Requires item component)
    /// </summary>
    [DataField("allowHologramCarry")]
    public bool AllowHologramCarry = false;

    /// <summary>
    /// Whether to show the projector map and selection interface.
    /// False for portable projectors, true for stationary consoles.
    /// </summary>
    [DataField("showMap")]
    public bool ShowMap = true;

    /// <summary>
    /// Whether to show the project button.
    /// </summary>
    [DataField("showProjectButton")]
    public bool ShowProjectButton = true;

    /// <summary>
    /// Whether to show the recall button.
    /// </summary>
    [DataField("showRecallButton")]
    public bool ShowRecallButton = true;

    /// <summary>
    /// Whether to show the blade server panel sidebar.
    /// </summary>
    [DataField("showBladeServerPanel")]
    public bool ShowBladeServerPanel = true;

    /// <summary>
    /// Power draw per active hologram in watts (Requires cell)
    /// </summary>
    [DataField("powerDrawPerHologram")]
    public float PowerDrawPerHologram = 50f;

    /// <summary>
    /// Dictionary mapping blade server UIDs to their spawned hologram UIDs (portable mode only)
    /// </summary>
    [DataField("activeHolograms")]
    public Dictionary<EntityUid, EntityUid> ActiveHolograms = new();

    /// <summary>
    /// EntityUid of the hologram that was called from blade server in exclusive mode
    /// </summary>
    [DataField("calledHologram")]
    public EntityUid? CalledHologram;
}

