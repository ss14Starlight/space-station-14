using System.Linq;
using Content.Shared._Starlight.Mind.Events;
using Content.Shared._Starlight.Objectives.ObjectivePicker;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Roles;
using Content.Shared.GameTicking;
using Robust.Shared.Enums;

namespace Content.Server._Starlight.Objectives.ObjectivePicker;

public sealed partial class AntagRandomObjectivesSystem
{
    private static readonly TimeSpan _pickerRefreshInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _pickerRefreshCooldown = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _progressCheckInterval = TimeSpan.FromSeconds(1);
    private const int PickersPerTick = 2;
    private const int RefillAttemptsPerTick = 4;
    private readonly Queue<EntityUid> _pickerRefreshQueue = new();
    private readonly Queue<EntityUid> _progressionQueue = new();
    private long _eligibilityRevision;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MindComponent, ObjectivePickerConfigurationComponent>();
        while (query.MoveNext(out var uid, out _, out var config))
        {
            if (config.Finished)
            {
                if (!config.BatchUnlocked && config.CurrentBatch.Count > 0 &&
                    now >= config.NextProgressCheck && !config.ProgressQueued)
                {
                    config.ProgressQueued = true;
                    _progressionQueue.Enqueue(uid);
                }
                continue;
            }

            if (config.PickerSession is { } session &&
                (session.Status != SessionStatus.InGame || !_mind.TryGetMind(session, out var current, out _) || current != uid))
                config.PickerSession = null;

            if (!TryComp<PotentialObjectivesComponent>(uid, out var offers))
                continue;
            var expired = now >= offers.AutoSelectionTime;
            if (config.Refill != null ||
                (expired && config.LastTimeoutRevision != _eligibilityRevision) ||
                (config.PickerSession != null && now >= config.NextPickerRefresh))
                QueuePickerRefresh(uid, config);
        }

        AssignedObjectiveCounts? counts = null;
        for (var i = 0; i < PickersPerTick && _pickerRefreshQueue.TryDequeue(out var uid); i++)
        {
            if (!TryComp<ObjectivePickerConfigurationComponent>(uid, out var config))
                continue;
            config.RefreshQueued = false;
            if (config.Finished || !TryComp<MindComponent>(uid, out var mind) ||
                !TryComp<PotentialObjectivesComponent>(uid, out var offers))
                continue;
            if (config.PickerSession == null && now < offers.AutoSelectionTime)
            {
                // Closing the window also pauses an unfinished background refill.
                config.Refill = null;
                config.LastRefillRevision = -1;
                continue;
            }

            counts ??= CountAssignedObjectives();
            var otherCounts = counts.Excluding(uid);
            RefreshPicker(uid, mind, offers, config, otherCounts, RefillAttemptsPerTick);
            config.NextPickerRefresh = now + _pickerRefreshInterval;
            if (now < offers.AutoSelectionTime)
                continue;

            // Finish the bounded replacement pass before permitting a below-budget timeout fallback.
            if (config.Refill != null)
                continue;

            config.LastTimeoutRevision = _eligibilityRevision;
            if (TryAutoSelectObjectives(uid, offers, otherCounts))
                counts = null; // A confirmation changes the counts used by the rest of this batch.
        }

        // One locked batch per tick preserves the completion ledger without a synchronized progress pass.
        if (_progressionQueue.TryDequeue(out var progressMind) &&
            TryComp<ObjectivePickerConfigurationComponent>(progressMind, out var progressConfig))
        {
            progressConfig.ProgressQueued = false;
            if (now >= progressConfig.NextProgressCheck && TryComp<MindComponent>(progressMind, out var mind))
                UpdateProgression(progressMind, mind, progressConfig);
        }
    }

    private void QueuePickerRefresh(EntityUid mindId, ObjectivePickerConfigurationComponent config)
    {
        if (config.RefreshQueued)
            return;
        config.RefreshQueued = true;
        _pickerRefreshQueue.Enqueue(mindId);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _pickerRefreshQueue.Clear();
        _progressionQueue.Clear();
        _eligibilityRevision++;
        var query = EntityQueryEnumerator<ObjectivePickerConfigurationComponent>();
        while (query.MoveNext(out _, out var config))
        {
            config.RefreshQueued = false;
            config.ProgressQueued = false;
            config.PickerSession = null;
            config.Refill = null;
        }
    }

    private void OnPickerSetOpen(ObjectivePickerSetOpen ev, EntitySessionEventArgs args)
    {
        if (!_mind.TryGetMind(args.SenderSession, out var mindId, out var mind) || GetNetEntity(mindId) != ev.MindId ||
            !TryComp<ObjectivePickerConfigurationComponent>(mindId, out var config) || config.Finished ||
            !TryComp<PotentialObjectivesComponent>(mindId, out var offers))
            return;

        config.PickerSession = ev.Open ? args.SenderSession : null;
        if (!ev.Open)
            return;

        // Stop people from spamming the picker by rapidly reopening it.
        var now = _timing.CurTime;
        if (now < config.NextPickerRefresh - _pickerRefreshInterval + _pickerRefreshCooldown)
            return;

        // Reopening a window must not display stale eligibility until the next maintenance interval.
        RefreshPicker(mindId, mind, offers, config, CountAssignedObjectives(mindId), RefillAttemptsPerTick);
        config.NextPickerRefresh = now + _pickerRefreshInterval;
    }

    private void RefreshPicker(EntityUid uid, MindComponent mind, PotentialObjectivesComponent offers,
        ObjectivePickerConfigurationComponent config, Dictionary<string, int> counts, int attemptBudget)
    {
        var previous = offers.UnavailableObjectives;
        var targetPools = offers.TargetPools;
        var count = offers.ObjectiveOptions.Count;
        UpdateOfferState(uid, mind, offers, counts, config);
        var changed = !previous.SetEquals(offers.UnavailableObjectives) || !SamePools(targetPools, offers.TargetPools);
        var countsChanged = config.RefillCounts == null || config.RefillCounts.Count != counts.Count ||
            counts.Any(pair => config.RefillCounts.GetValueOrDefault(pair.Key) != pair.Value);

        if ((config.Refill != null || !OffersViable(offers)) && (config.Refill != null || changed || countsChanged ||
            config.LastRefillRevision != _eligibilityRevision))
        {
            if (changed || countsChanged || config.LastRefillRevision != _eligibilityRevision)
                config.Refill = null;
            FillOffers(uid, mind, offers, config, counts, attemptBudget: attemptBudget);
        }

        if (!previous.SetEquals(offers.UnavailableObjectives) || count != offers.ObjectiveOptions.Count ||
            !SamePools(targetPools, offers.TargetPools))
            Dirty(uid, offers);
    }

    private static bool SamePools(Dictionary<NetEntity, HashSet<int>> first,
        Dictionary<NetEntity, HashSet<int>> second) => first.Count == second.Count && first.All(pair =>
            second.TryGetValue(pair.Key, out var pool) && pair.Value.SetEquals(pool));

    private void InvalidatePickers()
    {
        _eligibilityRevision++;
        // Combine any number of changes into one queued refresh. Closed windows wait for an action/deadline.
        var query = EntityQueryEnumerator<ObjectivePickerConfigurationComponent>();
        while (query.MoveNext(out var uid, out var config))
        {
            if (!config.Finished && config.PickerSession != null)
                QueuePickerRefresh(uid, config);
        }
    }

    private void OnObjectivesChanged(Entity<MindComponent> ent, ref MindObjectivesChangedEvent args) => InvalidatePickers();
    private void OnRoleAdded(Entity<MindComponent> ent, ref RoleAddedEvent args) => InvalidatePickers();
    private void OnRoleRemoved(Entity<MindComponent> ent, ref RoleRemovedEvent args) => InvalidatePickers();
    private void OnMindAdded(Entity<MindComponent> ent, ref MindGotAddedEvent args) => InvalidatePickers();
    private void OnMindRemoved(Entity<MindComponent> ent, ref MindGotRemovedEvent args) => InvalidatePickers();
    private void OnTargetMobStateChanged(Entity<MindContainerComponent> ent, ref MobStateChangedEvent args) => InvalidatePickers();

    private void SubscribeEligibilityChanges<T>() where T : Component
    {
        SubscribeLocalEvent<T, ComponentStartup>(OnEligibilityStartup);
        SubscribeLocalEvent<T, ComponentShutdown>(OnEligibilityShutdown);
    }

    private void OnEligibilityStartup<T>(Entity<T> ent, ref ComponentStartup args) where T : Component => InvalidatePickers();
    private void OnEligibilityShutdown<T>(Entity<T> ent, ref ComponentShutdown args) where T : Component => InvalidatePickers();
}
