namespace Content.Shared.GameTicking.Rules;

/// <summary>
/// Component that tracks how much a rule "costs" for Dynamic
/// </summary>
[RegisterComponent]
public sealed partial class DynamicRuleCostComponent : Component
{
    /// <summary>
    /// The amount of budget a rule takes up
    /// </summary>
    [DataField(required: true)]
    public int Cost;

    #region Starlight
    /// <summary>
    /// The number of subsequent Dynamic rounds this rule is ineligible for after being selected.
    /// Only decrements on Dynamic rounds, not on other rounds.
    /// </summary>
    [DataField]
    public int Cooldown = 0;
    #endregion
}
