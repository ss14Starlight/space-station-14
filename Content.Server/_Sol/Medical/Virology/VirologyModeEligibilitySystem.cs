using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Hard-gates the Virology/Bioterror preset on an eligible ready High-priority Virologist.
/// Kept separate from <see cref="VirologyModeRuleSystem"/> because GameRuleSystem already
/// subscribes to <see cref="RoundStartAttemptEvent"/>.
/// </summary>
public sealed class VirologyModeEligibilitySystem : EntitySystem
{
    public static readonly ProtoId<JobPrototype> RequiredJob = "Virologist";

    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly IChatManager _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartAttemptEvent>(OnRoundStartAttempt);
    }

    private void OnRoundStartAttempt(RoundStartAttemptEvent args)
    {
        // Forced starts must still be blocked when no Virologist is queued.
        if (args.Cancelled)
            return;

        if (!_ticker.IsGameRuleAdded<VirologyModeRuleComponent>())
            return;

        if (!HasVirologyStationWithJob() || !HasEligibleReadyVirologist(args.Players))
        {
            _chat.DispatchServerAnnouncement(Loc.GetString("sol-virology-preset-no-ready-virologist"));
            args.Cancel();
        }
    }

    private bool HasVirologyStationWithJob()
    {
        var query = EntityQueryEnumerator<VirologyStationComponent, StationJobsComponent, StationSpawningComponent>();
        while (query.MoveNext(out var station, out _, out _, out _))
        {
            var jobs = _stationJobs.GetRoundStartJobs(station);
            if (jobs.TryGetValue(RequiredJob, out var slots) && (slots is null || slots > 0))
                return true;
        }

        return false;
    }

    private bool HasEligibleReadyVirologist(ICommonSession[] players)
    {
        foreach (var session in players)
        {
            var prefs = _preferences.GetPreferencesOrNull(session.UserId);
            if (prefs == null)
                continue;

            if (!prefs.JobPriorities.TryGetValue(RequiredJob, out var priority) || priority != JobPriority.High)
                continue;

            if (prefs.GetAllEnabledProfilesForJob(RequiredJob).Count == 0)
                continue;

            if (_banManager.IsRoleBanned(session, new List<ProtoId<JobPrototype>> { RequiredJob }))
                continue;

            var candidates = new List<ProtoId<JobPrototype>> { RequiredJob };
            var ev = new StationJobsGetCandidatesEvent(session.UserId, candidates);
            RaiseLocalEvent(ref ev);

            if (candidates.Contains(RequiredJob))
                return true;
        }

        return false;
    }
}
