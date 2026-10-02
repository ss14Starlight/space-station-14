using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Statistics;

public sealed partial class RoundStatisticsSystem
{
    private readonly Dictionary<ProtoId<JobPrototype>, JobStatisticsAccumulator> _jobs = [];
    private readonly Dictionary<SpeciesJobSpawnKey, int> _speciesJobSpawns = [];
    private readonly Dictionary<JobPreferenceKey, int> _jobPreferences = [];

    private void InitializeJobStatistics()
        => RegisterStatisticsDomain(EmitJobStatistics, ClearJobStatistics);

    /// <summary>
    /// Records raw demand: how many players wanted each job at each priority, whether or not they
    /// were eligible. Unlike <see cref="RecordJobCandidates"/> every job a player asked for counts.
    /// </summary>
    public void RecordJobPreferences(IReadOnlyDictionary<NetUserId, Dictionary<ProtoId<JobPrototype>, JobPriority>> preferences)
    {
        if (!EnsureRound())
            return;

        foreach (var jobs in preferences.Values)
        {
            foreach (var (job, priority) in jobs)
            {
                if (priority == JobPriority.Never)
                    continue;

                Increment(_jobPreferences, new JobPreferenceKey(job, priority));
            }
        }
    }

    /// <summary>
    /// Records the initial job slots across every station participating in round-start assignment.
    /// A null slot count means the job is unlimited.
    /// </summary>
    public void RecordInitialJobSlots(
        IReadOnlyDictionary<EntityUid, Dictionary<ProtoId<JobPrototype>, int?>> stationJobs)
    {
        if (!EnsureRound())
            return;

        foreach (var jobs in stationJobs.Values)
        {
            foreach (var (job, slots) in jobs)
            {
                var statistic = GetOrAddJob(job);
                if (!statistic.SlotsRecorded)
                {
                    statistic.AvailableSlots = slots;
                    statistic.SlotsRecorded = true;
                    continue;
                }

                if (statistic.AvailableSlots == null || slots == null)
                {
                    statistic.AvailableSlots = null;
                    continue;
                }

                statistic.AvailableSlots += slots.Value;
            }
        }
    }

    /// <summary>
    /// Records the eligible players considered for each job during an assignment pass. This counts
    /// considerations, not distinct players, since one player is weighed by several passes.
    /// </summary>
    public void RecordJobCandidates(IReadOnlyDictionary<NetUserId, List<string>> candidates)
    {
        if (!EnsureRound())
            return;

        foreach (var jobs in candidates.Values)
        {
            foreach (var job in jobs)
            {
                GetOrAddJob(new ProtoId<JobPrototype>(job)).CandidateCount++;
            }
        }
    }

    /// <summary>
    /// Records the jobs selected by the round-start assignment algorithm.
    /// </summary>
    public void RecordRoundStartJobAssignments(
        IReadOnlyDictionary<NetUserId, (ProtoId<JobPrototype>? job, EntityUid station)> assignments)
    {
        if (!EnsureRound())
            return;

        foreach (var (_, (job, _)) in assignments)
        {
            if (job != null)
                GetOrAddJob(job.Value).RoundStartAssignmentCount++;
        }
    }

    /// <summary>
    /// Records a spawned job and species combination.
    /// </summary>
    public void RecordSpeciesJobSpawn(
        ProtoId<SpeciesPrototype> species,
        ProtoId<JobPrototype> job,
        GameRunLevel spawnPhase)
    {
        if (!EnsureRound())
            return;

        Increment(_speciesJobSpawns, new SpeciesJobSpawnKey(species, job, spawnPhase));
    }

    private void RecordJobSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null || args.Silent || !args.LateJoin)
            return;

        GetOrAddJob(new ProtoId<JobPrototype>(args.JobId)).LateJoinAssignmentCount++;
    }

    private void EmitJobStatistics(RoundEndMessageEvent args)
    {
        var roundId = args.RoundId;

        foreach (var (job, statistic) in _jobs)
        {
            EmitRoundRecord(
                roundId,
                "kind=job_selection job_id={JobId} candidates={Candidates} available_slots={AvailableSlots} " +
                "slots_recorded={SlotsRecorded} slots_unlimited={SlotsUnlimited} " +
                "round_start_assignments={RoundStartAssignments} late_join_assignments={LateJoinAssignments}",
                job.Id,
                statistic.CandidateCount,
                statistic.AvailableSlots ?? 0,
                statistic.SlotsRecorded,
                statistic.SlotsRecorded && statistic.AvailableSlots == null,
                statistic.RoundStartAssignmentCount,
                statistic.LateJoinAssignmentCount);
        }

        foreach (var (key, count) in _speciesJobSpawns)
        {
            EmitRoundRecord(
                roundId,
                "kind=job_spawn species_id={SpeciesId} job_id={JobId} spawn_phase={SpawnPhase} count={Count}",
                key.Species.Id,
                key.Job.Id,
                key.SpawnPhase,
                count);
        }

        foreach (var (key, players) in _jobPreferences)
        {
            EmitRoundRecord(
                roundId,
                "kind=job_preference job_id={JobId} priority={Priority} players={Players}",
                key.Job.Id,
                key.Priority,
                players);
        }
    }

    private void ClearJobStatistics()
    {
        _jobs.Clear();
        _speciesJobSpawns.Clear();
        _jobPreferences.Clear();
    }

    private JobStatisticsAccumulator GetOrAddJob(ProtoId<JobPrototype> job)
    {
        if (_jobs.TryGetValue(job, out var statistic))
            return statistic;

        statistic = new JobStatisticsAccumulator();
        _jobs.Add(job, statistic);
        return statistic;
    }

    private sealed class JobStatisticsAccumulator
    {
        public int CandidateCount;
        public int? AvailableSlots;
        public bool SlotsRecorded;
        public int RoundStartAssignmentCount;
        public int LateJoinAssignmentCount;
    }

    private readonly record struct SpeciesJobSpawnKey(
        ProtoId<SpeciesPrototype> Species,
        ProtoId<JobPrototype> Job,
        GameRunLevel SpawnPhase);

    private readonly record struct JobPreferenceKey(ProtoId<JobPrototype> Job, JobPriority Priority);
}
