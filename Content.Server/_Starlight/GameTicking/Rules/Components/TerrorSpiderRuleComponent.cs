namespace Content.Server._Starlight.GameTicking.Rules.Components;

/// <summary>
/// Component for the TerrorSpiderRuleSystem that stores info about winning/losing, player counts required for starting.
/// </summary>
[RegisterComponent, Access(typeof(TerrorSpiderRuleSystem))]
public sealed partial class TerrorSpiderRuleComponent : Component
{
    /// <summary>
    /// The amount of time between each check for players check.
    /// </summary>
    [DataField]
    public TimeSpan TimerWait = TimeSpan.FromSeconds(20);

    [DataField]
    public bool LoseProcessed = false;
}
