using System.Linq;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared._Starlight.Objectives.ObjectivePicker;

namespace Content.Server._Starlight.Objectives.ObjectivePicker;

public sealed partial class AntagRandomObjectivesSystem
{
    private readonly float _unlockRequirement = .75f; // The fraction of objectives that need to be completed before unlocking the next batch.
    private void RecordConfirmedBatch(EntityUid mindId, ObjectivePickerConfigurationComponent config,
        IEnumerable<NetEntity> selected)
    {
        config.CurrentBatch.Clear();
        foreach (var id in selected)
        {
            var objective = GetEntity(id);
            if (TryComp<ObjectiveComponent>(objective, out var comp) && comp.Difficulty > 0 &&
                !config.CompletedObjectives.Contains(objective))
                config.CurrentBatch.Add(objective);
        }
        config.BatchUnlocked = false;
        config.NextProgressCheck = _timing.CurTime + _progressCheckInterval;
        var progress = EnsureComp<ObjectivePickerProgressComponent>(mindId);
        progress.CanPickMore = false;
        Dirty(mindId, progress);
    }

    /// <summary>
    /// Reuses progress already calculated for the character menu.
    /// </summary>
    public void RecordObjectiveProgress(EntityUid mindId, EntityUid objective, float value)
    {
        if (!TryComp<ObjectivePickerConfigurationComponent>(mindId, out var config) ||
            !config.Finished || config.BatchUnlocked || !config.CurrentBatch.Contains(objective))
            return;

        if (value >= 0.999f)
            config.CompletedObjectives.Add(objective);
        config.NextProgressCheck = _timing.CurTime + _progressCheckInterval;
        TryUnlockBatch(mindId, config);
    }

    private void UpdateProgression(EntityUid uid, MindComponent mind, ObjectivePickerConfigurationComponent config)
    {
        config.NextProgressCheck = _timing.CurTime + _progressCheckInterval;
        if (!config.Finished || config.BatchUnlocked || config.CurrentBatch.Count == 0)
            return;

        foreach (var objective in config.CurrentBatch)
        {
            if (!config.CompletedObjectives.Contains(objective) && Exists(objective) &&
                _objectives.IsCompleted(objective, (uid, mind)))
                config.CompletedObjectives.Add(objective);
        }
        TryUnlockBatch(uid, config);
    }

    private void TryUnlockBatch(EntityUid mindId, ObjectivePickerConfigurationComponent config)
    {
        if (config.CurrentBatch.Count == 0 ||
            (float)config.CurrentBatch.Count(config.CompletedObjectives.Contains) / config.CurrentBatch.Count < _unlockRequirement)
            return;

        config.BatchUnlocked = true;
        var progress = EnsureComp<ObjectivePickerProgressComponent>(mindId);
        progress.CanPickMore = true;
        Dirty(mindId, progress);
    }

    private void OnRequestAdditionalObjectives(ObjectivePickerRequestAdditional ev, EntitySessionEventArgs args)
    {
        if (!_mind.TryGetMind(args.SenderSession, out var mindId, out var mind) || GetNetEntity(mindId) != ev.MindId)
            return;

        var accepted = TryCreateAdditionalPicker(mindId, mind);
        RaiseNetworkEvent(new ObjectivePickerReply
        {
            Accepted = accepted,
            OpenPicker = accepted,
            Message = accepted ? null : "objective-picker-additional-rejected",
        }, args.SenderSession);
    }

    private bool TryCreateAdditionalPicker(EntityUid mindId, MindComponent mind)
    {
        if (!TryComp<ObjectivePickerConfigurationComponent>(mindId, out var config) || !config.Finished)
            return false;
        UpdateProgression(mindId, mind, config);
        if (HasComp<PotentialObjectivesComponent>(mindId) ||
            !config.BatchUnlocked || !TryComp<ObjectivePickerProgressComponent>(mindId, out var progress) || !progress.CanPickMore)
            return false;

        var offers = new PotentialObjectivesComponent
        {
            MinimumDifficulty = config.MinimumDifficulty,
            AutoSelectionDelay = config.SelectionDelay,
            AutoSelectionTime = _timing.CurTime + config.SelectionDelay,
            MulliganUsed = config.MulliganUsed,
        };
        config.DeferredTargets.Clear();
        config.CachedOffers.Clear();
        config.TargetTokens.Clear();
        config.Refill = null;
        FillOffers(mindId, mind, offers, config, CountAssignedObjectives(mindId));
        if (!offers.ObjectiveOptions.Keys.Any(id => ObjectivePickerSelection.Available(offers, id)))
        {
            foreach (var id in offers.ObjectiveOptions.Keys)
                TryQueueDel(GetEntity(id));
            config.DeferredTargets.Clear();
            return false;
        }

        config.Finished = false;
        config.LastTimeoutRevision = -1;
        progress.CanPickMore = false;
        AddComp(mindId, offers);
        Dirty(mindId, offers);
        Dirty(mindId, progress);
        return true;
    }
}
