using Content.Shared.GameTicking;

namespace Content.Server._Starlight.Statistics;

public sealed partial class RoundStatisticsSystem
{
    private readonly Dictionary<string, int> _antagSpawns = [];
    private readonly Dictionary<AntagSelectionKey, AntagSelectionAccumulator> _antagSelections = [];
    private readonly Dictionary<AntagChoiceKey, int> _antagChoices = [];
    private readonly Dictionary<AntagOutcomeSourceKey, string> _antagOutcomes = [];
    private readonly Dictionary<AntagOutcomeStatKey, double> _antagOutcomeStats = [];
    private readonly Dictionary<EntityUid, DragonOutcome> _dragonOutcomes = [];
    private readonly List<CosmicCultOutcome> _cosmicCultOutcomes = [];

    private void InitializeAntagStatistics() => RegisterStatisticsDomain(EmitAntagStatistics, ClearAntagStatistics);

    /// <summary>
    /// Records an antagonist that was successfully assigned to a player.
    /// </summary>
    public void RecordAntagSpawn(string type)
    {
        if (!EnsureRound())
            return;

        Increment(_antagSpawns, type);
    }

    /// <summary>
    /// Updates the current selection snapshot for an antagonist rule and type. Repair and retry
    /// counts accumulate; a null count leaves the previous snapshot value in place.
    /// </summary>
    public void RecordAntagSelection(
        string rule,
        string type,
        int expected,
        int assigned,
        int ghostRoles,
        int forcedAssignments = 0,
        int ghostRolesCreated = 0,
        int? eligible = null,
        int? preselected = null)
    {
        if (!EnsureRound())
            return;

        var statistic = GetOrAddAntagSelection(new AntagSelectionKey(rule, type));
        statistic.Expected = expected;
        statistic.Assigned = assigned;
        statistic.GhostRoles = ghostRoles;
        statistic.ForcedAssignments += forcedAssignments;
        statistic.GhostRolesCreated += ghostRolesCreated;
        statistic.Eligible = eligible ?? statistic.Eligible;
        statistic.Preselected = preselected ?? statistic.Preselected;
    }

    /// <summary>
    /// Records a successful late-join antagonist assignment.
    /// </summary>
    public void RecordLateJoinAntagAssignment(string rule, string type)
    {
        if (!EnsureRound())
            return;

        GetOrAddAntagSelection(new AntagSelectionKey(rule, type)).LateJoinAssignments++;
    }

    /// <summary>
    /// Records an in-round choice made by an antagonist, such as a class, form or upgrade path.
    /// </summary>
    public void RecordAntagChoice(string type, string choiceType, string choiceId)
    {
        if (!EnsureRound())
            return;

        Increment(_antagChoices, new AntagChoiceKey(type, choiceType, choiceId));
    }

    /// <summary>
    /// Records the final outcome of an antagonist rule, keyed by the rule entity.
    /// </summary>
    public void RecordAntagOutcome(EntityUid source, string type, string result)
    {
        if (!EnsureRound())
            return;

        _antagOutcomes[new AntagOutcomeSourceKey(source, type)] = result;
    }

    /// <summary>
    /// Records a numeric result for an antagonist rule, such as a conversion count or an
    /// infection fraction. Values are summed across every rule instance of the same type.
    /// </summary>
    public void RecordAntagOutcomeStat(string type, string stat, double value)
    {
        if (!EnsureRound())
            return;

        Add(_antagOutcomeStats, new AntagOutcomeStatKey(type, stat), value);
    }

    /// <summary>
    /// Records the final state of a space dragon.
    /// </summary>
    public void RecordDragonOutcome(EntityUid dragon, bool alive, int rifts, int devoured)
    {
        if (!EnsureRound())
            return;

        _dragonOutcomes[dragon] = new DragonOutcome(alive, rifts, devoured); // Key the dragon entity in case of dragon+
    }

    /// <summary>
    /// Records the final state of a cosmic cult rule.
    /// </summary>
    public void RecordCosmicCultOutcome(
        string result,
        int totalCultists,
        double percentConverted,
        int entropySiphoned,
        int monumentTier)
    {
        if (!EnsureRound())
            return;

        _cosmicCultOutcomes.Add(new CosmicCultOutcome(
            result,
            totalCultists,
            percentConverted,
            entropySiphoned,
            monumentTier));
    }

    private void EmitAntagStatistics(RoundEndMessageEvent args)
    {
        var roundId = args.RoundId;

        foreach (var (type, count) in _antagSpawns)
        {
            EmitRoundRecord(
                roundId,
                "kind=antag_spawn type_id={TypeId} count={Count}",
                type,
                count);
        }

        foreach (var (key, statistic) in _antagSelections)
        {
            EmitRoundRecord(
                roundId,
                "kind=antag_selection rule_id={RuleId} type_id={TypeId} expected={Expected} eligible={Eligible} " +
                "preselected={Preselected} assigned={Assigned} ghost_roles={GhostRoles} unassigned={Unassigned} " +
                "uncovered={Uncovered} forced_assignments={ForcedAssignments} " +
                "ghost_roles_created={GhostRolesCreated} latejoin_assignments={LateJoinAssignments}",
                key.Rule,
                key.Type,
                statistic.Expected,
                statistic.Eligible,
                statistic.Preselected,
                statistic.Assigned,
                statistic.GhostRoles,
                Math.Max(0, statistic.Expected - statistic.Assigned),
                Math.Max(0, statistic.Expected - statistic.Assigned - statistic.GhostRoles),
                statistic.ForcedAssignments,
                statistic.GhostRolesCreated,
                statistic.LateJoinAssignments);
        }

        foreach (var (key, count) in _antagChoices)
        {
            EmitRoundRecord(
                roundId,
                "kind=antag_choice type_id={TypeId} choice_type={ChoiceType} choice_id={ChoiceId} count={Count}",
                key.Type,
                key.ChoiceType,
                key.Choice,
                count);
        }

        var antagOutcomeCounts = new Dictionary<AntagOutcomeKey, int>();
        foreach (var (key, result) in _antagOutcomes)
        {
            Increment(antagOutcomeCounts, new AntagOutcomeKey(key.Type, result));
        }

        foreach (var (key, count) in antagOutcomeCounts)
        {
            EmitRoundRecord(
                roundId,
                "kind=antag_outcome type_id={TypeId} result={Result} count={Count}",
                key.Type,
                key.Result,
                count);
        }

        foreach (var (key, value) in _antagOutcomeStats)
        {
            EmitRoundRecord(
                roundId,
                "kind=antag_outcome_stat type_id={TypeId} stat={Stat} value={Value}",
                key.Type,
                key.Stat,
                Number(value));
        }

        var dragonOutcomeCounts = new Dictionary<DragonOutcome, int>();
        foreach (var outcome in _dragonOutcomes.Values)
        {
            Increment(dragonOutcomeCounts, outcome);
        }

        foreach (var (key, count) in dragonOutcomeCounts)
        {
            EmitRoundRecord(
                roundId,
                "kind=antag_outcome type_id=Dragon result=completed alive={Alive} rifts={Rifts} " +
                "devoured={Devoured} count={Count}",
                key.Alive,
                key.Rifts,
                key.Devoured,
                count);
        }

        foreach (var outcome in _cosmicCultOutcomes)
        {
            EmitRoundRecord(
                roundId,
                "kind=antag_outcome type_id=CosmicCult result={Result} total_cultists={TotalCultists} " +
                "percent_converted={PercentConverted} entropy_siphoned={EntropySiphoned} " +
                "monument_tier={MonumentTier} count=1",
                outcome.Result,
                outcome.TotalCultists,
                Number(outcome.PercentConverted),
                outcome.EntropySiphoned,
                outcome.MonumentTier);
        }
    }

    private void ClearAntagStatistics()
    {
        _antagSpawns.Clear();
        _antagSelections.Clear();
        _antagChoices.Clear();
        _antagOutcomes.Clear();
        _antagOutcomeStats.Clear();
        _dragonOutcomes.Clear();
        _cosmicCultOutcomes.Clear();
    }

    private AntagSelectionAccumulator GetOrAddAntagSelection(AntagSelectionKey key)
    {
        if (_antagSelections.TryGetValue(key, out var statistic))
            return statistic;

        statistic = new AntagSelectionAccumulator();
        _antagSelections.Add(key, statistic);
        return statistic;
    }

    private sealed class AntagSelectionAccumulator
    {
        public int Expected;
        public int Eligible;
        public int Preselected;
        public int Assigned;
        public int GhostRoles;
        public int ForcedAssignments;
        public int GhostRolesCreated;
        public int LateJoinAssignments;
    }

    private readonly record struct AntagSelectionKey(string Rule, string Type);
    private readonly record struct AntagChoiceKey(string Type, string ChoiceType, string Choice);
    private readonly record struct AntagOutcomeSourceKey(EntityUid Source, string Type);
    private readonly record struct AntagOutcomeKey(string Type, string Result);
    private readonly record struct AntagOutcomeStatKey(string Type, string Stat);
    private readonly record struct DragonOutcome(bool Alive, int Rifts, int Devoured);
    private readonly record struct CosmicCultOutcome(
        string Result,
        int TotalCultists,
        double PercentConverted,
        int EntropySiphoned,
        int MonumentTier);
}
