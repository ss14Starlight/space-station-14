using System.Diagnostics;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.Conditions;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking.Rules;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.GameTicking.Rules;

/// <summary>
/// A system to handle one-shot dynamic rules, with slightly different add/start semantics.
/// </summary>
public sealed class SubRuleSystem : GameRuleSystem<SubRuleComponent>
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override void Added(EntityUid uid, SubRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        component.Budget = _random.Next(component.BudgetMin, component.BudgetMax);

        foreach (var rule in GetRuleSpawns((uid, component)))
        {
            var ruleUid = GameTicker.AddGameRule(rule, component.Rules);

            if (TryComp<DynamicRuleCostComponent>(ruleUid, out var cost))
            {
                component.Budget -= cost.Cost;
                _adminLog.Add(LogType.EventRan, LogImpact.High, $"{ToPrettyString(uid)} ran rule {ToPrettyString(ruleUid)} with cost {cost.Cost} on budget {component.Budget}.");
            }
            else
            {
                _adminLog.Add(LogType.EventRan, LogImpact.High, $"{ToPrettyString(uid)} ran rule {ToPrettyString(ruleUid)} which had no cost.");
            }
        }
    }

    protected override void Started(EntityUid uid, SubRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        foreach (var ruleUid in component.Rules)
        {
            var res = GameTicker.StartGameRule(ruleUid);
            Debug.Assert(res);
        }
    }

    protected override void Ended(EntityUid uid, SubRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        foreach (var rule in component.Rules)
        {
            GameTicker.EndGameRule(rule);
        }
    }

    /// <summary>
    /// Generates and returns a list of randomly selected,
    /// valid rules to spawn based on <see cref="SubRuleComponent.Table"/>.
    /// </summary>
    private IEnumerable<EntProtoId> GetRuleSpawns(Entity<SubRuleComponent> entity)
    {
        var ctx = new EntityTableContext(new Dictionary<string, object>
        {
            { HasBudgetCondition.BudgetContextKey, entity.Comp.Budget },
        });

        return _entityTable.GetSpawns(entity.Comp.Table, ctx: ctx);
    }
}
