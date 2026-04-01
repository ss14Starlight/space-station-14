using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.CCVar;
using Content.Shared.Players;
using Content.Shared.Players.JobWhitelist;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

#region Starlight
using Content.Client._Starlight.Managers;
using Content.Client.Lobby;
using Content.Shared.Starlight;
using Content.Shared._NullLink;
using Content.Shared.NullLink.CCVar;
using static Content.Shared._NullLink.NullLink;
using Microsoft.CodeAnalysis;
#endregion Starlight

namespace Content.Client.Players.PlayTimeTracking;

public sealed class JobRequirementsManager : ISharedPlaytimeManager
{
    [Dependency] private readonly IBaseClient _client = default!;
    [Dependency] private readonly IClientNetManager _net = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private readonly List<string> _jobBans = new();
    private readonly List<string> _antagBans = new();
    private readonly List<string> _jobWhitelists = new();

    // nulllink start
    private Dictionary<string, TimeSpan> _originalRoles = [];
    private readonly Dictionary<string, TimeSpan> _mergedRoles = new();
    private Dictionary<string, Dictionary<string, TimeSpan>> _rolesPerServer = [];
    private ServerPlaytimeRecognitionPrototype? _serverPlaytimeRecognition;
    private string? _project;
    private string? _server;
    // nulllink end

    private ISawmill _sawmill = default!;

    public event Action? Updated;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("job_requirements");

        // Yeah the client manager handles role bans and playtime but the server ones are separate DEAL.
        _net.RegisterNetMessage<MsgRoleBans>(RxRoleBans);
        _net.RegisterNetMessage<MsgPlayTime>(RxPlayTime);
        _net.RegisterNetMessage<MsgJobWhitelist>(RxJobWhitelist);

        // NullLink start
        _net.RegisterNetMessage<MsgUpdatePlayerPlayTime>(Update);
        _cfg.OnValueChanged(NullLinkCCVars.Project, x => _project = x, true);
        _cfg.OnValueChanged(NullLinkCCVars.Server, x => _server = x, true);
        // NullLink end

        _client.RunLevelChanged += ClientOnRunLevelChanged;
    }

    // Nulllink start
    private void Update(MsgUpdatePlayerPlayTime message)
    {
        _rolesPerServer = message.RolePlayTimePerServer;
        MergePlayTime();
    }

    private void MergePlayTime()
    {
        _mergedRoles.Clear();

        foreach (var (tracker, time) in _originalRoles)
            _mergedRoles[tracker] = time;

        if (_server is null || _project is null)
            return;

        if (_serverPlaytimeRecognition is null)
        {
            if (!_prototypes.TryIndex<ServerPlaytimeRecognitionPrototype>(_project, out var serverPlaytimeRecognition))
                return;

            _serverPlaytimeRecognition = serverPlaytimeRecognition;
        }

        if (_serverPlaytimeRecognition?.Recognition.TryGetValue(_server, out var servers) is true)
        {
            foreach (var server in servers)
            {
                if (_rolesPerServer.TryGetValue(server, out var rolesForServer))
                {
                    foreach (var (tracker, time) in rolesForServer)
                    {
                        if (_mergedRoles.ContainsKey(tracker))
                            _mergedRoles[tracker] += time;
                        else
                            _mergedRoles[tracker] = time;
                    }
                }
            }
        }

        Updated?.Invoke();
    }
    // Nulllink end

    private void ClientOnRunLevelChanged(object? sender, RunLevelChangedEventArgs e)
    {
        if (e.NewLevel == ClientRunLevel.Initialize)
        {
            // Reset on disconnect, just in case.
            // NullLink start
            _originalRoles.Clear();
            _mergedRoles.Clear();
            _rolesPerServer.Clear();
            // NullLink end
            _jobWhitelists.Clear();
            _jobBans.Clear();
            _antagBans.Clear();
        }
    }

    private void RxRoleBans(MsgRoleBans message)
    {
        _sawmill.Debug($"Received role ban info: {message.JobBans.Count} job ban entries and {message.AntagBans.Count} antag ban entries.");

        _jobBans.Clear();
        _jobBans.AddRange(message.JobBans);
        _antagBans.Clear();
        _antagBans.AddRange(message.AntagBans);
        Updated?.Invoke();
    }

    private void RxPlayTime(MsgPlayTime message)
    {
        // NullLink start
        _originalRoles = message.Trackers;

        MergePlayTime();
        //// NOTE: do not assign _roles = message.Trackers due to implicit data sharing in integration tests.
        //foreach (var (tracker, time) in message.Trackers)
        //{
        //    _originalRoles[tracker] = time;
        //}

        ///*var sawmill = Logger.GetSawmill("play_time");
        //foreach (var (tracker, time) in _roles)
        //{
        //    sawmill.Info($"{tracker}: {time}");
        //}*/
        //Updated?.Invoke();
        // NullLink end
    }

    private void RxJobWhitelist(MsgJobWhitelist message)
    {
        _jobWhitelists.Clear();
        _jobWhitelists.AddRange(message.Whitelist);
        Updated?.Invoke();
    }

    /// <summary>
    /// Check a list of job- and antag prototypes against the current player, for requirements and bans.
    /// </summary>
    /// <returns>
    /// False if any of the prototypes are banned or have unmet requirements.
    /// </returns>>
    public bool IsAllowed(
        List<ProtoId<JobPrototype>>? jobs,
        List<ProtoId<AntagPrototype>>? antags,
        HumanoidCharacterProfile? profile,
        out FormattedMessage reason) // Starlight: Always return reason
    {
        reason = new FormattedMessage(); // Starlight

        if (antags is not null)
        {
            foreach (var proto in antags)
            {
                if (!IsAllowed(_prototypes.Index(proto), profile, out reason))
                    return false;
            }
        }

        if (jobs is not null)
        {
            foreach (var proto in jobs)
            {
                if (!IsAllowed(_prototypes.Index(proto), profile, out reason))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Check the job prototype against the current player, for requirements and bans
    /// </summary>
    public bool IsAllowed(
        JobPrototype job,
        HumanoidCharacterProfile? profile,
        out FormattedMessage reason) // Starlight: Always return reason
    {
        // Check the player's bans
        if (_jobBans.Contains(job.ID))
        {
            reason = FormattedMessage.FromMarkupPermissive(Loc.GetString("role-ban")); // Starlight: Formatted
            return false;
        }

        // Check whitelist requirements
        if (!CheckWhitelist(job, out reason))
            return false;

        var player = _playerManager.LocalSession;
        if (player == null)
            return true;

        // Check other role requirements
        var reqs = _entManager.System<SharedRoleSystem>().GetRoleRequirements(job);
        if (!CheckRoleRequirements(reqs, player, profile, out reason))
            return false;

        return true;
    }

    /// <summary>
    /// Check the antag prototype against the current player, for requirements and bans
    /// </summary>
    public bool IsAllowed(
        AntagPrototype antag,
        HumanoidCharacterProfile? profile,
        out FormattedMessage reason) // Starlight: Always return reason
    {
        // Check the player's bans
        if (_antagBans.Contains(antag.ID))
        {
            reason = FormattedMessage.FromUnformatted(Loc.GetString("role-ban"));
            return false;
        }

        // Check whitelist requirements
        if (!CheckWhitelist(antag, out reason))
            return false;

        var player = _playerManager.LocalSession;
        if (player == null)
            return true;

        // Check other role requirements
        var reqs = _entManager.System<SharedRoleSystem>().GetRoleRequirements(antag);
        if (!CheckRoleRequirements(reqs, player, profile, out reason))
            return false;

        return true;
    }

    /// <summary>
    /// SL: Check against a requirements list without a role. Avoid using if there's a role, as this doesn't check bans.
    /// </summary>
    public bool CheckRequirementsForNonRole(HashSet<JobRequirement>? requirements, ICommonSession? player, HumanoidCharacterProfile? profile, out FormattedMessage reason) // Starlight: Always return reason
    {
        return CheckRoleRequirements(requirements, player, profile, out reason);
    }

    // This must be private so code paths can't accidentally skip requirement overrides. Call this through IsAllowed()
    private bool CheckRoleRequirements(HashSet<JobRequirement>? requirements, ICommonSession? player, HumanoidCharacterProfile? profile, out FormattedMessage reason) // Starlight: Always return reason
    {
        reason = new FormattedMessage(); // Starlight

        if (requirements == null || !_cfg.GetCVar(CCVars.GameRoleTimers))
            return true;

        var success = true; // Starlight
        foreach (var requirement in requirements)
        {
            if (!requirement.Check(_entManager, player, _prototypes, profile, _mergedRoles, out var checkDetails))
                success = false; // Starlight
            
            if (!reason.IsEmpty) // Starlight BEGIN
                reason.PushNewline();
            reason.AddMessage(checkDetails); // Starlight END
        }
        
        return success; // Starlight
    }

    public bool CheckWhitelist(JobPrototype job, out FormattedMessage reason) // Starlight: Always return reason
    {
        reason = FormattedMessage.FromMarkupPermissive(Loc.GetString("role-whitelisted")); // Starlight: Markup

        if (!_cfg.GetCVar(CCVars.GameRoleWhitelist))
            return true;

        if (job.Whitelisted && !_jobWhitelists.Contains(job.ID))
        {
            reason = FormattedMessage.FromMarkupPermissive(Loc.GetString("role-not-whitelisted")); // Starlight: Markup
            return false;
        }

        return true;
    }

    public bool CheckWhitelist(AntagPrototype antag, out FormattedMessage reason) // Starlight: Always return reason
    {
        reason = FormattedMessage.Empty; // Starlight

        // TODO: Implement antag whitelisting.

        return true;
    }

    // nullink
    public TimeSpan GetServerPlaytime(string server, string tracker)
        => _rolesPerServer.TryGetValue(server, out var rolesForServer)
            && rolesForServer.TryGetValue(tracker, out var time)
            ? time
            : TimeSpan.Zero;

    // nullink
    public TimeSpan FetchOverallPlaytime()
        => _mergedRoles
            .TryGetValue("Overall", out var overallPlaytime) ? overallPlaytime : TimeSpan.Zero;

    //starlight edit, string changed to JobPrototype
    public IEnumerable<KeyValuePair<JobPrototype, TimeSpan>> FetchPlaytimeByRoles()
    {
        var jobsToMap = _prototypes.EnumeratePrototypes<JobPrototype>();

        foreach (var job in jobsToMap)
        {
            if (_mergedRoles.TryGetValue(job.PlayTimeTracker, out var locJobName))
            {
                yield return new KeyValuePair<JobPrototype, TimeSpan>(job, locJobName);
            }
        }
    }

    //Starlight
    public IEnumerable<KeyValuePair<DepartmentPrototype, TimeSpan>> FetchPlaytimeByDepartments()
    {
        var departmentsToMap = _prototypes.EnumeratePrototypes<DepartmentPrototype>();

        foreach (var department in departmentsToMap)
        {
            //bulk up time
            var departmentTime = TimeSpan.Zero;

            foreach (var job in department.Roles)
            {
                //get it as the actual type
                if (!_prototypes.TryIndex(job, out JobPrototype? jobProto))
                    continue;

                if (_mergedRoles.TryGetValue(jobProto.PlayTimeTracker, out var time))
                {
                    departmentTime += time;
                }
            }

            //if the timer is 0, skip
            if (departmentTime == TimeSpan.Zero)
                continue;

            yield return new KeyValuePair<DepartmentPrototype, TimeSpan>(department, departmentTime);
        }
    }

    /// <summary>
    /// Fetches playtime per antag prototype.
    /// </summary>b
    public IEnumerable<KeyValuePair<AntagPrototype, TimeSpan>> FetchPlaytimeByAntags()
    {
        var antagsToMap = _prototypes.EnumeratePrototypes<AntagPrototype>();
        foreach (var antag in antagsToMap)
        {
            if (antag.PlayTimeTracker == null)
                continue;
            
            if (_mergedRoles.TryGetValue(antag.PlayTimeTracker, out var time))
                yield return new KeyValuePair<AntagPrototype, TimeSpan>(antag, time);
        }
    }

    /// <summary>
    /// Fetches playtime for all PlayTimeTracker prototypes that we don't see in any job or antag.
    /// This covers ghost roles and various admin spawns.
    /// </summary>
    public IEnumerable<KeyValuePair<PlayTimeTrackerPrototype, TimeSpan>> FetchPlaytimeMiscellaneous(
        IEnumerable<KeyValuePair<JobPrototype, TimeSpan>> jobPlaytimes,
        IEnumerable<KeyValuePair<AntagPrototype, TimeSpan>> antagPlaytimes)
    {
        var trackers = _prototypes.EnumeratePrototypes<PlayTimeTrackerPrototype>();
        var exclude = new HashSet<string> { "Overall" };
        foreach (var jobPlaytime in jobPlaytimes)
            exclude.Add(jobPlaytime.Key.PlayTimeTracker);
        foreach (var antagPlaytime in antagPlaytimes)
            if (antagPlaytime.Key.PlayTimeTracker != null)
                exclude.Add(antagPlaytime.Key.PlayTimeTracker);
        
        foreach (var tracker in trackers)
        {
            if (exclude.Contains(tracker.ID))
                continue;

            if (!_mergedRoles.TryGetValue(tracker.ID, out var rolePlaytime))
                continue;
            
            yield return new KeyValuePair<PlayTimeTrackerPrototype, TimeSpan>(tracker, rolePlaytime);
        }
    }
    //starlight end

    public IReadOnlyDictionary<string, TimeSpan> GetPlayTimes(ICommonSession session)
    {
        if (session != _playerManager.LocalSession)
        {
            return new Dictionary<string, TimeSpan>();
        }

        return _mergedRoles;
    }
}
