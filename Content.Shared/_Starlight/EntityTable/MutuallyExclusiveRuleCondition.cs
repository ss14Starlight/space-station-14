using Content.Shared.EntityTable;
using Content.Shared.EntityTable.Conditions;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.EntityTable;

/// <summary>
/// Condition that succeeds only when none of the specified gamerules have already run.
/// </summary>
/// <remarks>
/// This is meant for mutually exclusive gamerules. For example, Nukeops and NukeopsLate
/// should not both happen in the same round.
/// </remarks>
public sealed partial class MutuallyExclusiveRuleCondition : EntityTableCondition
{
    /// <summary>
    /// Any previously-run rule in this list will cause this condition to fail.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> Rules = new();

    protected override bool EvaluateImplementation(EntityTableSelector root,
        IEntityManager entMan,
        IPrototypeManager proto,
        EntityTableContext ctx)
    {
        if (Rules.Count == 0)
            return false;

        var gameTicker = entMan.System<SharedGameTicker>();

        foreach (var previousRule in gameTicker.AllPreviousGameRules)
        {
            foreach (var ruleId in Rules)
            {
                string rule = ruleId;

                if (previousRule.Item2 == rule)
                    return false;
            }
        }

        return true;
    }
}
