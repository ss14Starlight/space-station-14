using Content.Server._Starlight.Antags.Components;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
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
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    /// Determines how much seconds CachedState for <see cref="AtLeastOneRuleExists"/> is valid.
    /// </summary>
    private readonly TimeSpan _rulesCheckCacheTime = TimeSpan.FromSeconds(30);

    private TimeSpan _lastRuleCheck = TimeSpan.Zero;
    private bool _cachedState = false;

    protected override bool CanStartRule(EntityUid uid, TerrorSpiderRuleComponent component, GameRuleComponent gameRule, RoundStartAttemptEvent args, out string reason)
    {
        SpidersScore(component, out var percentage);
        reason = $"Can't run terror spiders rule when more than {100 - component.MinAliveCrewPercentage}% of crew has died!";
        return percentage < 100 - component.MinAliveCrewPercentage;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationCrewComponent, MobStateChangedEvent>(OnCrewMobStateChanged);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<TerrorPrincessComponent, MobStateChangedEvent>(OnPrincessStateChanged);
        SubscribeLocalEvent<TerrorPrincessComponent, GetBriefingEvent>(OnGetBriefing);
    }

    protected override void Started(EntityUid uid, TerrorSpiderRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        AtLeastOneRuleExists(false); // Update Cache first time.
    }

    private void OnGetBriefing(Entity<TerrorPrincessComponent> ent, ref GetBriefingEvent args)
        => args.Append(Loc.GetString(ent.Comp.Briefing));

    private void OnCrewMobStateChanged(EntityUid uid, StationCrewComponent component, MobStateChangedEvent args)
    {
        if (AtLeastOneRuleExists() && args.NewMobState is MobState.Dead or MobState.Invalid) // Enable cache, because there's a lot of crew members.
            ProcessLose();
    }

    private void OnPrincessStateChanged(EntityUid uid, TerrorPrincessComponent component, MobStateChangedEvent args)
    {
        if (AtLeastOneRuleExists(false) && args.NewMobState is MobState.Dead or MobState.Invalid) // Disable cache, because there's only small amount of princesses.
            ProcessLose();
    }

    protected override void AppendRoundEndText(EntityUid uid, TerrorSpiderRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        var message = "";

        switch (component.Status)
        {
            case TerrorSpidersWinStatus.MinorLose:
                message = Loc.GetString("terrorspiders-minorlose");
                break;
            case TerrorSpidersWinStatus.Lose:
                message = Loc.GetString("terrorspiders-lose");
                break;
            case TerrorSpidersWinStatus.MinorWin:
                message = Loc.GetString("terrorspiders-minorwin");
                break;
            case TerrorSpidersWinStatus.Win:
                message = Loc.GetString("terrorspiders-win");
                break;
        };

        args.AddLine(message);

        var query = EntityQueryEnumerator<MetaDataComponent, TerrorSpiderComponent>();
        var startAdded = false;
        while (query.MoveNext(out var spider, out var metaData, out _))
        {
            if (!_player.TryGetSessionByEntity(spider, out var session))
                continue;

            if (!startAdded)
            {
                args.AddLine(Loc.GetString("terrorspiders-list-start"));
                startAdded = true;
            }

            args.AddLine(Loc.GetString("terrorspiders-list-name-user", ("name", metaData.EntityName), ("user", session.Name)));
        }
        args.AddLine("");
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New is not GameRunLevel.PostRound)
            return;

        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var rule, out _))
        {
            OnRoundEnd((uid, rule));
        }
    }

    private void OnRoundEnd(Entity<TerrorSpiderRuleComponent> ent)
    {
        if (ent.Comp.LoseProcessed)
            return;

        var score = SpidersScore(ent.Comp, out _, false);

        ent.Comp.Status = score > ent.Comp.TargetMinorScore ? TerrorSpidersWinStatus.MinorWin : TerrorSpidersWinStatus.MinorLose;
    }

    private void ProcessLose()
    {
        var lose = AreAllPrincessesDead();
        var count = 0;
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out _, out var ruleComp, out _))
        {
            if (ruleComp.LoseProcessed)
                continue;

            var score = SpidersScore(ruleComp, out _);
            var win = score > ruleComp.TargetWinScore && !lose;

            if (!win && !lose)
                continue;

            count++;
            ruleComp.Status = win ? TerrorSpidersWinStatus.Win : TerrorSpidersWinStatus.Lose;
            ruleComp.LoseProcessed = true;
            ruleComp.AnnouncementTime = _timing.CurTime + ruleComp.AnnouncementDelay;
            ruleComp.EndRoundTime = _timing.CurTime + ruleComp.RoundEndDelay;
        }

        if (count == 0)
            return;

        // Check if the emergency shuttle is already called (not just arrived)
        if (_roundEnd.IsRoundEndRequested())
        {
            // If the shuttle is already called, we need to recall it
            // Cancel the current shuttle call - force it with false for checkCooldown
            _roundEnd.CancelRoundEndCountdown(null, false);
        }
    }

    protected override void ActiveTick(EntityUid uid, TerrorSpiderRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        if (!component.LoseProcessed) return;

        if (component.AlreadyAnnounced && component.RoundAlreadyEnded) return;

        if (_ticker.RunLevel != GameRunLevel.InRound) return;

        if (!component.AlreadyAnnounced && component.AnnouncementTime < _timing.CurTime)
        {
            try
            {
                // Send Central Command announcement
                if (component.Status == TerrorSpidersWinStatus.Lose)
                {
                    _roundEnd.DoRoundEndBehavior(component.RoundEndBehavior, component.EvacShuttleTime, component.RoundEndTextSender, component.RoundEndTextShuttleCallLose, component.RoundEndTextAnnouncementLose);
                    component.RoundEndBehavior = RoundEndBehavior.Nothing;
                }
                else if (component.Status == TerrorSpidersWinStatus.Win)
                {
                    _roundEnd.DoRoundEndBehavior(component.RoundEndBehavior, component.EvacShuttleTime, component.RoundEndTextSender, component.RoundEndTextShuttleCallWin, component.RoundEndTextAnnouncementWin);
                    component.RoundEndBehavior = RoundEndBehavior.Nothing;
                }

                component.AlreadyAnnounced = true;
            }
            catch (Exception ex)
            {
                Log.Error($"Error during first announcement: {ex}");
                component.AlreadyAnnounced = true; // Throw announce because it's broken.
            }
        }

        if (!component.RoundAlreadyEnded && component.EndRoundTime < _timing.CurTime)
        {
            // End the gamemode
            component.RoundAlreadyEnded = true;
            _ticker.EndGameRule(uid);
        }
    }

    private float SpidersScore(TerrorSpiderRuleComponent component, out float percentage, bool shouldUseMinGate = true)
    {
        percentage = 0;
        var crewList = new List<EntityUid>();

        var crew = EntityQueryEnumerator<StationCrewComponent>();
        while (crew.MoveNext(out var uid, out _))
            crewList.Add(uid);

        if (crewList.Count == 0)
            return 0;

        var spidersList = new List<EntityUid>();
        var spiders = EntityQueryEnumerator<TerrorSpiderComponent>();

        while (spiders.MoveNext(out var uid, out _))
            spidersList.Add(uid);

        if (shouldUseMinGate && spidersList.Count < component.MinSpidersCountForWin)
            return 0;

        if (spidersList.Count == 0)
            return 0;

        var crewDeadAmount = CountGoneEntities(crewList);
        var spidersDeadAmount = CountGoneEntities(spidersList);

        var deadCrewPercent =
            (float)crewDeadAmount / crewList.Count * 100f;

        percentage = deadCrewPercent;

        var aliveSpiderPercent =
            (float)(spidersList.Count - spidersDeadAmount) / spidersList.Count * 100f;
        return (deadCrewPercent * component.DeadCrewWeight) + (aliveSpiderPercent * component.SpiderAmountWeight);
    }

    private int CountGoneEntities(IEnumerable<EntityUid> entities, bool checkOffStation = true)
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

    private bool AreAllPrincessesDead()
    {
        var query = EntityQueryEnumerator<TerrorPrincessComponent, MobStateComponent>();

        var count = 0;

        while (query.MoveNext(out _, out _, out var state))
        {
            count++;
            if (state.CurrentState == MobState.Alive)
                return false;
        }

        if (count == 0)
            return false;

        return true;
    }

    private bool AtLeastOneRuleExists(bool canUseCache = true)
    {
        if (canUseCache && _timing.CurTime < _lastRuleCheck + _rulesCheckCacheTime)
            return _cachedState;

        _lastRuleCheck = _timing.CurTime;

        var count = 0;
        var query = EntityQueryEnumerator<TerrorSpiderRuleComponent>();
        while (query.MoveNext(out var ruleEnt, out var ruleComp))
        {
            if (ruleComp.LoseProcessed)
                continue;

            count++;
        }

        _cachedState = count > 0;

        return _cachedState;
    }
}
