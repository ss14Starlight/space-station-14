using System.Linq;
using Content.Server.Objectives.Components;
using Content.Shared._Starlight.Objectives.Targeting;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared._Starlight.Objectives.ObjectivePicker;

namespace Content.Server._Starlight.Objectives.ObjectivePicker;

public sealed partial class AntagRandomObjectivesSystem
{
    private void OnObjectivesSelected(ObjectivePickerSelected ev, EntitySessionEventArgs args)
    {
        if (!_mind.TryGetMind(args.SenderSession, out var mindId, out _) || GetNetEntity(mindId) != ev.MindId)
            return;

        var accepted = TryApplySelectedObjectives(mindId, ev.SelectedObjectives);
        RaiseNetworkEvent(new ObjectivePickerReply
        {
            Accepted = accepted,
            Finished = accepted,
            Message = accepted ? null : "objective-picker-selection-rejected",
        }, args.SenderSession);
    }

    private bool TryApplySelectedObjectives(EntityUid mindId, HashSet<NetEntity> selected, bool timeout = false,
        Dictionary<string, int>? counts = null, bool refreshed = false)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) ||
            !TryComp<PotentialObjectivesComponent>(mindId, out var offers) || offers.ObjectiveOptions.Count == 0)
            return false;

        counts ??= CountAssignedObjectives(mindId);
        if (!refreshed)
            UpdateOfferState(mindId, mind, offers, counts);
        Dirty(mindId, offers);
        if (!ObjectivePickerSelection.Valid(offers, selected, timeout))
            return false;

        // Only confirmed offers get targets. Temporary membership lets target filters
        // see earlier selections in this batch; no after-assignment side effects run until all succeed.
        if (!TryComp<ObjectivePickerConfigurationComponent>(mindId, out var config) || config.Finished)
            return false;
        if (!ObjectivePickerSelection.TryAssignTargets(offers, selected, out var targets,
                id => GetTargetOrder(offers, config, id)))
            return false;
        var initialized = new List<NetEntity>();
        var temporary = new List<EntityUid>();
        var success = true;
        foreach (var id in selected.Where(id => !config.DeferredTargets.Contains(id)))
        {
            var uid = GetEntity(id);
            mind.Objectives.Add(uid);
            temporary.Add(uid);
        }
        foreach (var id in selected.Where(config.DeferredTargets.Contains)
                    .OrderByDescending(id => Prototype(GetEntity(id))?.ID.EndsWith("HeadObjective") == true))
        {
            var uid = GetEntity(id);
            initialized.Add(id);
            _slTarget.SetTarget(uid, config.TargetTokens[targets[id]]);
            var assigned = new ObjectiveAssignedEvent(mindId, mind);
            RaiseLocalEvent(uid, ref assigned);
            if (assigned.Cancelled || !TryComp<TargetObjectiveComponent>(uid, out var target) || target.Target == null)
            {
                success = false;
                break;
            }
            mind.Objectives.Add(uid);
            temporary.Add(uid);
        }
        foreach (var uid in temporary)
            mind.Objectives.Remove(uid);

        if (!success)
        {
            // Discard partially initialized targets without disclosing any of them or marking teachers.
            foreach (var id in initialized)
            {
                var uid = GetEntity(id);
                var prototype = Prototype(uid)?.ID;
                offers.ObjectiveOptions.Remove(id);
                offers.Difficulties.Remove(id);
                offers.TargetPools.Remove(id);
                offers.TargetConflicts.Remove(id);
                config.DeferredTargets.Remove(id);
                if (offers.RetainedObjective == id)
                    offers.RetainedObjective = null;
                Del(uid);
                if (prototype != null && CreateOffer(mindId, mind, prototype, counts,
                        out var replacement, out var deferred))
                    AddOffer(mindId, mind, offers, config, replacement, deferred);
            }
            config.Refill = null;
            FillOffers(mindId, mind, offers, config, counts,
                attemptBudget: timeout ? RefillAttemptsPerTick : int.MaxValue);
            Dirty(mindId, offers);
            return false;
        }

        foreach (var id in initialized)
        {
            var uid = GetEntity(id);
            var after = new ObjectiveAfterAssignEvent(mindId, mind, Comp<ObjectiveComponent>(uid), MetaData(uid));
            RaiseLocalEvent(uid, ref after);
        }
        foreach (var id in selected)
            _mind.AddObjective(mindId, mind, GetEntity(id));
        foreach (var id in offers.ObjectiveOptions.Keys.Where(id => !selected.Contains(id)))
            TryQueueDel(GetEntity(id));

        // Clear synchronously to make repeat submissions harmless before deferred removal.
        offers.ObjectiveOptions.Clear();
        RecordConfirmedBatch(mindId, config, selected);
        config.Finished = true;
        Dirty(mindId, mind);
        RemCompDeferred<PotentialObjectivesComponent>(mindId);
        config.PickerSession = null;
        config.Refill = null;
        return true;
    }

    private bool TryAutoSelectObjectives(EntityUid mindId, PotentialObjectivesComponent offers,
        Dictionary<string, int> counts)
    {
        var order = offers.ObjectiveOptions.Keys.OrderBy(_ => _random.Next()).ToArray();
        var retained = offers.RetainedObjective is { } id && ObjectivePickerSelection.Available(offers, id)
            ? new[] { id } : Array.Empty<NetEntity>();
        return ObjectivePickerSelection.TrySelect(offers, order, retained, out var selected, ignoreMulligan: true) &&
            TryApplySelectedObjectives(mindId, selected, timeout: true, counts: counts, refreshed: true);
    }

    private void OnMulligan(ObjectivePickerMulligan ev, EntitySessionEventArgs args)
    {
        if (!_mind.TryGetMind(args.SenderSession, out var mindId, out var mind) || GetNetEntity(mindId) != ev.MindId)
            return;

        var accepted = TryMulligan(mindId, mind, ev.RetainedObjective);
        RaiseNetworkEvent(new ObjectivePickerReply
        {
            Accepted = accepted,
            Finished = false,
            Message = accepted ? "objective-picker-mulligan-success" : "objective-picker-mulligan-rejected",
        }, args.SenderSession);
    }

    private bool TryMulligan(EntityUid mindId, MindComponent mind, NetEntity retained)
    {
        if (!TryComp<PotentialObjectivesComponent>(mindId, out var offers) || offers.MulliganUsed ||
            !TryComp<ObjectivePickerConfigurationComponent>(mindId, out var oldConfig))
            return false;

        var counts = CountAssignedObjectives(mindId);
        UpdateOfferState(mindId, mind, offers, counts);
        Dirty(mindId, offers);
        if (!ObjectivePickerSelection.Available(offers, retained))
            return false;

        var rerolled = new PotentialObjectivesComponent
        {
            MinimumDifficulty = offers.MinimumDifficulty,
            ObjectiveOptions = new() { [retained] = offers.ObjectiveOptions[retained] },
            Difficulties = new() { [retained] = offers.Difficulties[retained] },
        };
        var config = new ObjectivePickerConfigurationComponent
        {
            Weights = oldConfig.Weights,
            StoryWeights = oldConfig.StoryWeights,
            PreferredOptions = oldConfig.PreferredOptions,
        };
        if (oldConfig.DeferredTargets.Contains(retained))
            config.DeferredTargets.Add(retained);

        FillOffers(mindId, mind, rerolled, config, counts, retained);
        if (!rerolled.ObjectiveOptions.Keys.Any(id => id != retained && ObjectivePickerSelection.Available(rerolled, id)))
        {
            foreach (var id in rerolled.ObjectiveOptions.Keys.Where(id => id != retained))
                TryQueueDel(GetEntity(id));
            return false;
        }

        foreach (var id in offers.ObjectiveOptions.Keys.Where(id => id != retained))
            TryQueueDel(GetEntity(id));
        offers.ObjectiveOptions = rerolled.ObjectiveOptions;
        offers.Difficulties = rerolled.Difficulties;
        offers.Conflicts = rerolled.Conflicts;
        offers.UnavailableObjectives = rerolled.UnavailableObjectives;
        offers.TargetPools = rerolled.TargetPools;
        offers.TargetConflicts = rerolled.TargetConflicts;
        oldConfig.TargetTokens = config.TargetTokens;
        offers.MulliganUsed = true;
        oldConfig.MulliganUsed = true;
        offers.RetainedObjective = retained;
        oldConfig.DeferredTargets = config.DeferredTargets;
        oldConfig.CachedOffers = config.CachedOffers;
        oldConfig.Refill = null;
        oldConfig.LastRefillRevision = _eligibilityRevision;
        oldConfig.RefillCounts = counts;
        Dirty(mindId, offers);
        return true;
    }

    private IEnumerable<int> GetTargetOrder(PotentialObjectivesComponent offers,
        ObjectivePickerConfigurationComponent config, NetEntity id)
    {
        var pick = Comp<PickRandomPersonComponent>(GetEntity(id));
        return offers.TargetPools[id].OrderBy(token =>
        {
            var mind = new Entity<MindComponent>(config.TargetTokens[token], Comp<MindComponent>(config.TargetTokens[token]));
            var weight = pick.Pool is WeightedAlivePool normal ? normal.Weight(mind, EntityManager)
                : pick.Pool is HighValueTargetsPool ? HighValueTargetsPool.Weight(mind, EntityManager) : 1;
            return -Math.Log(1 - _random.NextDouble()) / weight;
        }).ToArray();
    }
}
