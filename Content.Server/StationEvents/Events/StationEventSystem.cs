using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared.Station.Components; // Starlight
using Robust.Shared.Audio; // Starlight

namespace Content.Server.StationEvents.Events;

/// <summary>
///     An abstract entity system inherited by all station events for their behavior.
/// </summary>
public abstract class StationEventSystem<T> : GameRuleSystem<T> where T : IComponent
{
    [Dependency] protected readonly IAdminLogManager AdminLogManager = default!;
    [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
    [Dependency] protected readonly ChatSystem ChatSystem = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] protected readonly StationSystem StationSystem = default!;

    protected ISawmill Sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        Sawmill = Logger.GetSawmill("stationevents");
    }

    /// <inheritdoc/>
    protected override void Added(EntityUid uid, T component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        AdminLogManager.Add(LogType.EventAnnounced, $"Event added / announced: {ToPrettyString(uid)}");

        //Starlight begin
        if (TryGetRandomStation(out var chosenStation))
            stationEvent.TargetStation = chosenStation;
        //Starlight end stationEvent.TargetStation = station;

        Announce(stationEvent, stationEvent.StartAnnouncement, false, stationEvent.StartAnnouncementColor, stationEvent.StartAudio);
        
        // we don't want to send to players who aren't in game (i.e. in the lobby)
        
        //Starlight end
    }

    /// <inheritdoc/>
    protected override void Started(EntityUid uid, T component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        AdminLogManager.Add(LogType.EventStarted, LogImpact.High, $"Event started: {ToPrettyString(uid)}");

        if (stationEvent.Duration != null)
        {
            var duration = stationEvent.MaxDuration == null
                ? stationEvent.Duration
                : TimeSpan.FromSeconds(RobustRandom.NextDouble(stationEvent.Duration.Value.TotalSeconds,
                    stationEvent.MaxDuration.Value.TotalSeconds));
            stationEvent.EndTime = Timing.CurTime + duration;
        }
    }

    /// <inheritdoc/>
    protected override void Ended(EntityUid uid, T component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        AdminLogManager.Add(LogType.EventStopped, $"Event ended: {ToPrettyString(uid)}");

        //Starlight begin
        // we don't want to send to players who aren't in game (i.e. in the lobby)
        Announce(stationEvent, stationEvent.EndAnnouncement, false, stationEvent.EndAnnouncementColor, stationEvent.EndAudio);
        //Starlight end
    }

    /// <summary>
    ///     Called every tick when this event is running.
    ///     Events are responsible for their own lifetime, so this handles starting and ending after time.
    /// </summary>
    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StationEventComponent, GameRuleComponent>();
        // Starlight start - Some StationEvents can add more StationEvents, which causes the upstream
        // enumeration here to break. Instead, we make a snapshot and iterate through that.
        var snapshot = new List<(EntityUid uid, StationEventComponent stationEvent, GameRuleComponent ruleData)>();
        // Starlight end
        while (query.MoveNext(out var uid, out var stationEvent, out var ruleData))
        // Starlight start
            snapshot.Add((uid, stationEvent, ruleData));

        foreach (var (uid, stationEvent, ruleData) in snapshot)
        // Starlight end
        {
            if (!GameTicker.IsGameRuleAdded(uid, ruleData))
                continue;

            if (!GameTicker.IsGameRuleActive(uid, ruleData) && !HasComp<DelayedStartRuleComponent>(uid))
            {
                GameTicker.StartGameRule(uid, ruleData);
            }
            else if (stationEvent.EndTime != null && Timing.CurTime >= stationEvent.EndTime && GameTicker.IsGameRuleActive(uid, ruleData))
            {
                GameTicker.EndGameRule(uid, ruleData);
            }
        }
    }
    
    //Starlight begin
    public void Announce(StationEventComponent stationEvent, LocId? announcementLocId, bool dispatchSound, Color? colorOverride = null, SoundSpecifier? soundOverride = null)
    {
        if (announcementLocId is null) return;
        if (stationEvent.GlobalAnnouncement)
        {
            var allPlayersInGame = Filter.Empty().AddWhere(GameTicker.UserHasJoinedGame);

            ChatSystem.DispatchFilteredAnnouncement(allPlayersInGame,
                Loc.GetString(announcementLocId), playSound: dispatchSound,
                colorOverride: colorOverride);
                
            if(soundOverride is not null) Audio.PlayGlobal(soundOverride, allPlayersInGame, true);
        }
        else
        {
            var allPlayersOnStation = Filter.Empty().AddWhere(session =>
            {
                if (session.AttachedEntity is null) return false;
                if (!TryComp<StationMemberComponent>(Transform(session.AttachedEntity.Value).GridUid,
                        out var stationGrid)) return false;
                return stationGrid.Station == stationEvent.TargetStation;
            });

            ChatSystem.DispatchFilteredAnnouncement(allPlayersOnStation,
                Loc.GetString(announcementLocId), playSound: dispatchSound,
                colorOverride: colorOverride);

            if(soundOverride is not null) Audio.PlayGlobal(soundOverride, allPlayersOnStation, true);
        }
    }
    //Starlight end
}
