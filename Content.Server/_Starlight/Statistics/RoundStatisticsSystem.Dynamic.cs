using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Rules;

namespace Content.Server._Starlight.Statistics;

public sealed partial class RoundStatisticsSystem
{
    [Dependency] private DynamicRuleSystem _dynamic = default!;

    private readonly Dictionary<DynamicRuleKey, DynamicRuleAccumulator> _dynamicRules = [];

    private float _dynamicBudgetLatest;
    private float _dynamicBudgetPeak;
    private bool _dynamicSeen;

    private void InitializeDynamicStatistics()
        => RegisterStatisticsDomain(EmitDynamicStatistics, ClearDynamicStatistics);

    /// <summary>
    /// Records a game rule that the dynamic scheduler paid for and started.
    /// </summary>
    public void RecordDynamicRule(string ruleId, float cost, bool priced, bool roundStart)
    {
        if (!EnsureRound())
            return;

        _dynamicSeen = true;

        var statistic = GetOrAddDynamicRule(new DynamicRuleKey(ruleId, roundStart, priced));
        statistic.Count++;
        statistic.CostTotal += cost;
    }

    /// <summary>
    /// Records the scheduler's remaining budget either side of an execution pass, so the value
    /// just before it spends counts towards the round's peak.
    /// </summary>
    public void RecordDynamicBudget(float budget)
    {
        if (!EnsureRound())
            return;

        _dynamicSeen = true;
        _dynamicBudgetLatest = budget;
        _dynamicBudgetPeak = Math.Max(_dynamicBudgetPeak, budget);
    }

    /// <summary>
    /// Budget accrues continuously but the scheduler only reports it when it executes, so the last
    /// reported value is stale by the end of the round. Read it from the rule itself instead.
    /// </summary>
    private void SampleDynamicBudget()
    {
        var budget = 0f;
        var sampled = false;

        var query = EntityQueryEnumerator<DynamicRuleComponent>();
        while (query.MoveNext(out var uid, out var rule))
        {
            if (_dynamic.GetRuleBudget((uid, rule)) is not { } ruleBudget)
                continue;

            budget += ruleBudget;
            sampled = true;
        }

        if (sampled)
            RecordDynamicBudget(budget);
    }

    private void EmitDynamicStatistics(RoundEndMessageEvent args)
    {
        if (!_dynamicSeen)
            return;

        SampleDynamicBudget();

        var rules = 0;
        var costTotal = 0f;

        foreach (var (key, statistic) in _dynamicRules)
        {
            rules += statistic.Count;
            costTotal += statistic.CostTotal;

            EmitRoundRecord(
                args.RoundId,
                "kind=dynamic_rule rule_id={RuleId} roundstart={RoundStart} priced={Priced} " +
                "count={Count} cost_total={CostTotal}",
                key.Rule,
                key.RoundStart,
                key.Priced,
                statistic.Count,
                Number(statistic.CostTotal));
        }

        EmitRoundRecord(
            args.RoundId,
            "kind=dynamic_summary rules_run={RulesRun} cost_total={CostTotal} " +
            "budget_remaining={BudgetRemaining} budget_peak={BudgetPeak}",
            rules,
            Number(costTotal),
            Number(_dynamicBudgetLatest),
            Number(_dynamicBudgetPeak));
    }

    private void ClearDynamicStatistics()
    {
        _dynamicRules.Clear();
        _dynamicBudgetLatest = 0f;
        _dynamicBudgetPeak = 0f;
        _dynamicSeen = false;
    }

    private DynamicRuleAccumulator GetOrAddDynamicRule(DynamicRuleKey key)
    {
        if (_dynamicRules.TryGetValue(key, out var statistic))
            return statistic;

        statistic = new DynamicRuleAccumulator();
        _dynamicRules.Add(key, statistic);
        return statistic;
    }

    private sealed class DynamicRuleAccumulator
    {
        public int Count;
        public float CostTotal;
    }

    private readonly record struct DynamicRuleKey(string Rule, bool RoundStart, bool Priced);
}
