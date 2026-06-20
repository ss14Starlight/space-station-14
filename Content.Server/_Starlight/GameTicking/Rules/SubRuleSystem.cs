using Content.Server.Administration.Logs;
using Content.Server.GameTicking.Rules;
using Content.Shared.Database;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.Conditions;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking.Rules;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared._Starlight.GameTicking.Rules;

namespace Content.Server._Starlight.GameTicking.Rules;

/// <summary>
/// A system to handle one-shot dynamic rules, with slightly different add/start semantics.
/// </summary>
public sealed partial class SubRuleSystem : GameRuleSystem<SubRuleComponent>
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private EntityTableSystem _entityTable = default!;
    [Dependency] private IRobustRandom _random = default!;

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
            // If our use of subrule was to add a delayed rule, we need to avoid double-triggering it, as that'd cause it to immediately fire.
            if (HasComp<DelayedStartRuleComponent>(ruleUid))
                continue;

            GameTicker.StartGameRule(ruleUid);
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
