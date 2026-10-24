using System.Linq;
using Content.Server.Antag.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared._Starlight.Objectives.ObjectivePicker;
using Content.Server._Starlight.Objectives.ObjectivePicker;

namespace Content.Server.Objectives;

public sealed partial class ObjectivesSystem
{
    /// <summary>
    /// Keep picker participants visible even after their game rule stops being active.
    /// </summary>
    private void CollectPickerSummaries(Dictionary<string, Dictionary<string, List<(EntityUid, string)>>> summaries)
    {
        var listed = summaries.Values.SelectMany(groups => groups.Values).SelectMany(minds => minds)
            .Select(identity => identity.Item1).ToHashSet();
        var query = EntityQueryEnumerator<MindComponent, ObjectivePickerConfigurationComponent>();
        while (query.MoveNext(out var uid, out var mind, out var config))
        {
            if (listed.Contains(uid) || config.Rule is not { } rule ||
                !TryComp<AntagSelectionComponent>(rule, out var selection) || selection.AgentName is not { } agentName)
                continue;

            var agent = Loc.GetString(agentName);
            var prepend = new ObjectivesTextPrependEvent("");
            RaiseLocalEvent(rule, ref prepend);
            if (!summaries.TryGetValue(agent, out var groups))
                summaries[agent] = groups = new();
            if (!groups.TryGetValue(prepend.Text, out var minds))
                groups[prepend.Text] = minds = new();
            minds.Add((uid, mind.CharacterName ?? "anonymous"));
            listed.Add(uid);
        }
    }

    private float AppendDifficultySummary(EntityUid mindId, MindComponent mind,
        System.Text.StringBuilder summary, float legacyRate)
    {
        if (!HasComp<ObjectivePickerConfigurationComponent>(mindId))
            return legacyRate;

        var score = new ObjectiveDifficultyScore();
        foreach (var objective in mind.Objectives)
        {
            if (TryComp<ObjectiveComponent>(objective, out var comp))
                score = score.Add(comp.Difficulty, IsCompleted(objective, (mindId, mind)));
        }
        summary.AppendLine(Loc.GetString("objectives-round-end-difficulty",
            ("selected", ObjectiveDifficultyDisplay.Format(score.Selected)),
            ("completed", ObjectiveDifficultyDisplay.Format(score.Completed)),
            ("percentage", ObjectiveDifficultyDisplay.Format(score.Percentage))));
        return score.Completed;
    }
}
