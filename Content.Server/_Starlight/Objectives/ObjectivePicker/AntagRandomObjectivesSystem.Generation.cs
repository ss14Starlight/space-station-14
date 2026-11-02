using System.Linq;
using Content.Shared._Starlight.Traits.Antags;
using Content.Server.Antag;
using Content.Server.Mind.Filters;
using Content.Server.Objectives.Systems;
using Content.Server.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives;
using Content.Shared.Random.Helpers;
using Content.Shared.Random;
using Content.Shared.Whitelist;
using Content.Shared._Starlight.Objectives.ObjectivePicker;
using Content.Shared.Objectives.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Objectives.ObjectivePicker;

public sealed partial class AntagRandomObjectivesSystem
{
    [Dependency] private EntityWhitelistSystem _slWhitelist = default!;
    [Dependency] private IPrototypeManager _slPrototypes = default!;
    [Dependency] private TargetObjectiveSystem _slTarget = default!;
    [Dependency] private TargetSystem _target = default!;

    private static readonly EntProtoId _sLDieObjective = "DieObjective";

    private void CreatePicker(Entity<AntagRandomObjectivesComponent> rule, ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Session == null || !_mind.TryGetMind(args.Session, out var mindId, out var mind))
            return;

        // Repeated selection events must not grant another forced objective or mulligan.
        if (HasComp<ObjectivePickerConfigurationComponent>(mindId))
            return;

        if (!float.IsFinite(rule.Comp.MaxDifficulty) || rule.Comp.MaxDifficulty < 0)
        {
            Log.Error($"Objective picker on {ToPrettyString(rule)} requires a finite, nonnegative maxDifficulty.");
            return;
        }

        var config = EnsureComp<ObjectivePickerConfigurationComponent>(mindId);
        config.Rule = rule.Owner;
        config.PreferredOptions = Math.Max(1, rule.Comp.MaxOptions);
        config.MinimumDifficulty = rule.Comp.MaxDifficulty;
        config.SelectionDelay = rule.Comp.SelectionDelay;
        EnsureComp<ObjectivePickerProgressComponent>(mindId);
        foreach (var set in rule.Comp.Sets)
        {
            if (_random.Prob(set.Prob))
                FlattenWeights(set.Groups, config.Weights);
        }

        var counts = CountAssignedObjectives(mindId);
        EntityUid? forcedTraitor = null;
        var traitorForcedWeights = new Dictionary<string, float>();
        if (rule.Comp.TraitorForcedObjectiveGroup is { } traitorGroup)
        {
            FlattenWeights(traitorGroup, traitorForcedWeights);
            if (config.Weights.GetValueOrDefault(_sLDieObjective) <= 0 ||
                (mind.OwnedEntity is { } body && HasComp<UngloriousComponent>(body)))
                traitorForcedWeights.Remove(_sLDieObjective);
            forcedTraitor = ForceObjective(mindId, mind, traitorForcedWeights, counts);
            foreach (var prototype in traitorForcedWeights.Keys)
                config.Weights.Remove(prototype);
        }

        if (rule.Comp.ForcedObjectiveGroup is { } forcedGroup)
        {
            var forced = new Dictionary<string, float>();
            FlattenWeights(forcedGroup, forced);
            ForceObjective(mindId, mind, forced, counts);
            foreach (var prototype in forced.Keys)
                config.Weights.Remove(prototype);
        }

        if (rule.Comp.StoryObjectiveGroup is { } storyGroup)
        {
            FlattenWeights(storyGroup, config.StoryWeights);
            foreach (var prototype in config.StoryWeights.Keys)
                config.Weights.Remove(prototype);
        }

        foreach (var prototype in rule.Comp.ExcludedObjectives)
            config.Weights.Remove(prototype);

        var offers = EnsureComp<PotentialObjectivesComponent>(mindId);
        config.MinimumDifficulty = GetPickerDifficulty(rule.Comp.MaxDifficulty, mind);
        offers.MinimumDifficulty = config.MinimumDifficulty;
        offers.AutoSelectionDelay = rule.Comp.SelectionDelay;
        offers.AutoSelectionTime = _timing.CurTime + rule.Comp.SelectionDelay;
        var viable = FillOffers(mindId, mind, offers, config, counts);
        if (!viable && forcedTraitor is { } dagd && Prototype(dagd)?.ID == _sLDieObjective.Id)
        {
            // DAGD is ineligible when the compatible combat pool cannot supply the required budget.
            // Preserve its configured rarity whenever eligible; otherwise choose a viable forced goal.
            mind.Objectives.Remove(dagd);
            Del(dagd);
            foreach (var id in offers.ObjectiveOptions.Keys)
                Del(GetEntity(id));
            offers.ObjectiveOptions.Clear();
            offers.Difficulties.Clear();
            config.DeferredTargets.Clear();
            traitorForcedWeights.Remove(_sLDieObjective);
            ForceObjective(mindId, mind, traitorForcedWeights, counts);
            config.MinimumDifficulty = GetPickerDifficulty(rule.Comp.MaxDifficulty, mind);
            offers.MinimumDifficulty = config.MinimumDifficulty;
            viable = FillOffers(mindId, mind, offers, config, counts);
        }
        if (!viable)
            Log.Error($"No viable objective pool for {ToPrettyString(mindId)} at difficulty {offers.MinimumDifficulty}; awaiting eligible offers.");
        Dirty(mindId, offers);
        Dirty(mindId, mind);
    }

    /// <summary>
    /// Preserve each nested category's probability rather than giving large categories extra weight.
    /// </summary>
    private void FlattenWeights(string groupId, Dictionary<string, float> result,
        float weight = 1, HashSet<string>? path = null)
    {
        path ??= new HashSet<string>();
        if (!path.Add(groupId))
        {
            Log.Error($"Cyclic objective group: {groupId}");
            return;
        }

        if (_slPrototypes.TryIndex<WeightedRandomPrototype>(groupId, out var group))
        {
            var total = group.Weights.Values.Where(value => value > 0).Sum();
            if (total > 0)
            {
                foreach (var (id, value) in group.Weights)
                {
                    if (value > 0)
                        FlattenWeights(id, result, weight * value / total, path);
                }
            }
        }
        else if (_slPrototypes.HasIndex<EntityPrototype>(groupId))
        {
            result[groupId] = result.GetValueOrDefault(groupId) + weight;
        }

        path.Remove(groupId);
    }

    private EntityUid? ForceObjective(EntityUid mindId, MindComponent mind, Dictionary<string, float> weights,
        Dictionary<string, int> counts)
    {
        var candidates = new Dictionary<string, float>(weights);
        while (_random.TryPickAndTake(candidates, out var prototype))
        {
            if (!CreateOffer(mindId, mind, prototype, counts, out var objective, out var deferred))
                continue;

            if (deferred)
            {
                var assigned = new ObjectiveAssignedEvent(mindId, mind);
                RaiseLocalEvent(objective, ref assigned);
                if (assigned.Cancelled)
                {
                    Del(objective);
                    continue;
                }
                var after = new ObjectiveAfterAssignEvent(mindId, mind, Comp<ObjectiveComponent>(objective), MetaData(objective));
                RaiseLocalEvent(objective, ref after);
            }

            _mind.AddObjective(mindId, mind, objective);
            return objective;
        }
        Log.Error($"No eligible forced objective for {ToPrettyString(mindId)}.");
        return null;
    }

    /// <summary>
    /// Preferred option count is a starting point. Grow the pool until it has twice the budget and a
    /// compatible selection meeting the budget. Story goals are added separately and cost zero.
    /// </summary>
    private bool FillOffers(EntityUid mindId, MindComponent mind, PotentialObjectivesComponent offers,
        ObjectivePickerConfigurationComponent config, Dictionary<string, int> counts, NetEntity? retained = null,
        int attemptBudget = int.MaxValue)
    {
        if (config.Refill == null)
        {
            var candidates = new Dictionary<string, float>(config.Weights);
            var stories = new Dictionary<string, float>(config.StoryWeights);
            foreach (var key in offers.ObjectiveOptions.Keys)
            {
                if (Prototype(GetEntity(key)) is not { } proto)
                    continue;
                stories.Remove(proto.ID);
                if (TryComp<ObjectiveComponent>(GetEntity(key), out var objective) && objective.Unique)
                    candidates.Remove(proto.ID);
            }

            // Finite attempts also cover small DAGD pools with non-unique kill/lesson goals.
            var smallest = config.Weights.Keys.Select(id => _slPrototypes.Index<EntityPrototype>(id))
                .Select(proto => proto.TryComp<ObjectiveComponent>(out var objective, EntityManager.ComponentFactory)
                    ? objective.Difficulty : 0).Where(difficulty => difficulty > 0).DefaultIfEmpty(1).Min();
            config.Refill = new ObjectivePickerRefillState
            {
                Candidates = candidates,
                Stories = stories,
                Attempts = Math.Max(config.Weights.Count * 4,
                    (int) Math.Ceiling(2 * offers.MinimumDifficulty / smallest) + (config.PreferredOptions * 8)),
            };
            config.LastRefillRevision = _eligibilityRevision;
            config.RefillCounts = counts;
        }

        var refill = config.Refill;
        UpdateOfferState(mindId, mind, offers, counts, config);
        while (!refill.CombatFinished)
        {
            if (OffersViable(offers, retained) &&
                offers.Difficulties.Count(pair => pair.Value > 0 && ObjectivePickerSelection.Available(offers, pair.Key))
                    >= config.PreferredOptions)
            {
                refill.CombatFinished = true;
                break;
            }
            if (refill.Attempts <= 0)
            {
                refill.CombatFinished = true;
                break;
            }
            if (attemptBudget <= 0)
                return false;
            attemptBudget--;
            refill.Attempts--;

            if (refill.Candidates.Count == 0)
                refill.Candidates = new Dictionary<string, float>(refill.Repeatable);
            if (!_random.TryPickAndTake(refill.Candidates, out var prototype))
            {
                refill.CombatFinished = true;
                break;
            }
            if (!CreateOffer(mindId, mind, prototype, counts, out var uid, out var deferred))
            {
                refill.Repeatable.Remove(prototype);
                continue;
            }

            if (!Comp<ObjectiveComponent>(uid).Unique)
            {
                // Different offers must have enough distinct eligible targets, even before any names are assigned.
                if (deferred && TryComp<PickRandomPersonComponent>(uid, out var pick))
                {
                    var targets = new HashSet<Entity<MindComponent>>();
                    var pool = pick.Pool;
                    pool.FindMinds(targets, mindId, EntityManager, _target);
                    _mind.FilterMinds(targets, pick.Filters, mindId);
                    if (offers.ObjectiveOptions.Keys.Count(id => Prototype(GetEntity(id))?.ID == prototype) >= targets.Count)
                    {
                        Del(uid);
                        refill.Repeatable.Remove(prototype);
                        continue;
                    }
                }
                refill.Repeatable[prototype] = config.Weights[prototype];
            }

            AddOffer(mindId, mind, offers, config, uid, deferred);
            UpdateOfferState(mindId, mind, offers, counts, config);
        }

        var storyCount = offers.ObjectiveOptions.Keys.Count(key =>
            Prototype(GetEntity(key)) is { } proto && config.StoryWeights.ContainsKey(proto.ID));
        while (storyCount < 2 && refill.Stories.Count > 0)
        {
            if (attemptBudget <= 0)
                return false;
            attemptBudget--;
            if (!_random.TryPickAndTake(refill.Stories, out var prototype))
                break;
            if (!CreateOffer(mindId, mind, prototype, counts, out var uid, out var deferred))
                continue;
            AddOffer(mindId, mind, offers, config, uid, deferred);
            storyCount++;
        }

        config.Refill = null;
        UpdateOfferState(mindId, mind, offers, counts, config);
        return OffersViable(offers, retained);
    }

    private static bool OffersViable(PotentialObjectivesComponent offers, NetEntity? retained = null)
    {
        var available = offers.ObjectiveOptions.Keys.Where(id => ObjectivePickerSelection.Available(offers, id)).ToArray();
        return ObjectivePickerSelection.Difficulty(offers, available) + ObjectivePickerSelection.Tolerance
                >= 2 * offers.MinimumDifficulty && ObjectivePickerSelection.TryComplete(offers, available,
                    retained is { } id ? new[] { id } : Array.Empty<NetEntity>(), out _);
    }

    private bool CreateOffer(EntityUid mindId, MindComponent mind, string prototype,
        Dictionary<string, int> counts, out EntityUid uid, out bool deferred)
    {
        uid = default;
        deferred = false;
        if (_objectives.TryCreateObjective(mindId, mind, prototype, assign: false) is not { } created)
            return false;
        uid = created;

        if (!_objectives.CanBeAssigned(uid, mindId, mind) ||
            !WithinObjectiveLimit(uid, counts) || mind.Objectives.Any(other => !AreObjectivesCompatible(created, other)))
        {
            Del(uid);
            return false;
        }

        var objective = Comp<ObjectiveComponent>(uid);
        if (objective.Hidden && objective.HiddenTitle == null)
        {
            Log.Error($"Hidden objective picker preview on {ToPrettyString(uid)} requires hiddenTitle.");
            Del(uid);
            return false;
        }

        deferred = objective.Hidden && HasComp<TargetObjectiveComponent>(uid) && HasComp<PickRandomPersonComponent>(uid);
        if (deferred)
        {
            // Check eligibility without assigning a target or revealing their identity.
            if (!TryComp<PickRandomPersonComponent>(uid, out var pick) ||
                _target.PickFromPool(pick.Pool, pick.Filters, mindId) == null)
            {
                Del(uid);
                return false;
            }
            return true;
        }

        var assigned = new ObjectiveAssignedEvent(mindId, mind);
        RaiseLocalEvent(uid, ref assigned);
        if (assigned.Cancelled)
        {
            Del(uid);
            return false;
        }
        var after = new ObjectiveAfterAssignEvent(mindId, mind, Comp<ObjectiveComponent>(uid), MetaData(uid));
        RaiseLocalEvent(uid, ref after);
        return true;
    }

    private void AddOffer(EntityUid mindId, MindComponent mind, PotentialObjectivesComponent offers,
        ObjectivePickerConfigurationComponent config, EntityUid uid, bool deferred)
    {
        var objective = Comp<ObjectiveComponent>(uid);
        var netId = GetNetEntity(uid);
        ObjectiveInfo? info;
        if (objective.Hidden && objective.HiddenTitle is { } title && objective.Icon is { } icon)
        {
            info = new ObjectiveInfo(Loc.GetString(title), Loc.GetString(objective.HiddenDescription), icon, 0);
        }
        else
        {
            info = _objectives.GetInfo(uid, mindId, mind);
        }

        if (info is not { } value)
        {
            Del(uid);
            return;
        }
        offers.ObjectiveOptions[netId] = value;
        offers.Difficulties[netId] = objective.Difficulty;
        if (deferred)
            config.DeferredTargets.Add(netId);
    }

    private bool AreObjectivesCompatible(EntityUid first, EntityUid second)
    {
        if ((TryComp<ObjectiveBlacklistRequirementComponent>(first, out var a) &&
            _slWhitelist.IsWhitelistPass(a.Blacklist, second)) ||
            (TryComp<ObjectiveBlacklistRequirementComponent>(second, out var b) &&
            _slWhitelist.IsWhitelistPass(b.Blacklist, first)))
            return false;

        return Prototype(first)?.ID != Prototype(second)?.ID ||
                (!Comp<ObjectiveComponent>(first).Unique && !Comp<ObjectiveComponent>(second).Unique);
    }

    private sealed class AssignedObjectiveCounts
    {
        public readonly Dictionary<string, int> Total = new();
        public readonly Dictionary<EntityUid, HashSet<string>> ByMind = new();

        public Dictionary<string, int> Excluding(EntityUid mind)
        {
            var counts = new Dictionary<string, int>(Total);
            if (!ByMind.TryGetValue(mind, out var own))
                return counts;
            foreach (var prototype in own)
            {
                if (--counts[prototype] == 0)
                    counts.Remove(prototype);
            }
            return counts;
        }
    }

    private Dictionary<string, int> CountAssignedObjectives(EntityUid exceptMind) =>
        CountAssignedObjectives().Excluding(exceptMind);

    private AssignedObjectiveCounts CountAssignedObjectives()
    {
        var counts = new AssignedObjectiveCounts();
        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var uid, out var mind))
        {
            var prototypes = mind.Objectives.Where(Exists).Select(objective => Prototype(objective)?.ID)
                .OfType<string>().ToHashSet();
            counts.ByMind[uid] = prototypes;
            foreach (var prototype in prototypes)
                counts.Total[prototype] = counts.Total.GetValueOrDefault(prototype) + 1;
        }
        return counts;
    }

    private bool WithinObjectiveLimit(EntityUid uid, Dictionary<string, int> counts) => !TryComp<ObjectiveLimitComponent>(uid, out var limit) ||
                    (Prototype(uid) is { } prototype && counts.GetValueOrDefault(prototype.ID) < limit.Limit);

    private void UpdateOfferState(EntityUid mindId, MindComponent mind, PotentialObjectivesComponent offers,
        Dictionary<string, int> counts, ObjectivePickerConfigurationComponent? config = null)
    {
        var unavailable = new HashSet<NetEntity>();
        var keys = offers.ObjectiveOptions.Keys.ToArray();
        var targetPools = new Dictionary<NetEntity, HashSet<int>>();
        if (config == null)
            TryComp<ObjectivePickerConfigurationComponent>(mindId, out config);
        CacheOfferCompatibility(offers, config);
        // Keep tokens stable across refreshes so unchanged pools don't make unnecessary updates.
        var tokens = config?.TargetTokens.ToDictionary(pair => pair.Value, pair => pair.Key)
            ?? new Dictionary<EntityUid, int>();
        for (var i = 0; i < keys.Length; i++)
        {
            var uid = GetEntity(keys[i]);
            if (!Exists(uid) || !WithinObjectiveLimit(uid, counts) ||
                !_objectives.CanBeAssigned(uid, mindId, mind) || mind.Objectives.Any(other => !AreObjectivesCompatible(uid, other)))
                unavailable.Add(keys[i]);

            if (config?.DeferredTargets.Contains(keys[i]) == true &&
                TryComp<PickRandomPersonComponent>(uid, out var pick))
            {
                var candidates = new HashSet<Entity<MindComponent>>();
                var poolSource = pick.Pool;
                poolSource.FindMinds(candidates, mindId, EntityManager, _target);
                _mind.FilterMinds(candidates, pick.Filters, mindId);
                var pool = new HashSet<int>();
                foreach (var candidate in candidates)
                {
                    if (!tokens.TryGetValue(candidate.Owner, out var token))
                    {
                        token = tokens.Count;
                        tokens[candidate.Owner] = token;
                        config.TargetTokens[token] = candidate.Owner;
                    }
                    pool.Add(token);
                }
                targetPools[keys[i]] = pool;
                if (pool.Count == 0)
                    unavailable.Add(keys[i]);
            }
        }
        offers.UnavailableObjectives = unavailable;
        offers.TargetPools = targetPools;
    }

    private void CacheOfferCompatibility(PotentialObjectivesComponent offers, ObjectivePickerConfigurationComponent? config)
    {
        if (config != null && config.CachedOffers.SetEquals(offers.ObjectiveOptions.Keys))
            return;

        var keys = offers.ObjectiveOptions.Keys.ToArray();
        var conflicts = keys.ToDictionary(id => id, _ => new HashSet<NetEntity>());
        var targetConflicts = new Dictionary<NetEntity, HashSet<NetEntity>>();
        for (var i = 0; i < keys.Length; i++)
        {
            var uid = GetEntity(keys[i]);
            for (var j = 0; j < i; j++)
            {
                if (!Exists(uid) || !Exists(GetEntity(keys[j])) || !AreObjectivesCompatible(uid, GetEntity(keys[j])))
                {
                    conflicts[keys[i]].Add(keys[j]);
                    conflicts[keys[j]].Add(keys[i]);
                }
            }
            if (config?.DeferredTargets.Contains(keys[i]) == true && TryComp<PickRandomPersonComponent>(uid, out var pick))
                targetConflicts[keys[i]] = keys.Where(other => other != keys[i] && Exists(GetEntity(other)) &&
                    pick.Filters.OfType<TargetObjectiveMindFilter>().Any(filter =>
                        _slWhitelist.IsWhitelistPassOrNull(filter.Blacklist, GetEntity(other)))).ToHashSet();
        }
        offers.Conflicts = conflicts;
        offers.TargetConflicts = targetConflicts;
        config?.CachedOffers = keys.ToHashSet();
    }

    /// <summary>
    /// Applies any assigned objectives' YAML modifiers to the base picker budget.
    /// </summary>
    public float GetPickerDifficulty(float baseDifficulty, MindComponent mind)
    {
        var difficulty = baseDifficulty;
        foreach (var objective in mind.Objectives)
        {
            if (!TryComp<DifficultyModifierComponent>(objective, out var modifier))
                continue;
            if (!float.IsFinite(modifier.Multiplier) || modifier.Multiplier < 0 ||
                !float.IsFinite(difficulty * modifier.Multiplier))
            {
                Log.Error($"Invalid objective picker difficulty multiplier on {ToPrettyString(objective)}.");
                continue;
            }
            difficulty *= modifier.Multiplier;
        }
        return difficulty;
    }

    private void OnPickerShutdown(Entity<PotentialObjectivesComponent> ent, ref ComponentShutdown args)
    {
        foreach (var key in ent.Comp.ObjectiveOptions.Keys)
            TryQueueDel(GetEntity(key));
        // Keep the configuration and completion ledger after confirming a batch.
        if (TryComp<ObjectivePickerConfigurationComponent>(ent, out var config) && !config.Finished)
            RemCompDeferred<ObjectivePickerConfigurationComponent>(ent);
        if (config != null)
        {
            config.PickerSession = null;
            config.Refill = null;
            config.CachedOffers.Clear();
            config.TargetTokens.Clear();
        }
    }
}
