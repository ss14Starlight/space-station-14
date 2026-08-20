using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Hands.Systems;
using Content.Server.Mind;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Popups;
using Content.Server.StationEvents;
using Content.Server.StationEvents.Components;
using Content.Server.StationRecords.Systems;
// Cosmatic Drift Record System-start
using Content.Server._CD.Records;
// Cosmatic Drift Record System-end
using Content.Shared.Administration;
using Content.Shared.Administration.Events;
using Content.Shared.CCVar;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Hands.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.PDA;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Roles.Jobs;
using Content.Shared.StationRecords;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Administration.Systems;

public sealed partial class AdminSystem : EntitySystem
{
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MindSystem _minds = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private PhysicsSystem _physics = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private EventManagerSystem _eventManager = default!;
    [Dependency] private BasicStationEventSchedulerSystem _eventScheduler = default!;
    [Dependency] private StationRecordsSystem _stationRecords = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private CharacterRecordsSystem _characterRecords = default!; // Cosmatic Drift Record System: erase-ban helper
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<NetUserId, PlayerInfo> _playerList = new();

    /// <summary>
    ///     Set of players that have participated in this round.
    /// </summary>
    public IReadOnlySet<NetUserId> RoundActivePlayers => _roundActivePlayers;

    private readonly HashSet<NetUserId> _roundActivePlayers = new();
    public readonly PanicBunkerStatus PanicBunker = new();

    public override void Initialize()
    {
        base.Initialize();

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        _adminManager.OnPermsChanged += OnAdminPermsChanged;
        _playTime.SessionPlayTimeUpdated += OnSessionPlayTimeUpdated;

        // Panic Bunker Settings
        Subs.CVar(_config, CCVars.PanicBunkerEnabled, OnPanicBunkerChanged, true);
        Subs.CVar(_config, CCVars.PanicBunkerDisableWithAdmins, OnPanicBunkerDisableWithAdminsChanged, true);
        Subs.CVar(_config, CCVars.PanicBunkerEnableWithoutAdmins, OnPanicBunkerEnableWithoutAdminsChanged, true);
        Subs.CVar(_config, CCVars.PanicBunkerCountDeadminnedAdmins, OnPanicBunkerCountDeadminnedAdminsChanged, true);
        Subs.CVar(_config, CCVars.PanicBunkerShowReason, OnPanicBunkerShowReasonChanged, true);
        Subs.CVar(_config, CCVars.PanicBunkerMinAccountAge, OnPanicBunkerMinAccountAgeChanged, true);
        Subs.CVar(_config, CCVars.PanicBunkerMinOverallMinutes, OnPanicBunkerMinOverallMinutesChanged, true);

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleEvent);
        SubscribeLocalEvent<RoleRemovedEvent>(OnRoleEvent);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<ActorComponent, EntityRenamedEvent>(OnPlayerRenamed);
        SubscribeLocalEvent<ActorComponent, IdentityChangedEvent>(OnIdentityChanged);
        SubscribeNetworkEvent<RequestStationEventsEvent>(OnRequestStationEvents);
        SubscribeNetworkEvent<StationEventQueueCommandEvent>(OnStationEventQueueCommand);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _roundActivePlayers.Clear();

        foreach (var (id, data) in _playerList)
        {
            if (!data.ActiveThisRound)
                continue;

            if (!_playerManager.TryGetPlayerData(id, out var playerData))
                return;

            _playerManager.TryGetSessionById(id, out var session);
            _playerList[id] = GetPlayerInfo(playerData, session);
        }

        var updateEv = new FullPlayerListEvent() { PlayersInfo = _playerList.Values.ToList() };

        foreach (var admin in _adminManager.ActiveAdmins)
        {
            RaiseNetworkEvent(updateEv, admin.Channel);
        }
    }

    private void OnPlayerRenamed(Entity<ActorComponent> ent, ref EntityRenamedEvent args)
    {
        UpdatePlayerList(ent.Comp.PlayerSession);
    }

    public void UpdatePlayerList(ICommonSession player)
    {
        _playerList[player.UserId] = GetPlayerInfo(player.Data, player);

        var playerInfoChangedEvent = new PlayerInfoChangedEvent
        {
            PlayerInfo = _playerList[player.UserId]
        };

        foreach (var admin in _adminManager.ActiveAdmins)
        {
            RaiseNetworkEvent(playerInfoChangedEvent, admin.Channel);
        }
    }

    public PlayerInfo? GetCachedPlayerInfo(NetUserId? netUserId)
    {
        if (netUserId == null)
            return null;

        _playerList.TryGetValue(netUserId.Value, out var value);
        return value ?? null;
    }

    private void OnIdentityChanged(Entity<ActorComponent> ent, ref IdentityChangedEvent ev)
    {
        UpdatePlayerList(ent.Comp.PlayerSession);
    }

    private void OnRoleEvent(RoleEvent ev)
    {
        if (!ev.RoleTypeUpdate || !_playerManager.TryGetSessionById(ev.Mind.UserId, out var session))
            return;

        UpdatePlayerList(session);
    }

    private void OnAdminPermsChanged(AdminPermsChangedEventArgs obj)
    {
        UpdatePanicBunker();

        if (!obj.IsAdmin)
        {
            RaiseNetworkEvent(new FullPlayerListEvent(), obj.Player.Channel);
            return;
        }

        SendFullPlayerList(obj.Player);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        // If disconnected then the player won't have a connected entity to get character name from.
        // The disconnected state gets sent by OnPlayerStatusChanged.
        if (ev.Player.Status == SessionStatus.Disconnected)
            return;

        UpdatePlayerList(ev.Player);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (ev.Player.Status == SessionStatus.Disconnected)
            return;

        _roundActivePlayers.Add(ev.Player.UserId);
        UpdatePlayerList(ev.Player);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        _adminManager.OnPermsChanged -= OnAdminPermsChanged;
        _playTime.SessionPlayTimeUpdated -= OnSessionPlayTimeUpdated;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        UpdatePlayerList(e.Session);
        UpdatePanicBunker();
    }

    private void SendFullPlayerList(ICommonSession playerSession)
    {
        var ev = new FullPlayerListEvent();

        ev.PlayersInfo = _playerList.Values.ToList();

        RaiseNetworkEvent(ev, playerSession.Channel);
    }

    private PlayerInfo GetPlayerInfo(SessionData data, ICommonSession? session)
    {
        var name = data.UserName;
        var entityName = string.Empty;
        var identityName = string.Empty;
        var sortWeight = 0;

        // Visible (identity) name can be different from real name
        if (session?.AttachedEntity != null)
        {
            entityName = Comp<MetaDataComponent>(session.AttachedEntity.Value).EntityName;
            identityName = Identity.Name(session.AttachedEntity.Value, EntityManager);
        }

        var antag = false;

        // Starting role, antagonist status and role type
        RoleTypePrototype? roleType = null;
        var startingRole = string.Empty;
        LocId? subtype = null;
        if (_minds.TryGetMind(session, out var mindId, out var mindComp) && mindComp is not null)
        {
            sortWeight = _role.GetRoleCompByTime(mindComp)?.Comp.SortWeight ?? 0;

            if (_proto.TryIndex(mindComp.RoleType, out var role))
            {
                roleType = role;
                subtype = mindComp.Subtype;
            }
            else
                Log.Error($"{ToPrettyString(mindId)} has invalid Role Type '{mindComp.RoleType}'. Displaying '{Loc.GetString(RoleTypePrototype.FallbackName)}' instead");

            antag = _role.MindIsAntagonist(mindId);
            startingRole = _jobs.MindTryGetJobName(mindId);
        }

        // Connection status and playtime
        var connected = session != null && session.Status is SessionStatus.Connected or SessionStatus.InGame;

        // Start with the last available playtime data
        var cachedInfo = GetCachedPlayerInfo(data.UserId);
        var overallPlaytime = cachedInfo?.OverallPlaytime;
        // Overwrite with current playtime data, unless it's null (such as if the player just disconnected)
        if (session != null &&
            _playTime.TryGetOriginalTrackerTimes(session, out var playTimes) && // Starlight
            playTimes.TryGetValue(PlayTimeTrackingShared.TrackerOverall, out var playTime))
        {
            overallPlaytime = playTime;
        }

        return new PlayerInfo(
            name,
            entityName,
            identityName,
            startingRole,
            antag,
            roleType?.ID,
            subtype,
            sortWeight,
            GetNetEntity(session?.AttachedEntity),
            data.UserId,
            connected,
            _roundActivePlayers.Contains(data.UserId),
            overallPlaytime);
    }

    private void OnPanicBunkerChanged(bool enabled)
    {
        PanicBunker.Enabled = enabled;
        _chat.SendAdminAlert(Loc.GetString(enabled
            ? "admin-ui-panic-bunker-enabled-admin-alert"
            : "admin-ui-panic-bunker-disabled-admin-alert"
        ));

        SendPanicBunkerStatusAll();
    }

    private void OnPanicBunkerDisableWithAdminsChanged(bool enabled)
    {
        PanicBunker.DisableWithAdmins = enabled;
        UpdatePanicBunker();
    }

    private void OnPanicBunkerEnableWithoutAdminsChanged(bool enabled)
    {
        PanicBunker.EnableWithoutAdmins = enabled;
        UpdatePanicBunker();
    }

    private void OnPanicBunkerCountDeadminnedAdminsChanged(bool enabled)
    {
        PanicBunker.CountDeadminnedAdmins = enabled;
        UpdatePanicBunker();
    }

    private void OnPanicBunkerShowReasonChanged(bool enabled)
    {
        PanicBunker.ShowReason = enabled;
        SendPanicBunkerStatusAll();
    }

    private void OnPanicBunkerMinAccountAgeChanged(int minutes)
    {
        PanicBunker.MinAccountAgeMinutes = minutes;
        SendPanicBunkerStatusAll();
    }

    private void OnPanicBunkerMinOverallMinutesChanged(int minutes)
    {
        PanicBunker.MinOverallMinutes = minutes;
        SendPanicBunkerStatusAll();
    }

    private void UpdatePanicBunker()
    {
        var hasAdmins = false;
        foreach (var admin in _adminManager.AllAdmins)
        {
            if (_adminManager.HasAdminFlag(admin, AdminFlags.Admin, includeDeAdmin: PanicBunker.CountDeadminnedAdmins))
            {
                hasAdmins = true;
                break;
            }
        }

        // TODO Fix order dependent Cvars
        // Please for the sake of my sanity don't make cvars & order dependent.
        // Just make a bool field on the system instead of having some cvars automatically modify other cvars.
        //
        // I.e., this:
        //   /sudo cvar game.panic_bunker.enabled true
        //   /sudo cvar game.panic_bunker.disable_with_admins true
        // and this:
        //   /sudo cvar game.panic_bunker.disable_with_admins true
        //   /sudo cvar game.panic_bunker.enabled true
        //
        // should have the same effect, but currently setting the disable_with_admins can modify enabled.

        if (hasAdmins && PanicBunker.DisableWithAdmins)
        {
            _config.SetCVar(CCVars.PanicBunkerEnabled, false);
        }
        else if (!hasAdmins && PanicBunker.EnableWithoutAdmins)
        {
            _config.SetCVar(CCVars.PanicBunkerEnabled, true);
        }

        SendPanicBunkerStatusAll();
    }

    private void SendPanicBunkerStatusAll()
    {
        var ev = new PanicBunkerChangedEvent(PanicBunker);
        foreach (var admin in _adminManager.AllAdmins)
        {
            RaiseNetworkEvent(ev, admin);
        }
    }

    private void OnRequestStationEvents(RequestStationEventsEvent ev, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin))
            return;

        SendStationEvents(args.SenderSession);
    }

    private void OnStationEventQueueCommand(StationEventQueueCommandEvent ev, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
            return;

        var changed = ev.Command switch
        {
            StationEventQueueCommand.Schedule =>
                _eventScheduler.ScheduleEvent(ev.EventId, Math.Max(ev.Seconds, 0f)),
            StationEventQueueCommand.Adjust =>
                _eventScheduler.AdjustScheduledEvent(ev.QueueId, ev.Seconds),
            StationEventQueueCommand.Remove =>
                _eventScheduler.RemoveScheduledEvent(ev.QueueId),
            StationEventQueueCommand.RunNow =>
                _eventScheduler.RunScheduledEventNow(ev.QueueId),
            StationEventQueueCommand.EndActive => EndActiveStationEvent(ev.ActiveEvent),
            _ => false
        };

        if (changed)
            SendStationEvents(args.SenderSession);
    }

    private bool EndActiveStationEvent(NetEntity netEntity)
    {
        var uid = GetEntity(netEntity);
        if (!Exists(uid) ||
            !HasComp<StationEventComponent>(uid) ||
            !_gameTicker.IsGameRuleActive(uid))
        {
            return false;
        }

        return _gameTicker.EndGameRule(uid);
    }

    private void SendStationEvents(ICommonSession session)
    {
        var available = _eventManager.AvailableEvents();
        var runtimeStates = new Dictionary<string, EventRuntimeState>();
        var activeEvents = new List<ActiveStationEventData>();

        foreach (var uid in _gameTicker.GetAddedGameRules())
        {
            if (MetaData(uid).EntityPrototype?.ID is not { } id ||
                !TryComp(uid, out StationEventComponent? stationEvent) ||
                HasComp<EndedGameRuleComponent>(uid))
            {
                continue;
            }

            if (!runtimeStates.TryGetValue(id, out var runtime))
            {
                runtime = new EventRuntimeState();
                runtimeStates[id] = runtime;
            }

            if (_gameTicker.IsGameRuleActive(uid))
            {
                runtime.ActiveCount++;

                var elapsed = 0f;
                if (TryComp(uid, out GameRuleComponent? gameRule))
                {
                    elapsed = Math.Max((float) (_timing.CurTime - gameRule.ActivatedAt).TotalSeconds, 0f);
                }

                var duration = -1f;
                var remaining = -1f;

                if (stationEvent.EndTime is { } endTime)
                {
                    remaining = Math.Max((float) (endTime - _timing.CurTime).TotalSeconds, 0f);
                    duration = elapsed + remaining;
                    runtime.MinRemainingSeconds = runtime.MinRemainingSeconds < 0f
                        ? remaining
                        : Math.Min(runtime.MinRemainingSeconds, remaining);
                    runtime.MaxRemainingSeconds = Math.Max(runtime.MaxRemainingSeconds, remaining);
                }

                activeEvents.Add(new ActiveStationEventData
                {
                    Entity = GetNetEntity(uid),
                    EventId = id,
                    ElapsedSeconds = elapsed,
                    DurationSeconds = duration,
                    RemainingSeconds = remaining
                });

                continue;
            }

            runtime.PendingCount++;

            var nextStart = 0f;
            DelayedStartRuleComponent? delayed = null;
            if (TryComp(uid, out delayed) && delayed != null)
                nextStart = Math.Max((float) (delayed.RuleStartTime - _timing.CurTime).TotalSeconds, 0f);

            runtime.NextStartSeconds = runtime.NextStartSeconds < 0f
                ? nextStart
                : Math.Min(runtime.NextStartSeconds, nextStart);
        }

        var snapshot = new StationEventsChangedEvent
        {
            EventsEnabled = _eventManager.EventsEnabled,
            PlayerCount = _playerManager.PlayerCount,
            RoundDurationMinutes = (float) _gameTicker.RoundDuration().TotalMinutes,
            HasScheduler = _eventScheduler.HasActiveScheduler(out _, out _),
            Queue = _eventScheduler.GetQueuedEvents()
                .Select(entry => new ScheduledStationEventData
                {
                    Id = entry.Id,
                    EventId = entry.EventId,
                    TriggerInSeconds = Math.Max((float) (entry.TriggerTime - _timing.CurTime).TotalSeconds, 0f),
                    TotalDelaySeconds = Math.Max((float) (entry.TriggerTime - entry.QueuedAt).TotalSeconds, 0f),
                    Automatic = entry.Automatic
                })
                .ToList(),
            ActiveEvents = activeEvents
                .OrderBy(active => active.RemainingSeconds < 0f ? float.MaxValue : active.RemainingSeconds)
                .ThenBy(active => active.EventId)
                .ToList(),
            Events = _eventManager.AllEvents()
                .OrderBy(pair => pair.Key.ID)
                .Select(pair => new StationEventData
                {
                    Id = pair.Key.ID,
                    Available = available.ContainsKey(pair.Key),
                    MinimumPlayers = pair.Value.MinimumPlayers,
                    EarliestStartMinutes = pair.Value.EarliestStart,
                    ReoccurrenceDelayMinutes = pair.Value.ReoccurrenceDelay,
                    Weight = pair.Value.Weight,
                    DurationSeconds = pair.Value.Duration is { } duration
                        ? (float) duration.TotalSeconds
                        : -1f,
                    MaxDurationSeconds = pair.Value.MaxDuration is { } maxDuration
                        ? (float) maxDuration.TotalSeconds
                        : pair.Value.Duration is { } fixedDuration
                            ? (float) fixedDuration.TotalSeconds
                            : -1f,
                    ActiveCount = runtimeStates.TryGetValue(pair.Key.ID, out var runtime) ? runtime.ActiveCount : 0,
                    PendingCount = runtimeStates.TryGetValue(pair.Key.ID, out runtime) ? runtime.PendingCount : 0,
                    NextStartSeconds = runtimeStates.TryGetValue(pair.Key.ID, out runtime) ? runtime.NextStartSeconds : -1f,
                    MinRemainingSeconds = runtimeStates.TryGetValue(pair.Key.ID, out runtime) ? runtime.MinRemainingSeconds : -1f,
                    MaxRemainingSeconds = runtimeStates.TryGetValue(pair.Key.ID, out runtime) ? runtime.MaxRemainingSeconds : -1f
                })
                .ToList()
        };

        RaiseNetworkEvent(snapshot, session.Channel);
    }

    private sealed class EventRuntimeState
    {
        public int ActiveCount;
        public int PendingCount;
        public float NextStartSeconds = -1f;
        public float MinRemainingSeconds = -1f;
        public float MaxRemainingSeconds = -1f;
    }

    /// <summary>
    ///     Erases a player from the round.
    ///     This removes them and any trace of them from the round, deleting their
    ///     chat messages and showing a popup to other players.
    ///     Their items are dropped on the ground.
    /// </summary>
    public void Erase(NetUserId uid)
    {
        _chat.DeleteMessagesBy(uid);

        var eraseEvent = new EraseEvent(uid);

        if (!_minds.TryGetMind(uid, out var mindId, out var mind) || mind.OwnedEntity == null || TerminatingOrDeleted(mind.OwnedEntity.Value))
        {
            RaiseLocalEvent(ref eraseEvent);
            return;
        }

        var entity = mind.OwnedEntity.Value;

        if (TryComp(entity, out TransformComponent? transform))
        {
            var coordinates = _transform.GetMoverCoordinates(entity, transform);
            var name = Identity.Entity(entity, EntityManager);
            _popup.PopupCoordinates(Loc.GetString("admin-erase-popup", ("user", name)), coordinates, PopupType.LargeCaution);
            var filter = Filter.Pvs(coordinates, 1, EntityManager, _playerManager);
            var audioParams = new AudioParams().WithVolume(3);
            _audio.PlayStatic("/Audio/Effects/pop_high.ogg", filter, coordinates, true, audioParams);
        }

        foreach (var item in _inventory.GetHandOrInventoryEntities(entity))
        {
            if (TryComp(item, out PdaComponent? pda) &&
                TryComp(pda.ContainedId, out StationRecordKeyStorageComponent? keyStorage) &&
                keyStorage.Key is { } key &&
                _stationRecords.TryGetRecord(key, out GeneralStationRecord? record))
            {
                if (TryComp(entity, out DnaComponent? dna) &&
                    dna.DNA != record.DNA)
                {
                    continue;
                }

                if (TryComp(entity, out FingerprintComponent? fingerPrint) &&
                    fingerPrint.Fingerprint != record.Fingerprint)
                {
                    continue;
                }

                _stationRecords.RemoveRecord(key);
                Del(item);
            }
        }

        if (_inventory.TryGetContainerSlotEnumerator(entity, out var enumerator))
        {
            while (enumerator.NextItem(out var item, out var slot))
            {
                if (_inventory.TryUnequip(entity, entity, slot.Name, true, true))
                    _physics.ApplyAngularImpulse(item, ThrowingSystem.ThrowAngularImpulse);
            }
        }

        if (TryComp(entity, out HandsComponent? hands))
        {
            foreach (var hand in _hands.EnumerateHands((entity, hands)))
            {
                _hands.TryDrop((entity, hands), hand, checkActionBlocker: false, doDropInteraction: false);
            }
        }

        _minds.WipeMind(mindId, mind);
        QueueDel(entity);

        if (_playerManager.TryGetSessionById(uid, out var session))
            _gameTicker.SpawnObserver(session);

        // Cosmatic Drift Record System-start: Scrub any persisted records when an admin performs an erase.
        _characterRecords.DeleteAllRecords(entity);
        // Cosmatic Drift Record System-end

        RaiseLocalEvent(ref eraseEvent);
    }

    private void OnSessionPlayTimeUpdated(ICommonSession session)
    {
        UpdatePlayerList(session);
    }
}

/// <summary>
/// Event fired after a player is erased by an admin
/// </summary>
/// <param name="PlayerNetUserId">NetUserId of the player that was the target of the Erase</param>
[ByRefEvent]
public record struct EraseEvent(NetUserId PlayerNetUserId);
