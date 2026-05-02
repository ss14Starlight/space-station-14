using Content.Server._Starlight.Antags.Components;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Antags.TerrorSpider;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed class TerrorSpiderRuleSystem : GameRuleSystem<TerrorSpiderRuleComponent>
{
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;

    /// <summary>
    /// How much of the crew needs to be dead for the spiders to win.
    /// </summary>
    private const int TargetDeadCrewPercentage = 50;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationCrewComponent, MobStateChangedEvent>(OnCrewMobStateChanged);
        SubscribeLocalEvent<TerrorPrincessComponent, GetBriefingEvent>(OnGetBriefing);
    }

    private void OnGetBriefing(Entity<TerrorPrincessComponent> ent, ref GetBriefingEvent args)
        => args.Append(Loc.GetString(ent.Comp.Briefing));

    private void OnCrewMobStateChanged(EntityUid uid, StationCrewComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Dead or MobState.Invalid)
            ProcessLose();
    }

    private void ProcessLose()
    {
        if (CheckLoseStatus())
        {
            _roundEnd.CancelRoundEndCountdown(null, false);
            var query = EntityQueryEnumerator<TerrorSpiderRuleComponent>();
            while (query.MoveNext(out var ruleEnt, out var ruleComp))
            {
                if (ruleComp.LoseProcessed)
                    return;

                ruleComp.LoseProcessed = true;
                GameTicker.EndGameRule(ruleEnt); // End all terror spider rules
            }

            // Check if the emergency shuttle is already called (not just arrived)
            if (_roundEnd.IsRoundEndRequested())
            {
                // If the shuttle is already called, we need to recall it
                // Cancel the current shuttle call - force it with false for checkCooldown
                _roundEnd.CancelRoundEndCountdown(null, false);
            }

            // Use a safer approach for scheduling the announcements
            // Schedule the first announcement after 7 seconds
            Timer.Spawn(TimeSpan.FromSeconds(7), () =>
            {
                try
                {
                    // Send Central Command announcement
                    _chatSystem.DispatchGlobalAnnouncement(
                        Loc.GetString("central-command-terror-spiders-announcement"),
                        Loc.GetString("central-command-sender"),
                        true,
                        new SoundPathSpecifier("/Audio/_Starlight/Announcements/announce_broken.ogg"),
                        Color.Red
                    );
                }
                catch (Exception ex)
                {
                    Log.Error($"Error during first announcement: {ex}");
                }
            });

            Timer.Spawn(TimeSpan.FromSeconds(32), () =>
            {
                try
                {
                    // End the round
                    _roundEnd.EndRound();
                }
                catch (Exception ex)
                {
                    Log.Error($"Error during second announcement: {ex}");
                    // Still try to end the round even if the announcement fails
                    _roundEnd.EndRound();
                }
            });
        }
    }

    private bool CheckLoseStatus()
    {
        var crewList = new List<EntityUid>();

        var crew = EntityQueryEnumerator<StationCrewComponent>();
        while (crew.MoveNext(out var uid, out _))
            crewList.Add(uid);

        if (crewList.Count == 0)
            return false;

        var crewDeadAmount = CheckGroupStatus(crewList);
        return crewDeadAmount * 100 / crewList.Count >= TargetDeadCrewPercentage;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="crewList"></param>
    /// <param name="checkOffStation"></param>
    /// <returns></returns>
    private int CheckGroupStatus(IEnumerable<EntityUid> entities, bool checkOffStation = true)
    {
        var gone = 0;
        foreach (var ent in entities)
        {
            if (EntityManager.TryGetComponent(ent, out MobStateComponent? mobState) && mobState.CurrentState is MobState.Dead or MobState.Invalid)
                gone++;
            else if (checkOffStation && _stationSystem.GetOwningStation(ent) == null && !_emergencyShuttle.EmergencyShuttleArrived)
                gone++;
        }
        return gone;
    }
}
