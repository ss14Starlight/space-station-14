using Content.Shared.EntityTable.EntitySelectors;

namespace Content.Shared.GameTicking.Rules;

/// <summary>
/// Gamerule which creates starts a number of other gamerules at once based on a budget
/// </summary>
[RegisterComponent]
public sealed partial class SubRuleComponent : Component
{
    /// <summary>
    /// The total budget for antags.
    /// </summary>
    [DataField]
    public float Budget;

    /// <summary>
    /// The minimum or lower bound for budgets to start at.
    /// </summary>
    [DataField]
    public int BudgetMin = 200;

    /// <summary>
    /// The maximum or upper bound for budgets to start at.
    /// </summary>
    [DataField]
    public int BudgetMax = 350;

    /// <summary>
    /// A table of rules that are picked from.
    /// </summary>
    [DataField]
    public EntityTableSelector Table = new NoneSelector();

    /// <summary>
    /// The rules that have been spawned
    /// </summary>
    [DataField]
    public List<EntityUid> Rules = new();
}
