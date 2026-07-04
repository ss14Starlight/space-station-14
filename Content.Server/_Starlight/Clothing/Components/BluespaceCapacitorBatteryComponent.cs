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

    /// <summary>EMP pulse radius on overcharge (metres).</summary>
    [DataField]
    public float OverchargeEmpRange = 3f;

    /// <summary>Energy consumed by the EMP pulse on overcharge (J).</summary>
    [DataField]
    public float OverchargeEmpEnergy = 20000f;

    /// <summary>Duration of the EMP effect on overcharge.</summary>
    [DataField]
    public TimeSpan OverchargeEmpDuration = TimeSpan.FromSeconds(5);

    /// <summary>Radius of the lightning arcs on overcharge (metres).</summary>
    [DataField]
    public float OverchargeLightningRange = 4f;

    /// <summary>Number of lightning bolts fired on overcharge.</summary>
    [DataField]
    public int OverchargeLightningCount = 5;

    /// <summary>Radius in which mobs are electrocuted on overcharge (metres).</summary>
    [DataField]
    public float OverchargeElectrocuteRange = 2f;

    /// <summary>Shock damage dealt per mob on overcharge.</summary>
    [DataField]
    public int OverchargeElectrocuteDamage = 20;

    /// <summary>Duration of electrocution stun on overcharge.</summary>
    [DataField]
    public TimeSpan OverchargeElectrocuteDuration = TimeSpan.FromSeconds(5);
}
