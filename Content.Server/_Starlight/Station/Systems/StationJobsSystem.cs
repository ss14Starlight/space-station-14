using Content.Server._Starlight.Statistics;
using Content.Server.Station.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server.Station.Systems;

public sealed partial class StationJobsSystem
{
    [Dependency] private RoundStatisticsSystem _roundStatistics = default!;

    /// <summary>
    /// Snapshots what every player entering round-start assignment asked for, before any of them
    /// are assigned. Only priorities backed by an enabled character count, as in the algorithm.
    /// </summary>
    private void RecordRoundJobPreferences(IReadOnlySet<NetUserId> userIds)
    {
        var preferences = new Dictionary<NetUserId, Dictionary<ProtoId<JobPrototype>, JobPriority>>(userIds.Count);

        foreach (var userId in userIds)
        {
            var playerPreferences = _serverPreferences.GetPreferences(userId);
            var characterJobs = new HashSet<ProtoId<JobPrototype>>();

            foreach (var profile in playerPreferences.Characters.Values)
            {
                if (profile is not HumanoidCharacterProfile { Enabled: true } humanoid)
                    continue;

                characterJobs.UnionWith(humanoid.JobPreferences);
            }

            var jobs = new Dictionary<ProtoId<JobPrototype>, JobPriority>();
            foreach (var (job, priority) in playerPreferences.JobPriorities)
            {
                if (characterJobs.Contains(job))
                    jobs[job] = priority;
            }

            if (jobs.Count > 0)
                preferences[userId] = jobs;
        }

        _roundStatistics.RecordJobPreferences(preferences);
    }

    /// <summary>
    /// Removes one job from a player on this station, keeping their other jobs.
    /// </summary>
    public bool TryRemovePlayerJob(Entity<StationJobsComponent?> station,
        NetUserId userId,
        ProtoId<JobPrototype> job)
    {
        if (!Resolve(station, ref station.Comp, false))
            return false;

        if (!station.Comp.PlayerJobs.TryGetValue(userId, out var jobs) || !jobs.Remove(job))
            return false;

        if (jobs.Count == 0)
            station.Comp.PlayerJobs.Remove(userId);

        return true;
    }
}
