using Content.Server.GameTicking;
using Content.Server.Objectives;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Systems;

namespace Content.Server._Starlight.Statistics;

public sealed partial class RoundStatisticsSystem
{
    /// <summary>
    /// An objective at or above this progress counts as completed, matching the round-end summary.
    /// </summary>
    private const float ObjectiveCompletionThreshold = 0.999f;

    [Dependency] private SharedObjectivesSystem _objectives = default!;

    private readonly Dictionary<ObjectiveKey, ObjectiveAccumulator> _objectiveResults = [];

    private void InitializeObjectiveStatistics()
    {
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextForObjectives);
        RegisterStatisticsDomain(EmitObjectiveStatistics, ClearObjectiveStatistics);
    }

    /// <summary>
    /// Collects every objective handed out by an active game rule, along with its final progress.
    /// </summary>
    private void OnRoundEndTextForObjectives(RoundEndTextAppendEvent args)
    {
        if (!EnsureRound())
            return;

        var query = EntityQueryEnumerator<ActiveGameRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            var info = new ObjectivesTextGetInfoEvent([], string.Empty);
            RaiseLocalEvent(uid, ref info);

            if (info.Minds.Count == 0)
                continue;

            var rule = MetaData(uid).EntityPrototype?.ID ?? "unknown";

            foreach (var (mindId, _) in info.Minds)
            {
                if (!TryComp<MindComponent>(mindId, out var mind))
                    continue;

                foreach (var objective in mind.Objectives)
                {
                    if (_objectives.GetProgress(objective, (mindId, mind)) is not { } progress)
                        continue;

                    var objectiveId = MetaData(objective).EntityPrototype?.ID ?? "unknown";
                    var statistic = GetOrAddObjective(new ObjectiveKey(rule, objectiveId));

                    statistic.Assigned++;
                    statistic.ProgressSum += progress;

                    if (progress >= ObjectiveCompletionThreshold)
                        statistic.Completed++;
                }
            }
        }
    }

    private void EmitObjectiveStatistics(RoundEndMessageEvent args)
    {
        foreach (var (key, statistic) in _objectiveResults)
        {
            EmitRoundRecord(
                args.RoundId,
                "kind=objective rule_id={RuleId} objective_id={ObjectiveId} assigned={Assigned} " +
                "completed={Completed} progress_sum={ProgressSum}",
                key.Rule,
                key.Objective,
                statistic.Assigned,
                statistic.Completed,
                Number(statistic.ProgressSum));
        }
    }

    private void ClearObjectiveStatistics()
        => _objectiveResults.Clear();

    private ObjectiveAccumulator GetOrAddObjective(ObjectiveKey key)
    {
        if (_objectiveResults.TryGetValue(key, out var statistic))
            return statistic;

        statistic = new ObjectiveAccumulator();
        _objectiveResults.Add(key, statistic);
        return statistic;
    }

    private sealed class ObjectiveAccumulator
    {
        public int Assigned;
        public int Completed;
        public double ProgressSum;
    }

    private readonly record struct ObjectiveKey(string Rule, string Objective);
}
