using System.Diagnostics;
using Content.Server.Administration.Logs;
using Content.Server.RoundEnd;
using Content.Shared._Starlight.EntityTable;
using Content.Shared.Database;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.Conditions;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking.Rules;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;
using Content.Server.Chat.Managers;

namespace Content.Server.GameTicking.Rules;

public sealed partial class DynamicRuleSystem : GameRuleSystem<DynamicRuleComponent>
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private EntityTableSystem _entityTable = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!; // Starlight
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private IRobustRandom _random = default!;
    #region Starlight
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    private readonly Dictionary<EntProtoId, int> _ruleCooldowns = new();
    private readonly HashSet<EntProtoId> _roundCooldowns = new();
    private bool _roundCooldownsInitialized;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("dynamic");
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }
    #endregion

    protected override void Added(EntityUid uid, DynamicRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        component.Budget = _random.Next(component.StartingBudgetMin, component.StartingBudgetMax);
        component.NextRuleTime = Timing.CurTime + _random.Next(component.MinRuleInterval, component.MaxRuleInterval);
        // Starlight begin - If in lobby, add them now so we can metagame!
        if (_ticker.RunLevel != GameRunLevel.PreRoundLobby) return;
        component.LastBudgetUpdate = Timing.CurTime;
        Execute((uid, component));
        // Starlight end
    }

    protected override void Started(EntityUid uid, DynamicRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // Since we don't know how long until this rule is activated, we need to
        // set the last budget update to now so it doesn't immediately give the component a bunch of points.
        if (_ticker.RunLevel == GameRunLevel.PreRoundLobby) return; // Starlight - Don't add them twice!
        component.LastBudgetUpdate = Timing.CurTime;
        Execute((uid, component));
    }

    protected override void Ended(EntityUid uid, DynamicRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        foreach (var rule in component.Rules)
        {
            GameTicker.EndGameRule(rule);
        }
    }

    protected override void ActiveTick(EntityUid uid, DynamicRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (Timing.CurTime < component.NextRuleTime)
            return;

        // don't spawn antags during evac
        if (_roundEnd.IsRoundEndRequested())
            return;

        Execute((uid, component));
    }

    /// <summary>
    /// Generates and returns a list of randomly selected,
    /// valid rules to spawn based on <see cref="DynamicRuleComponent.Table"/>.
    /// </summary>
    private List<EntProtoId> GetRuleSpawns(Entity<DynamicRuleComponent> entity) // Starlight
    {
        #region Starlight
        // Modified heavily to support the new GameRuleTableContext, which allows us to check for cooldowns and previous rules.
        InitializeRoundCooldowns();
        UpdateBudget((entity.Owner, entity.Comp));
        var budget = entity.Comp.Budget;
        var previousRules = new List<EntProtoId>();
        var previousRuleEntities = new HashSet<EntityUid>();

        foreach (var previousRule in _ticker.GetAddedGameRules().Concat(entity.Comp.Rules))
        {
            if (!previousRuleEntities.Add(previousRule) ||
                Deleted(previousRule) ||
                MetaData(previousRule).EntityPrototype?.ID is not { } prototype)
            {
                continue;
            }

            previousRules.Add(prototype);
        }

        var gameRuleContext = new GameRuleTableContext(previousRules, _roundCooldowns);
        var ctx = new EntityTableContext(new Dictionary<string, object>
        {
            { HasBudgetCondition.BudgetContextKey, budget },
        });
        ctx.SetData(gameRuleContext);

        foreach (var rule in _entityTable.GetSpawns(entity.Comp.Table, ctx: ctx))
        {
            _prototypeManager.Index(rule)
                .TryGetComponent(out DynamicRuleCostComponent? cost, EntityManager.ComponentFactory);

            if (_roundCooldowns.Contains(rule))
                continue;

            // HasBudgetCondition should normally reject this rule, but we check here just in case.
            // We want to avoid negative budgets.
            if (cost != null && cost.Cost > budget)
                continue;

            gameRuleContext.SelectedRules.Add(rule);

            if (cost == null)
                continue;

            budget -= cost.Cost;
            ctx.SetData(HasBudgetCondition.BudgetContextKey, budget);
        }

        return gameRuleContext.SelectedRules;
        #endregion
    }

    // Starlight, added variant budget
    /// <summary>
    /// Updates the budget of the provided dynamic rule component based on the amount of time since the last update
    /// multiplied by the <see cref="DynamicRuleComponent.BudgetPerSecond"/> value.
    /// After the budget has reached <see cref="DynamicRuleComponent.VariantBudgetThreshold"/> value,
    /// the budget will increase at the rate specified by <see cref="DynamicRuleComponent.VariantBudgetPerSecond"/> instead.
    /// </summary>
    private void UpdateBudget(Entity<DynamicRuleComponent> entity)
    {
        var duration = (float)(Timing.CurTime - entity.Comp.LastBudgetUpdate).TotalSeconds;

        #region Starlight
        // If the budget has reached or exceeded the variant threshold, we use the variant budget per second, otherwise we use the normal budget per second.
        if (entity.Comp.Budget >= entity.Comp.VariantBudgetThreshold)
            entity.Comp.Budget += duration * entity.Comp.VariantBudgetPerSecond;
        else
            entity.Comp.Budget += duration * entity.Comp.BudgetPerSecond;
        #endregion
        entity.Comp.LastBudgetUpdate = Timing.CurTime;
    }

    /// <summary>
    /// Executes this rule, generating new dynamic rules and starting them.
    /// </summary>
    /// <returns>
    /// Returns a list of the rules that were executed.
    /// </returns>
    private List<EntityUid> Execute(Entity<DynamicRuleComponent> entity)
    {
        entity.Comp.NextRuleTime =
            Timing.CurTime + _random.Next(entity.Comp.MinRuleInterval, entity.Comp.MaxRuleInterval);

        var executedRules = new List<EntityUid>();

        foreach (var rule in GetRuleSpawns(entity))
        {
            // Starlight start
            // We add the rule passing along the list of child rules, so that
            // if the rule is added, it's added to our child list before the
            // events calling its Added method are fired. This makes the rule
            // hierarchy available for the Added method.
            var ruleUid = GameTicker.AddGameRule(rule, entity.Comp.Rules);
            var res = GameTicker.StartGameRule(ruleUid);
            // Starlight end
            // var res = GameTicker.StartGameRule(rule, out var ruleUid); Starlight - commented out in favor of the above
            Debug.Assert(res);

            executedRules.Add(ruleUid);

            if (TryComp<DynamicRuleCostComponent>(ruleUid, out var cost))
            {
                entity.Comp.Budget -= cost.Cost;

                #region Starlight
                if (cost.Cooldown > 0)
                {
                    _ruleCooldowns[rule] = cost.Cooldown;
                    _sawmill.Info($"Rule {rule} added to the Dynamic cooldown for {cost.Cooldown} rounds.");
                }
                #endregion

                _adminLog.Add(LogType.EventRan, LogImpact.High, $"{ToPrettyString(entity)} ran rule {ToPrettyString(ruleUid)} with cost {cost.Cost} on budget {entity.Comp.Budget}.");
            }
            else
            {
                _adminLog.Add(LogType.EventRan, LogImpact.High, $"{ToPrettyString(entity)} ran rule {ToPrettyString(ruleUid)} which had no cost.");
            }
        }

        //entity.Comp.Rules.AddRange(executedRules); // Starlight - comment

        // Starlight begin
        if (_ticker.RunLevel != GameRunLevel.PreRoundLobby) return executedRules;

        List<string> ruleIdList = [];
        foreach (var meta in executedRules.Select(MetaData))
            if(meta.EntityPrototype is not null) ruleIdList.Add(meta.EntityPrototype.ID);

        _chat.SendAdminAnnouncement($"Dynamic roundstart rules: {string.Join(", ", ruleIdList)}.");
        // Starlight end

        return executedRules;
    }

    #region Starlight
    /// <summary>
    /// Builds the cooldown snapshot used by all Dynamic rolls in the current round.
    /// This runs once when Dynamic first selects rules, advancing persistent cooldowns
    /// while keeping those rules unavailable for the entire current Dynamic round.
    /// </summary>
    private void InitializeRoundCooldowns()
    {
        if (_roundCooldownsInitialized)
            return;

        _roundCooldownsInitialized = true;
        _roundCooldowns.Clear();

        foreach (var (rule, remaining) in _ruleCooldowns.ToArray())
        {
            if (remaining <= 0)
            {
                _ruleCooldowns.Remove(rule);
                continue;
            }

            _roundCooldowns.Add(rule);

            if (remaining <= 1)
                _ruleCooldowns.Remove(rule);
            else
                _ruleCooldowns[rule] = remaining - 1;
        }
    }

    /// <summary>
    /// Clears the cooldown snapshot for the round that just ended and marks it for rebuilding.
    /// Persistent cooldowns are preserved and applied when the next Dynamic round begins.
    /// </summary>
    private void OnRoundRestartCleanup(RoundRestartCleanupEvent _)
    {
        _roundCooldownsInitialized = false;
        _roundCooldowns.Clear();
    }
    #endregion

    #region Command Methods

    public List<EntityUid> GetDynamicRules()
    {
        var rules = new List<EntityUid>();
        var query = EntityQueryEnumerator<DynamicRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out _, out var comp))
        {
            if (!GameTicker.IsGameRuleActive(uid, comp))
                continue;
            rules.Add(uid);
        }

        return rules;
    }

    public float? GetRuleBudget(Entity<DynamicRuleComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return null;

        UpdateBudget((entity.Owner, entity.Comp));
        return entity.Comp.Budget;
    }

    public float? AdjustBudget(Entity<DynamicRuleComponent?> entity, float amount)
    {
        if (!Resolve(entity, ref entity.Comp))
            return null;

        UpdateBudget((entity.Owner, entity.Comp));
        entity.Comp.Budget += amount;
        return entity.Comp.Budget;
    }

    public float? SetBudget(Entity<DynamicRuleComponent?> entity, float amount)
    {
        if (!Resolve(entity, ref entity.Comp))
            return null;

        entity.Comp.LastBudgetUpdate = Timing.CurTime;
        entity.Comp.Budget = amount;
        return entity.Comp.Budget;
    }

    public IEnumerable<EntProtoId> DryRun(Entity<DynamicRuleComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return new List<EntProtoId>();

        return GetRuleSpawns((entity.Owner, entity.Comp));
    }

    public IEnumerable<EntityUid> ExecuteNow(Entity<DynamicRuleComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return new List<EntityUid>();

        return Execute((entity.Owner, entity.Comp));
    }

    public IEnumerable<EntityUid> Rules(Entity<DynamicRuleComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return new List<EntityUid>();

        return entity.Comp.Rules;
    }

    #endregion
}
