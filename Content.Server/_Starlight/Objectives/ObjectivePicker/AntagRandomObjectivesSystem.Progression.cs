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
        var progress = EnsureComp<ObjectivePickerProgressComponent>(mindId);
        progress.CanPickMore = false;
        Dirty(mindId, progress);
    }

    private void UpdateProgression()
    {
        var query = EntityQueryEnumerator<MindComponent, ObjectivePickerConfigurationComponent>();
        while (query.MoveNext(out var uid, out var mind, out var config))
        {
            if (!config.Finished || config.BatchUnlocked || config.CurrentBatch.Count == 0)
                continue;

            foreach (var objective in config.CurrentBatch)
            {
                if (Exists(objective) && _objectives.IsCompleted(objective, (uid, mind)))
                    config.CompletedObjectives.Add(objective);
            }
            var completed = config.CurrentBatch.Count(config.CompletedObjectives.Contains);
            if ((float)completed / config.CurrentBatch.Count < _unlockRequirement)
                continue;

            config.BatchUnlocked = true;
            var progress = EnsureComp<ObjectivePickerProgressComponent>(uid);
            progress.CanPickMore = true;
            Dirty(uid, progress);
        }
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
        if (HasComp<PotentialObjectivesComponent>(mindId) ||
            !TryComp<ObjectivePickerProgressComponent>(mindId, out var progress) || !progress.CanPickMore ||
            !TryComp<ObjectivePickerConfigurationComponent>(mindId, out var config) || !config.Finished)
            return false;

        var offers = new PotentialObjectivesComponent
        {
            MinimumDifficulty = config.MinimumDifficulty,
            AutoSelectionDelay = config.SelectionDelay,
            AutoSelectionTime = _timing.CurTime + config.SelectionDelay,
            MulliganUsed = config.MulliganUsed,
        };
        config.DeferredTargets.Clear();
        FillOffers(mindId, mind, offers, config, CountAssignedObjectives(mindId));
        if (!offers.ObjectiveOptions.Keys.Any(id => ObjectivePickerSelection.Available(offers, id)))
        {
            foreach (var id in offers.ObjectiveOptions.Keys)
                TryQueueDel(GetEntity(id));
            config.DeferredTargets.Clear();
            return false;
        }

        config.Finished = false;
        progress.CanPickMore = false;
        AddComp(mindId, offers);
        Dirty(mindId, offers);
        Dirty(mindId, progress);
        return true;
    }
}
