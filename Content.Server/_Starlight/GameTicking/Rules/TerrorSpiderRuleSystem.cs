using Content.Server._Starlight.Antags.Components;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Antags.TerrorSpider;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed partial class TerrorSpiderRuleSystem : GameRuleSystem<TerrorSpiderRuleComponent>
{
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private EntityQuery<TerrorSpiderRuleComponent> _rules = default!;

    /// <summary>
    /// How much of the crew needs to be dead for the spiders to win.
    /// </summary>
    private const int TargetDeadCrewPercentage = 70;

    private int ActiveRulesCount = 0;

    protected override bool CanStartRule(EntityUid uid, TerrorSpiderRuleComponent component, GameRuleComponent gameRule, RoundStartAttemptEvent args, out string reason)
    {
        CheckLoseStatus(out var percentage);
        reason = "Can't run terror spiders rule when more than 40% crew is already died!";
        return percentage < 40; // If there's more than 40% died players - don't run this rule.
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationCrewComponent, MobStateChangedEvent>(OnCrewMobStateChanged);
        SubscribeLocalEvent<TerrorPrincessComponent, MobStateChangedEvent>(OnPrincessStateChanged);
        SubscribeLocalEvent<TerrorPrincessComponent, GetBriefingEvent>(OnGetBriefing);
    }

    protected override void Started(EntityUid uid, TerrorSpiderRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        ActiveRulesCount++;
    }

    protected override void Ended(EntityUid uid, TerrorSpiderRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);
        if (ActiveRulesCount > 0)
            ActiveRulesCount--;
    }

    private void OnGetBriefing(Entity<TerrorPrincessComponent> ent, ref GetBriefingEvent args)
        => args.Append(Loc.GetString(ent.Comp.Briefing));

    private void OnCrewMobStateChanged(EntityUid uid, StationCrewComponent component, MobStateChangedEvent args)
    {
        if (ActiveRulesCount > 0 && args.NewMobState is MobState.Dead or MobState.Invalid)
            ProcessLose();
    }

    private void OnPrincessStateChanged(EntityUid uid, TerrorPrincessComponent component, MobStateChangedEvent args)
    {
        var query = EntityQueryEnumerator<TerrorPrincessComponent, MobStateComponent>();
        var count = 0;
        while (query.MoveNext(out var princess, out _, out var state))
        {
            if (state.CurrentState is MobState.Alive)
                count++;
        }

        if (count == 0 && ActiveRulesCount > 0)
            ActiveRulesCount--;
    }

    protected override void AppendRoundEndText(EntityUid uid, TerrorSpiderRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        args.AddLine(Loc.GetString($"terrorspiders-win"));

        args.AddLine(Loc.GetString("terrorspiders-list-start"));

        var query = EntityQueryEnumerator<MetaDataComponent, TerrorSpiderComponent>();
        while (query.MoveNext(out var spider, out var metaData, out _))
        {
            if (!_player.TryGetSessionByEntity(spider, out var session))
                continue;
            args.AddLine(Loc.GetString("terrorspiders-list-name-user", ("name", metaData.EntityName), ("user", session.Name)));
        }
        args.AddLine("");
    }

    private void ProcessLose()
    {
        if (CheckLoseStatus(out _))
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

    private bool CheckLoseStatus(out float percentage)
    {
        percentage = 0;
        var crewList = new List<EntityUid>();

        var crew = EntityQueryEnumerator<StationCrewComponent>();
        while (crew.MoveNext(out var uid, out _))
            crewList.Add(uid);

        if (crewList.Count == 0)
            return false;

        var crewDeadAmount = CheckGroupStatus(crewList);
        percentage = crewDeadAmount * 100 / crewList.Count;
        return percentage >= TargetDeadCrewPercentage;
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
            if (TryComp<MobStateComponent>(ent, out var mobState) && mobState.CurrentState is MobState.Dead or MobState.Invalid)
                gone++;
            else if (checkOffStation && _stationSystem.GetOwningStation(ent) == null && !_emergencyShuttle.EmergencyShuttleArrived)
                gone++;
        }
        return gone;
    }
}
