namespace Content.Server._Starlight.Clothing.Components;

/// <summary>
/// Marker component for CE capacitor gloves.
/// </summary>
[RegisterComponent]
public sealed partial class CapacitorGlovesComponent : Component
{
    /// <summary>
    /// Whether the gloves are currently draining power or injecting it.
    /// Toggled by activating (E) the gloves in hand.
    /// </summary>
    [DataField]
    public Content.Shared._Starlight.Clothing.CapacitorGlovesMode Mode = Content.Shared._Starlight.Clothing.CapacitorGlovesMode.Drain;

    /// <summary>
    /// The entity to which we added a BatteryDrainerComponent on equip.
    /// Null when the wearer already had a drainer (IPC / ninja).
    /// </summary>
    [DataField]
    public EntityUid? DrainerTarget;

    /// <summary>
    /// The entity currently wearing these gloves. Set on equip, cleared on unequip.
    /// </summary>
    [DataField]
    public EntityUid? WearerUid;

    /// <summary>
    /// Fraction of joules drawn from the power device that end up in the cell.
    /// </summary>
    [DataField]
    public float DrainEfficiency = 0.3f;

    /// <summary>Duration in seconds of the drain do-after.</summary>
    [DataField]
    public float DrainTime = 1.0f;

    /// <summary>
    /// Hard cap (J) drawn from the source per drain tick. Equalises drain rate across
    /// all source types (APC, SMES, substation).
    /// APC natural cap = MaxSupply(10 kW) × DrainTime(1.5 s) = 15 000 J.
    /// </summary>
    [DataField]
    public float MaxDrainPerTick = 15000f;

    /// <summary>
    /// Fraction of joules taken from the slotted cell that end up in the target when injecting.
    /// </summary>
    [DataField]
    public float InjectionEfficiency = 0.9f;

    /// <summary>
    /// Hard cap (J) drawn from the cell per inject tick for non-bluespace cells.
    /// Small/medium cells (360–1 800 J) will satisfy this in one tick; larger cells take several.
    /// </summary>
    [DataField]
    public float InjectRateLimit = 5000f;

    /// <summary>
    /// Seconds the inject verb is locked out after an injection run completes.
    /// Prevents recharger-spam loops.
    /// </summary>
    [DataField]
    public float InjectCooldownTime = 10.0f;

    /// <summary>Runtime: earliest <see cref="IGameTiming.CurTime"/> at which the next inject can start.</summary>
    public TimeSpan InjectAvailableAt;

    /// <summary>The power device currently receiving automatic periodic injection. Null when idle.</summary>
    public EntityUid? AutoInjectTarget;
}

