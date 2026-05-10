namespace Content.Server._Starlight.GameTicking.Rules.Components;

/// <summary>
/// This is used to FTL a map to station when RuleLoadedGridsEvent is triggered.
/// Works with <see cref="RuleGridsComponent"/>.
/// </summary>
[RegisterComponent, Access(typeof(FTLToStationRuleSystem))]
public sealed partial class FTLToStationRuleComponent : Component
{
    /// <summary>
    /// Dock PriorityTag
    /// </summary>
    [DataField]
    public string? PriorityTag;

    /// <summary>
    /// How long its stays in FTL.
    /// </summary>
    [DataField]
    public float? HyperspaceTime;
}
