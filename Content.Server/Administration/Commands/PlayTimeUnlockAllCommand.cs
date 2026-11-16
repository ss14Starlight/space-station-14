using System.Linq;
using System.Threading.Tasks;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Administration;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Moderator)]
public sealed partial class PlayTimeUnlockAllCommand : LocalizedEntityCommands
{
    private const string CommandDepartment = "Command";
    private const string SecurityDepartment = "Security";
    private const string CentralCommandDepartment = "CentralCommand";

    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PlayTimeTrackingManager _playTimeTracking = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedRoleSystem _roles = default!;

    public override string Command => "playtime_unlockall";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteError(Loc.GetString("cmd-playtime_unlockall-error-args"));
            return;
        }

        var userName = args[0];
        var includeCommand = false;
        var includeSecurity = false;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--command":
                    includeCommand = true;
                    break;
                case "--security":
                    includeSecurity = true;
                    break;
                default:
                    shell.WriteError(Loc.GetString("cmd-playtime_unlockall-error-args"));
                    return;
            }
        }

        var playTimes = await GetPlayTimes(userName);
        if (playTimes == null)
        {
            shell.WriteError(Loc.GetString("parse-player-record-fail", ("username", userName)));
            return;
        }

        var additions = CalculateAdditions(playTimes, includeCommand, includeSecurity);
        if (additions.Count == 0)
        {
            shell.WriteLine(Loc.GetString("cmd-playtime_unlockall-already", ("username", userName)));
            return;
        }

        var total = TimeSpan.Zero;
        foreach (var (tracker, time) in additions)
        {
            var result = await _playTimeTracking.TryAddTimeToTrackerByUserName(userName, tracker, time);
            if (result == null)
            {
                shell.WriteError(Loc.GetString("parse-player-record-fail", ("username", userName)));
                return;
            }

            total += time;
        }

        if (_playerManager.TryGetSessionByUsername(userName, out var session))
            _playTimeTracking.SaveSession(session);

        shell.WriteLine(Loc.GetString(
            "cmd-playtime_unlockall-succeed",
            ("username", userName),
            ("time", total)));
    }

    private async Task<Dictionary<string, TimeSpan>?> GetPlayTimes(string userName)
    {
        if (_playerManager.TryGetSessionByUsername(userName, out var session))
        {
            if (!_playTimeTracking.TryGetTrackerTimes(session, out _))
                return null;

            _playTimeTracking.FlushTracker(session);

            if (!_playTimeTracking.TryGetTrackerTimes(session, out var merged))
                return null;

            return new Dictionary<string, TimeSpan>(merged);
        }

        return await _playTimeTracking.TryGetPlayTimesByUserName(userName);
    }

    private Dictionary<string, TimeSpan> CalculateAdditions(
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        bool includeCommand,
        bool includeSecurity)
    {
        var additions = new Dictionary<string, TimeSpan>();
        var excludedDepartments = GetExcludedDepartments(includeCommand, includeSecurity);
        var visibleJobs = GetVisibleJobs(excludedDepartments);
        var targetableJobIds = visibleJobs
            .Where(visibleJob => visibleJob.Targetable)
            .Select(visibleJob => visibleJob.Job.ID)
            .ToHashSet();
        var targetableTrackers = visibleJobs
            .Where(visibleJob => visibleJob.Targetable)
            .Select(visibleJob => (string) visibleJob.Job.PlayTimeTracker)
            .ToHashSet();

        var excludedTrackers = new HashSet<string>();
        foreach (var tracker in _prototypes.EnumeratePrototypes<PlayTimeTrackerPrototype>())
        {
            if (tracker.ID == (string) PlayTimeTrackingShared.TrackerOverall)
                continue;

            if (!targetableTrackers.Contains(tracker.ID))
                excludedTrackers.Add(tracker.ID);
        }

        var trackerDepartments = GetTrackerDepartments(targetableJobIds);
        var roleRequirements = new Dictionary<string, TimeSpan>();
        var departmentRequirements = new Dictionary<string, TimeSpan>();
        TimeSpan? overallRequirement = null;

        foreach (var visibleJob in visibleJobs)
        {
            var requirements = _roles.GetRoleRequirements(visibleJob.Job);
            if (requirements == null)
                continue;

            foreach (var requirement in requirements)
            {
                switch (requirement)
                {
                    case RoleTimeRequirement role when !role.Inverted:
                    {
                        var tracker = (string) role.Role;
                        if (excludedTrackers.Contains(tracker))
                            break;

                        AddRequirement(roleRequirements, tracker, role.Time);
                        break;
                    }
                    case DepartmentTimeRequirement department when !department.Inverted:
                    {
                        var departmentId = (string) department.Department;
                        if (excludedDepartments.Contains(departmentId))
                            break;

                        AddRequirement(departmentRequirements, departmentId, department.Time);
                        break;
                    }
                    case OverallPlaytimeRequirement overall when !overall.Inverted:
                        if (overallRequirement == null || overall.Time > overallRequirement.Value)
                            overallRequirement = overall.Time;
                        break;
                }
            }
        }

        foreach (var (tracker, required) in roleRequirements)
            AddDeficit(additions, playTimes, tracker, required);

        var departmentOrder = departmentRequirements.Keys
            .OrderBy(id => id == CommandDepartment ? 1 : 0)
            .ThenBy(id => id)
            .ToList();

        foreach (var departmentId in departmentOrder)
        {
            var required = departmentRequirements[departmentId];
            var department = _prototypes.Index<DepartmentPrototype>(departmentId);
            var candidateTrackers = new List<string>();
            var current = TimeSpan.Zero;

            foreach (var jobId in department.Roles)
            {
                if (!_prototypes.TryIndex(jobId, out JobPrototype? departmentJob))
                    continue;

                var tracker = (string) departmentJob.PlayTimeTracker;
                current += GetCurrentTime(playTimes, additions, tracker);

                if (targetableJobIds.Contains(departmentJob.ID))
                    candidateTrackers.Add(tracker);
            }

            var deficit = required - current;
            if (deficit <= TimeSpan.Zero)
                continue;

            var target = candidateTrackers
                .Distinct()
                .OrderByDescending(tracker => trackerDepartments.GetValueOrDefault(tracker)?.Contains(CommandDepartment) == true)
                .ThenByDescending(tracker => trackerDepartments.GetValueOrDefault(tracker)?.Count ?? 0)
                .ThenBy(tracker => tracker)
                .FirstOrDefault();

            if (target != null)
                AddTime(additions, target, deficit);
        }

        if (overallRequirement != null)
            AddDeficit(additions, playTimes, (string) PlayTimeTrackingShared.TrackerOverall, overallRequirement.Value);

        return additions;
    }

    private HashSet<string> GetExcludedDepartments(bool includeCommand, bool includeSecurity)
    {
        var excluded = new HashSet<string> { CentralCommandDepartment };

        if (!includeCommand)
            excluded.Add(CommandDepartment);

        if (!includeSecurity)
            excluded.Add(SecurityDepartment);

        return excluded;
    }

    private List<VisibleJob> GetVisibleJobs(HashSet<string> excludedDepartments)
    {
        var jobDepartments = new Dictionary<string, List<DepartmentPrototype>>();
        foreach (var department in _prototypes.EnumeratePrototypes<DepartmentPrototype>())
        {
            foreach (var jobId in department.Roles)
            {
                var id = (string) jobId;
                if (!jobDepartments.TryGetValue(id, out var departments))
                {
                    departments = new List<DepartmentPrototype>();
                    jobDepartments[id] = departments;
                }

                departments.Add(department);
            }
        }

        var jobs = new List<VisibleJob>();
        foreach (var job in _prototypes.EnumeratePrototypes<JobPrototype>())
        {
            if (job.Hidden || !job.SetPreference)
                continue;

            if (!jobDepartments.TryGetValue(job.ID, out var departments))
                continue;

            var hasVisibleDepartment = false;
            var hasCentralCommandDepartment = false;
            var hasCommandDepartment = false;
            var hasSecurityDepartment = false;
            var hasDisabledDepartment = false;

            foreach (var department in departments)
            {
                if (department.ID == CentralCommandDepartment)
                {
                    hasCentralCommandDepartment = true;
                    break;
                }

                if (department.ID == CommandDepartment)
                    hasCommandDepartment = true;

                if (department.ID == SecurityDepartment)
                    hasSecurityDepartment = true;

                if (excludedDepartments.Contains(department.ID))
                    hasDisabledDepartment = true;

                if (!department.EditorHidden)
                    hasVisibleDepartment = true;
            }

            if (hasCentralCommandDepartment || !hasVisibleDepartment)
                continue;

            if (hasCommandDepartment)
            {
                if (excludedDepartments.Contains(CommandDepartment))
                    continue;
            }
            else if (hasSecurityDepartment)
            {
                if (excludedDepartments.Contains(SecurityDepartment))
                    continue;
            }

            jobs.Add(new VisibleJob(job, !hasDisabledDepartment));
        }

        return jobs;
    }

    private Dictionary<string, HashSet<string>> GetTrackerDepartments(HashSet<string> jobIds)
    {
        var trackerDepartments = new Dictionary<string, HashSet<string>>();
        foreach (var department in _prototypes.EnumeratePrototypes<DepartmentPrototype>())
        {
            foreach (var jobId in department.Roles)
            {
                if (!jobIds.Contains((string) jobId))
                    continue;

                if (!_prototypes.TryIndex(jobId, out JobPrototype? job))
                    continue;

                var tracker = (string) job.PlayTimeTracker;
                if (!trackerDepartments.TryGetValue(tracker, out var departments))
                {
                    departments = new HashSet<string>();
                    trackerDepartments[tracker] = departments;
                }

                departments.Add(department.ID);
            }
        }

        return trackerDepartments;
    }

    private static void AddRequirement(Dictionary<string, TimeSpan> requirements, string key, TimeSpan time)
    {
        if (!requirements.TryGetValue(key, out var current) || time > current)
            requirements[key] = time;
    }

    private static TimeSpan GetCurrentTime(
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        IReadOnlyDictionary<string, TimeSpan> additions,
        string tracker)
    {
        return playTimes.GetValueOrDefault(tracker) + additions.GetValueOrDefault(tracker);
    }

    private static void AddDeficit(
        Dictionary<string, TimeSpan> additions,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        string tracker,
        TimeSpan required)
    {
        var deficit = required - GetCurrentTime(playTimes, additions, tracker);
        if (deficit > TimeSpan.Zero)
            AddTime(additions, tracker, deficit);
    }

    private static void AddTime(Dictionary<string, TimeSpan> additions, string tracker, TimeSpan time)
    {
        additions[tracker] = additions.GetValueOrDefault(tracker) + time;
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _playerManager),
                Loc.GetString("cmd-playtime_unlockall-arg-user"));
        }

        if (args.Length is 2 or 3)
        {
            return CompletionResult.FromHintOptions(
                new[] { "--command", "--security" },
                Loc.GetString("cmd-playtime_unlockall-arg-flags"));
        }

        return CompletionResult.Empty;
    }

    private sealed record VisibleJob(JobPrototype Job, bool Targetable);
}
