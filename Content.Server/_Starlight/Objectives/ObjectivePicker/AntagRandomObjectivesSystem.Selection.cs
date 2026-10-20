using System.Linq;
using Content.Server.Objectives.Components;
using Content.Shared._Starlight.Objectives.Targeting;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared._Starlight.Objectives.ObjectivePicker;

namespace Content.Server._Starlight.Objectives.ObjectivePicker;

public sealed partial class AntagRandomObjectivesSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _slNextAvailability)
            return;
        _slNextAvailability = _timing.CurTime + TimeSpan.FromSeconds(1);
        RefreshPickers();
        UpdateProgression();
    }

    private void RefreshPickers()
    {
        var query = EntityQueryEnumerator<PotentialObjectivesComponent, MindComponent, ObjectivePickerConfigurationComponent>();
        while (query.MoveNext(out var uid, out var offers, out var mind, out var config))
        {
            if (config.Finished)
                continue;
            var previous = offers.UnavailableObjectives;
            var targetPools = offers.TargetPools;
            var count = offers.ObjectiveOptions.Count;
            var counts = CountAssignedObjectives(uid);
            UpdateOfferState(uid, mind, offers, counts);
            // Keep depleted cards visible and disabled, but supply replacements so a late chooser can finish.
            if (!OffersViable(offers))
                FillOffers(uid, mind, offers, config, counts);
            if (!previous.SetEquals(offers.UnavailableObjectives) || count != offers.ObjectiveOptions.Count ||
                targetPools.Count != offers.TargetPools.Count || targetPools.Any(pair =>
                    !offers.TargetPools.TryGetValue(pair.Key, out var pool) || !pair.Value.SetEquals(pool)))
                Dirty(uid, offers);
        }
    }

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

    private bool TryApplySelectedObjectives(EntityUid mindId, HashSet<NetEntity> selected, bool timeout = false)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) ||
            !TryComp<PotentialObjectivesComponent>(mindId, out var offers) || offers.ObjectiveOptions.Count == 0)
            return false;

        UpdateOfferState(mindId, mind, offers, CountAssignedObjectives(mindId));
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
                if (prototype != null && CreateOffer(mindId, mind, prototype, CountAssignedObjectives(mindId),
                        out var replacement, out var deferred))
                    AddOffer(mindId, mind, offers, config, replacement, deferred);
            }
            FillOffers(mindId, mind, offers, config, CountAssignedObjectives(mindId));
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
        RefreshPickers();
        return true;
    }

    public void AutoSelectObjectives(EntityUid mindId, PotentialObjectivesComponent offers)
    {
        if (!TryComp<MindComponent>(mindId, out var mind))
            return;
        UpdateOfferState(mindId, mind, offers, CountAssignedObjectives(mindId));
        var order = offers.ObjectiveOptions.Keys.OrderBy(_ => _random.Next()).ToArray();
        var retained = offers.RetainedObjective is { } id && ObjectivePickerSelection.Available(offers, id)
            ? new[] { id } : Array.Empty<NetEntity>();
        if (ObjectivePickerSelection.TrySelect(offers, order, retained, out var selected, ignoreMulligan: true))
            TryApplySelectedObjectives(mindId, selected, timeout: true);
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
