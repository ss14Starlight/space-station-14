namespace Content.Server._Starlight.Clothing.Components;

/// <summary>
/// Marks a power cell as a bluespace cell.
/// When slotted into capacitor gloves, limits the per-press transfer rate and enforces a
/// cooldown between inject presses so filling an APC takes ~45–60 seconds.
/// When inserted into ANY other item (not gloves, not a charger), triggers an overcharge
/// that drains all power from the host, fires lightning arcs, and electrocutes nearby mobs.
/// </summary>
[RegisterComponent]
public sealed partial class BluespaceCapacitorBatteryComponent : Component
{
    /// <summary>
    /// Maximum joules drawn from this cell per single inject verb press.
    /// At 90 % efficiency → 1 250 J into target per press.
    /// 40 presses × 1.0 s cooldown = 40 s to fill a basic APC (50 000 J).
    /// </summary>
    [DataField]
    public float TransferRateLimit = 1389f;

    /// <summary>Minimum seconds between consecutive inject ticks when using this cell.</summary>
    [DataField]
    public float TransferCooldown = 1.0f;
}
