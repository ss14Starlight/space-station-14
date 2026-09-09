using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server.Antag;
using Content.Server._Functional.TutorialServer.UI;
using Content.Server.Chat.Managers;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Humanoid;
using Content.Server.Mind;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Roles.Jobs;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Popups;
using Content.Shared.Strip.Components;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DetailExaminable;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Server.Preferences.Managers;
using Content.Shared._Starlight.TutorialServer;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Objectives.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.SSDIndicator;
using Content.Shared.StatusEffectNew;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using LookupFlags = Robust.Shared.GameObjects.LookupFlags;

namespace Content.Server._Functional.TutorialServer;

/*
 * TODO: SOMEBODY PLEASE REWRITE THIS ENTIRE SYSTEM UNDER _Starlight PLEASE.
 * ITS A MESS AND FULL OF AI SLOPCODE THAT BARELY WORKS. I WOULD DO IT BUT
 * I GENUINELY CANNOT BE BOTHERED RIGHT NOW GIVEN THAT I HAVE LIKE SEVERAL
 * OTHER THINGS GOING ON THAT I NEED TO MANAGE AND WORK ON.
 * FYI entire system meaning everything related to it not just TutorialServerRuleSystem.
 * Also ideally make a shared tutorial system that this calls for stuff and make
 * the rest of the stuff shared+predicted etc
 */

/// <summary>
/// Orchestrates the Functional Tutorial Server: picker, private maps, sessions, respawn loop.
/// </summary>
public sealed partial class TutorialServerRuleSystem : GameRuleSystem<TutorialServerRuleComponent>
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IDependencyCollection _deps = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private HumanoidAppearanceSystem _humanoidProfile = default!;
    [Dependency] private IdentitySystem _identity = default!;
    [Dependency] private JobSystem _jobs = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedObjectivesSystem _objectives = default!;
    [Dependency] private TutorialGoalConditionSystem _goalObjectives = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedGodmodeSystem _godmode = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private RespawnRuleSystem _respawn = default!;
    // [Dependency] private SharedVisualBodySystem _visualBody = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;
    [Dependency] private IServerPreferencesManager _prefsManager = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private TutorialMapSystem _tutorialMaps = default!;
    [Dependency] private TutorialPracticeRoomSystem _tutorialRooms = default!;
    [Dependency] private TutorialTegBootstrapSystem _tegBootstrap = default!;
    [Dependency] private TutorialResearchBootstrapSystem _researchBootstrap = default!;
    [Dependency] private TutorialCargoBootstrapSystem _cargoBootstrap = default!;
    [Dependency] private TutorialCommandBootstrapSystem _commandBootstrap = default!;
    [Dependency] private TutorialChemBootstrapSystem _chemBootstrap = default!;
    [Dependency] private TutorialAntagBootstrapSystem _antagBootstrap = default!;
    [Dependency] private AntagSelectionSystem _antagSelection = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private TutorialMentorFollowSystem _mentorFollow = default!;
    [Dependency] private SharedDoorSystem _doors = default!;
    [Dependency] private TutorialHoloMentorSystem _holoMentor = default!;
    [Dependency] private TutorialTrainerSystem _trainer = default!;
    [Dependency] private TutorialLeadMentorSystem _leadMentor = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly EntProtoId TutorialGuideProto = "TutorialGuide";
    private static readonly EntProtoId TutorialMentorProto = "TutorialMentor";
    private static readonly EntProtoId TutorialPassengerMentorProto = "TutorialPassengerMentor";
    private static readonly EntProtoId TutorialCargoQmMentorProto = "TutorialCargoQmMentor";
    private static readonly EntProtoId TutorialChooseRoleActionProto = "ActionTutorialChooseRole";
    private static readonly EntProtoId StatusEffectSsdSleepingProto = "StatusEffectSSDSleeping";
    private static readonly EntProtoId TutorialHackAirlockProto = "TutorialHackAirlock";
    private static readonly EntProtoId TutorialCurriculumGoalProto = "TutorialCurriculumGoal";
    private static readonly ProtoId<TutorialRolePrototype> TutorialPassengerRole = "TutorialPassenger";
    private static readonly ProtoId<TutorialRolePrototype> TutorialCargoTechnicianRole = "TutorialCargoTechnician";
    private static readonly ProtoId<TutorialRolePrototype> TutorialMedicalDoctorRole = "TutorialMedicalDoctor";
    private static readonly EntProtoId TutorialMedicalBeltProto = "ClothingBeltMedicalFilled";
    private static readonly TimeSpan ProgressPopupCooldown = TimeSpan.FromSeconds(0.75);
    private const string PickerCategoryStartHere = "Start Here";
    private const string PickerCategoryStationJobs = "Station Jobs";
    private const string PickerCategoryAntagonist = "Antagonist";
    private const string PickerCategoryWizdenAntags = "Wizden antagonists";
    private const string PickerCategoryServerSpecific = "Server specific";

    /// <summary>
    /// IC speak range for mentor tips. Beyond this, hybrid travel roles fall back to tip chat.
    /// </summary>
    private const float MentorCoachRange = 10f;

    /// <summary>
    /// Sub-tile offsets so piled practice items stay visible (right-click still works; this
    /// avoids exact Z-fighting stacks when multiple spawns share a tile).
    /// </summary>
    private static readonly Vector2[] PracticePileScatter =
    [
        Vector2.Zero,
        new Vector2(0.18f, 0.06f),
        new Vector2(-0.16f, 0.1f),
        new Vector2(0.06f, -0.18f),
        new Vector2(-0.14f, -0.12f),
        new Vector2(0.2f, -0.08f),
        new Vector2(-0.2f, 0.04f),
        new Vector2(0.1f, 0.2f),
        new Vector2(-0.08f, 0.18f),
        new Vector2(0.16f, -0.16f),
    ];

    private const float PracticePileLookupRange = 0.35f;

    private readonly Dictionary<NetUserId, TutorialRolePickerEui> _openPickers = new();
    private readonly HashSet<EntityUid> _advancing = new();
    private bool _cvarsApplied;
    private bool _restartCleanup;
    private bool _prevOoc;
    private bool _prevLooc;
    // private bool _prevDeadChat;
    private bool _prevOocEnableDuringRound;
    private bool _prevDisallowLateJoin;
    private bool _prevRoleTimers;
    private bool _prevRoleWhitelist;
    private int _prevAutoCallTime;
    private bool _prevGhostRolesEnabled;
    private bool _prevVoteEnabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        // Alive /ghost uses TransferTo → MindRemovedMessage on the body. MindUnvisitedMessage only
        // fires when leaving a Visit, so it never sees living tutorial exits.
        SubscribeLocalEvent<TutorialParticipantComponent, MindRemovedMessage>(OnTutorialMindRemoved);
        SubscribeLocalEvent<TutorialPracticeMobComponent, MapInitEvent>(OnPracticeMobMapInit);
        // Do not subscribe GhostComponent.MapInit — GhostSystem already owns that directed event.
        SubscribeLocalEvent<PlayerAttachedEvent>(OnGhostPlayerAttached);
        SubscribeLocalEvent<GhostComponent, GetVerbsEvent<AlternativeVerb>>(OnGhostGetVerbs);
        SubscribeLocalEvent<TutorialChooseRoleActionEvent>(OnTutorialChooseRoleAction);
        SubscribeLocalEvent<StationPostInitEvent>(OnStationPostInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeNetworkEvent<TutorialAcknowledgeStepEvent>(OnAcknowledgeStep);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    /// <summary>
    /// Practice dummies are mindless — strip SSD ZZZ / forced sleep so they stay awake and can speak.
    /// </summary>
    private void OnPracticeMobMapInit(Entity<TutorialPracticeMobComponent> ent, ref MapInitEvent args)
    {
        RemComp<SSDIndicatorComponent>(ent);
        RemComp<SleepingComponent>(ent);
        _statusEffects.TryRemoveStatusEffect(ent.Owner, StatusEffectSsdSleepingProto);
    }

    private void OnGhostPlayerAttached(PlayerAttachedEvent args)
    {
        if (!TryGetActiveRule(out _, out _, out _))
            return;

        if (!HasComp<GhostComponent>(args.Entity))
            return;

        EnsureTutorialChooseAction(args.Entity);
    }

    /// <summary>
    /// Grants the Choose a tutorial action if the entity does not already have one.
    /// </summary>
    private void EnsureTutorialChooseAction(EntityUid uid)
    {
        foreach (var (actionUid, _) in _actions.GetActions(uid))
        {
            if (MetaData(actionUid).EntityPrototype?.ID == TutorialChooseRoleActionProto.Id)
                return;
        }

        _actions.AddAction(uid, TutorialChooseRoleActionProto);
    }

    private void OnTutorialChooseRoleAction(TutorialChooseRoleActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ActorComponent>(args.Performer, out var actor))
            return;

        // Living InTutorial bodies open the picker in-place; leaving only happens on select.
        // Ghosts / pending-select use the same entry point.
        TryOpenRolePicker(actor.PlayerSession);
        args.Handled = true;
    }

    private void OnStationPostInit(ref StationPostInitEvent args)
    {
        if (!TryGetActiveRule(out _, out _, out _))
            return;

        StripStationCentcomm();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    protected override void Started(EntityUid uid, TutorialServerRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        ApplyTutorialCVars();
        // Lobby must never pull CentComm — strip any leftover StationCentcomm from stations.
        StripStationCentcomm();
    }

    private void StripStationCentcomm()
    {
        var query = EntityQueryEnumerator<StationCentcommComponent>();
        while (query.MoveNext(out var stationUid, out var centcomm))
        {
            if (centcomm.MapEntity is { } mapUid && !TerminatingOrDeleted(mapUid))
                QueueDel(mapUid);
            RemCompDeferred<StationCentcommComponent>(stationUid);
        }
    }

    protected override void Ended(EntityUid uid, TutorialServerRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        foreach (var session in component.Sessions.Values)
        {
            if (session.MapUid != EntityUid.Invalid)
                _tutorialMaps.UnloadTutorialMap(session.MapUid);
        }

        component.Sessions.Clear();

        foreach (var eui in _openPickers.Values.ToList())
            eui.Close();
        _openPickers.Clear();

        RestoreTutorialCVars();
    }

    private void ApplyTutorialCVars()
    {
        if (_cvarsApplied)
            return;

        _prevOoc = _cfg.GetCVar(CCVars.OocEnabled);
        _prevLooc = _cfg.GetCVar(CCVars.LoocEnabled);
        // _prevDeadChat = _cfg.GetCVar(CCVars.DeadChatEnabled);
        _prevOocEnableDuringRound = _cfg.GetCVar(CCVars.OocEnableDuringRound);
        _prevDisallowLateJoin = _cfg.GetCVar(CCVars.GameDisallowLateJoins);
        _prevRoleTimers = _cfg.GetCVar(CCVars.GameRoleTimers);
        _prevRoleWhitelist = _cfg.GetCVar(CCVars.GameRoleWhitelist);
        _prevAutoCallTime = _cfg.GetCVar(CCVars.EmergencyShuttleAutoCallTime);
        _prevGhostRolesEnabled = _cfg.GetCVar(TutorialCVars.GhostRolesEnabled);
        _prevVoteEnabled = _cfg.GetCVar(CCVars.VoteEnabled);

        _cfg.SetCVar(CCVars.OocEnabled, false);
        _cfg.SetCVar(CCVars.LoocEnabled, false);
        // _cfg.SetCVar(CCVars.DeadChatEnabled, false);
        // ChatSystem re-enables ooc.enabled on PostRound/PreRoundLobby unless this is true.
        _cfg.SetCVar(CCVars.OocEnableDuringRound, true);
        _cfg.SetCVar(CCVars.GameDisallowLateJoins, false);
        _cfg.SetCVar(CCVars.GameRoleTimers, false);
        _cfg.SetCVar(CCVars.GameRoleWhitelist, false);
        // Lobby has no StationEmergencyShuttle — auto-call docks an empty set and crashes (.Max).
        _cfg.SetCVar(CCVars.EmergencyShuttleAutoCallTime, 0);
        _cfg.SetCVar(TutorialCVars.GhostRolesEnabled, false);
        _cfg.SetCVar(CCVars.VoteEnabled, false);
        // Persist across restarts / round preset reset; do not restore on rule end.
        _cfg.SetCVar(CCVars.GameLobbyDefaultPreset, "TutorialServer");
        _cfg.SetCVar(CCVars.GameLobbyFallbackPreset, "TutorialServer");
        _cvarsApplied = true;
    }

    private void RestoreTutorialCVars()
    {
        if (!_cvarsApplied)
            return;

        _cfg.SetCVar(CCVars.OocEnabled, _prevOoc);
        _cfg.SetCVar(CCVars.LoocEnabled, _prevLooc);
        // _cfg.SetCVar(CCVars.DeadChatEnabled, _prevDeadChat);
        _cfg.SetCVar(CCVars.OocEnableDuringRound, _prevOocEnableDuringRound);
        _cfg.SetCVar(CCVars.GameDisallowLateJoins, _prevDisallowLateJoin);
        _cfg.SetCVar(CCVars.GameRoleTimers, _prevRoleTimers);
        _cfg.SetCVar(CCVars.GameRoleWhitelist, _prevRoleWhitelist);
        _cfg.SetCVar(CCVars.EmergencyShuttleAutoCallTime, _prevAutoCallTime);
        _cfg.SetCVar(TutorialCVars.GhostRolesEnabled, _prevGhostRolesEnabled);
        _cfg.SetCVar(CCVars.VoteEnabled, _prevVoteEnabled);
        _cvarsApplied = false;
    }

    private bool TryGetActiveRule(out EntityUid ruleUid, out TutorialServerRuleComponent rule, out RespawnTrackerComponent tracker)
    {
        var query = EntityQueryEnumerator<TutorialServerRuleComponent, RespawnTrackerComponent, GameRuleComponent>();
        while (query.MoveNext(out ruleUid, out rule!, out tracker!, out var gameRule))
        {
            if (GameTicker.IsGameRuleActive(ruleUid, gameRule))
                return true;
        }

        ruleUid = default;
        rule = default!;
        tracker = default!;
        return false;
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if (!TryGetActiveRule(out var ruleUid, out var rule, out var tracker))
            return;

        if (!rule.Sessions.TryGetValue(ev.Player.UserId, out var session))
            session = new TutorialSessionData();

        // E2E: optionally skip picker and jump straight into a configured tutorialRole.
        var autoRole = _cfg.GetCVar(TutorialCVars.E2EAutoRole);
        if (string.IsNullOrWhiteSpace(session.SelectedRoleId) && !string.IsNullOrWhiteSpace(autoRole))
        {
            session.SelectedRoleId = autoRole;
            Log.Info($"TUTORIAL_E2E: auto_role={autoRole} for {ev.Player.Name}");
        }

        var profile = ev.Profile ?? _prefsManager.GetPreferences(ev.Player.UserId).GetRandomEnabledProfile() ??
            HumanoidCharacterProfile.DefaultWithSpecies();

        if (session.SelectedRoleId != null &&
            ProtoMan.TryIndex<TutorialRolePrototype>(session.SelectedRoleId, out var roleProto))
        {
            if (TryStartTutorial(ev.Player, profile, ruleUid, rule, tracker, roleProto)) // Starlight edit
            {
                ev.Handled = true;
                return;
            }
        }

        // Late-join with a station job → matching crew tutorial (round start keeps the picker).
        if (ev.LateJoin &&
            !string.IsNullOrEmpty(ev.JobId) &&
            TryResolveTutorialRoleForJob(ev.JobId, out var jobRole) &&
            TryStartTutorial(ev.Player, profile, ruleUid, rule, tracker, jobRole)) // Starlight edit
        {
            ev.Handled = true;
            return;
        }

        session.State = TutorialSessionState.PendingSelect;
        session.SelectedRoleId = null;
        session.PickerQuit = false;
        session.GuideAutoOpened = false;
        session.MapUid = EntityUid.Invalid;
        session.GridUid = EntityUid.Invalid;
        session.BodyUid = EntityUid.Invalid;
        session.GuideUid = EntityUid.Invalid;
        session.MentorUid = EntityUid.Invalid;
        session.StepIndex = 0;
        session.GoalIndex = 0;
        session.SubGoalIndex = 0;
        session.Completed = false;
        rule.Sessions[ev.Player.UserId] = session;

        // Handled spawn skips a body; attach an observer first so GameplayState has a valid map
        // (otherwise client input spams Map=0 transform errors and the picker can look broken).
        if (ev.Player.AttachedEntity is not { } existing || !HasComp<GhostComponent>(existing))
            GameTicker.JoinAsObserver(ev.Player);

        OpenPicker(ev.Player);
        Log.Info($"TUTORIAL_E2E: opened_role_picker for {ev.Player.Name}");
        // Claim spawn so default late-join does not place players on the lobby station.
        ev.Handled = true;
    }

    /// <summary>
    /// Maps a station job id to its crew tutorial package (<c>Tutorial{JobId}</c> preferred).
    /// </summary>
    public bool TryResolveTutorialRoleForJob(string jobId, [NotNullWhen(true)] out TutorialRolePrototype? role)
    {
        role = null;
        if (string.IsNullOrEmpty(jobId))
            return false;

        var preferredId = $"Tutorial{jobId}";
        if (ProtoMan.TryIndex<TutorialRolePrototype>(preferredId, out var preferred) &&
            preferred.Antag == null &&
            preferred.Job?.Id == jobId)
        {
            role = preferred;
            return true;
        }

        TutorialRolePrototype? match = null;
        foreach (var proto in ProtoMan.EnumeratePrototypes<TutorialRolePrototype>())
        {
            if (proto.Antag != null)
                continue;
            if (proto.Job?.Id != jobId)
                continue;
            if (match != null)
                return false; // Ambiguous job → keep picker.
            match = proto;
        }

        role = match;
        return role != null;
    }

    public void TrySelectRole(ICommonSession player, string roleId, bool confirmedStub)
    {
        if (string.IsNullOrEmpty(roleId))
            return;

        if (!TryGetActiveRule(out _, out var rule, out _))
            return;

        if (!ProtoMan.TryIndex<TutorialRolePrototype>(roleId, out var roleProto))
            return;

        if (_cfg.GetCVar(TutorialCVars.LiveTutorials) && !roleProto.LiveTutorial)
            return;

        if (IsRoleBlockedForPlayer(player, roleProto))
        {
            _chat.DispatchServerMessage(player, Loc.GetString("tutorial-server-role-species-blocked"));
            return;
        }

        if (roleProto.Stub && !confirmedStub)
        {
            _chat.DispatchServerMessage(player, Loc.GetString("tutorial-server-stub-confirm-needed"));
            return;
        }

        // Selecting a new role is what actually leaves the current tutorial.
        if (rule.Sessions.TryGetValue(player.UserId, out var existing) &&
            existing.State == TutorialSessionState.InTutorial)
        {
            LeaveCurrentTutorialForRoleChange(player);
        }

        if (!rule.Sessions.TryGetValue(player.UserId, out var session))
            session = new TutorialSessionData();

        session.SelectedRoleId = roleId;
        session.PickerQuit = false;
        session.State = TutorialSessionState.PendingSelect;
        rule.Sessions[player.UserId] = session;

        if (_openPickers.Remove(player.UserId, out var eui))
            eui.Close();

        var station = GetAnyStation();
        GameTicker.MakeJoinGame(player, station, silent: true);
    }

    /// <summary>
    /// Tears down an InTutorial session so a new role can spawn. Marks Exiting before any
    /// observer transfer so <see cref="OnTutorialMindRemoved"/> does not also open the picker.
    /// </summary>
    private void LeaveCurrentTutorialForRoleChange(ICommonSession player)
    {
        if (!TryGetActiveRule(out _, out var rule, out _))
            return;

        if (!rule.Sessions.TryGetValue(player.UserId, out var session) ||
            session.State != TutorialSessionState.InTutorial)
            return;

        // MindRemoved only acts while State == InTutorial; flip first so TransferTo is silent.
        session.State = TutorialSessionState.Exiting;
        rule.Sessions[player.UserId] = session;

        if (player.AttachedEntity is { } body && !HasComp<GhostComponent>(body))
            GameTicker.JoinAsObserver(player);

        EndTutorialSession(player.UserId, queueRespawn: false, unloadMap: true, deleteBody: true);
    }

    public void OnPickerClosed(ICommonSession player)
    {
        _openPickers.Remove(player.UserId);

        // Round restart closes EUIs while the old rule is still briefly active — do not re-open.
        if (_restartCleanup)
            return;

        if (!TryGetActiveRule(out _, out var rule, out _))
            return;

        if (!rule.Sessions.TryGetValue(player.UserId, out var session))
            return;

        // Still in a tutorial: dismiss / Quit close is a no-op beyond closing the EUI.
        if (session.State == TutorialSessionState.InTutorial)
            return;

        // Dismiss (window X) must not force-reopen — that locked living players / observers into
        // the picker. They already have Choose a tutorial on spawn and as ghosts.
        if (session.PickerQuit || session.State != TutorialSessionState.PendingSelect)
            return;

        if (session.SelectedRoleId != null)
            return;

        session.PickerQuit = true;
        rule.Sessions[player.UserId] = session;

        // BeforeSpawn may have claimed spawn without a body; keep a ghost so the action works.
        if (player.AttachedEntity is not { } body || !HasComp<GhostComponent>(body))
            GameTicker.JoinAsObserver(player);

        _chat.DispatchServerMessage(player, Loc.GetString("tutorial-server-picker-quit-tip"));
    }

    public void OnPickerQuit(ICommonSession player)
    {
        if (!TryGetActiveRule(out _, out var rule, out _))
            return;

        if (!rule.Sessions.TryGetValue(player.UserId, out var session))
            session = new TutorialSessionData();

        // Starlight begin
        // // Quit while mid-tutorial: close only — do not ghost or clear the session.
        // if (session.State == TutorialSessionState.InTutorial)
        // {
        //     if (_openPickers.Remove(player.UserId, out var inTutorialEui))
        //         inTutorialEui.Close();
        //     return;
        // }
        // Starlight end

        session.PickerQuit = true;
        session.SelectedRoleId = null;
        session.State = TutorialSessionState.PendingSelect;
        rule.Sessions[player.UserId] = session;

        if (_openPickers.Remove(player.UserId, out var eui))
            eui.Close();

        GameTicker.JoinAsObserver(player);
        EndTutorialSession(player.UserId, queueRespawn: false, unloadMap: true, deleteBody: true); // Starlight edit
    }

    /// <summary>
    /// Opens the role picker for a ghost / observer, or in-place for a living tutorial body
    /// (cancel keeps the current tutorial; selecting a role leaves it).
    /// </summary>
    public void TryOpenRolePicker(ICommonSession player)
    {
        if (!TryGetActiveRule(out _, out var rule, out _))
            return;

        if (!rule.Sessions.TryGetValue(player.UserId, out var session))
            session = new TutorialSessionData();

        // Mid-tutorial: open without ending the session. Quit / dismiss leaves them in place.
        if (session.State == TutorialSessionState.InTutorial &&
            player.AttachedEntity is { } living &&
            living == session.BodyUid &&
            !HasComp<GhostComponent>(living))
        {
            OpenPicker(player);
            return;
        }

        if (player.AttachedEntity is not { } ent || !HasComp<GhostComponent>(ent))
            return;

        session.PickerQuit = false;
        session.SelectedRoleId = null;
        session.State = TutorialSessionState.PendingSelect;
        rule.Sessions[player.UserId] = session;

        OpenPicker(player);
    }

    /// <inheritdoc cref="TryOpenRolePicker"/>
    public void TryOpenPickerForGhost(ICommonSession player) => TryOpenRolePicker(player);

    public bool IsPickerOpen(ICommonSession player) => _openPickers.ContainsKey(player.UserId);

    /// <summary>Whether the TutorialServer game rule is currently active.</summary>
    public bool IsTutorialServerActive() => TryGetActiveRule(out _, out _, out _);

    private void OnGhostGetVerbs(Entity<GhostComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryGetActiveRule(out _, out _, out _))
            return;

        if (args.User != ent.Owner)
            return;

        if (!TryComp<ActorComponent>(ent.Owner, out var actor))
            return;

        var player = actor.PlayerSession;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("tutorial-server-ghost-choose"),
            Priority = 10,
            Act = () => TryOpenRolePicker(player),
        });
    }

    private void OpenPicker(ICommonSession player)
    {
        if (_openPickers.TryGetValue(player.UserId, out var existing))
        {
            if (!existing.IsShutDown)
            {
                existing.StateDirty();
                return;
            }

            _openPickers.Remove(player.UserId);
        }

        var entries = BuildPickerEntries(player);
        var eui = new TutorialRolePickerEui(this, entries);
        _openPickers[player.UserId] = eui;
        _eui.OpenEui(eui, player);
        eui.StateDirty();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        // Clear before FlushEntities / ClearGameRules so stale EUIs cannot block a later OpenPicker.
        _restartCleanup = true;
        try
        {
            foreach (var eui in _openPickers.Values.ToList())
                eui.Close();
            _openPickers.Clear();
            _advancing.Clear();
        }
        finally
        {
            _restartCleanup = false;
        }
    }

    /// <summary>
    /// Builds the role-picker list: Start Here, Station Jobs, then remaining departments,
    /// server-specific, antagonists last. ERT packages are omitted. When
    /// <see cref="TutorialCVars.LiveTutorials"/> is set, only <c>liveTutorial</c> roles remain.
    /// </summary>
    /// <param name="player">Whose species decides what comes back blocked; null lists everything.</param>
    public List<TutorialRolePickerEntry> BuildPickerEntries(ICommonSession? player = null)
    {
        var species = GetSelectedSpecies(player);
        var liveOnly = _cfg.GetCVar(TutorialCVars.LiveTutorials);

        var list = new List<TutorialRolePickerEntry>();
        foreach (var proto in ProtoMan.EnumeratePrototypes<TutorialRolePrototype>())
        {
            if (IsErtTutorialRole(proto))
                continue;

            if (liveOnly && !proto.LiveTutorial)
                continue;

            list.Add(new TutorialRolePickerEntry
            {
                RoleId = proto.ID,
                DisplayName = GetRoleDisplayName(proto),
                Category = proto.Category,
                SubCategory = proto.SubCategory,
                Stub = proto.Stub || !proto.LiveTutorial,
                Order = proto.PickerOrder,
                BlockedForSpecies = species != null && proto.BlockedSpecies.Contains(species.Value),
            });
        }

        return list
            .OrderBy(e => GetPickerCategoryOrder(e))
            .ThenBy(e => e.Category, StringComparer.Ordinal)
            .ThenBy(e => e.SubCategory ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(e => e.Order)
            .ThenBy(e => e.DisplayName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Species on the character this player currently has selected, if any.</summary>
    private ProtoId<SpeciesPrototype>? GetSelectedSpecies(ICommonSession? player)
    {
        if (player == null)
            return null;

        if (!_prefsManager.TryGetCachedPreferences(player.UserId, out var prefs))
            return null;

        return prefs.GetRandomEnabledProfile()?.Species; // Starlight edit
    }

    /// <summary>
    /// Swaps a blocked species out of the profile the body is about to be built from. The picker
    /// reads the saved slot, which a randomised character is not, so it has to be caught here too.
    /// EnsureValid re-derives appearance and loadouts so the corrected profile stays coherent.
    /// </summary>
    private HumanoidCharacterProfile CoerceBlockedSpecies(
        ICommonSession player,
        HumanoidCharacterProfile profile,
        TutorialRolePrototype roleProto)
    {
        if (roleProto.BlockedSpecies.Count == 0 || !roleProto.BlockedSpecies.Contains(profile.Species))
            return profile;

        // Starlight begin
        var fallback = HumanoidCharacterProfile.DefaultWithSpecies();
        if (roleProto.BlockedSpecies.Contains(fallback.Species))
        {
            Log.Error($"tutorialRole {roleProto.ID} blocks its own fallback species; spawning {profile.Species} anyway");
            return profile;
        }

        Log.Info($"TUTORIAL: {player.Name} spawned as {profile.Species} into {roleProto.ID}, which blocks it; using {fallback}");

        var corrected = profile.WithSpecies(fallback.Species);
        // Starlight end
        corrected.EnsureValid(player, _deps);
        return corrected;
    }

    /// <summary>
    /// True when this player's character cannot take the role. Checked again on selection, since
    /// the picker list is only a suggestion as far as the client is concerned.
    /// </summary>
    public bool IsRoleBlockedForPlayer(ICommonSession player, TutorialRolePrototype roleProto)
    {
        if (roleProto.BlockedSpecies.Count == 0)
            return false;

        return GetSelectedSpecies(player) is { } species && roleProto.BlockedSpecies.Contains(species);
    }

    /// <summary>
    /// 0 = Start Here, 1 = Station Jobs, 2 = Passenger leftover, 3 = other departments,
    /// 4 = server-specific, 5 = antagonists.
    /// </summary>
    private static int GetPickerCategoryOrder(TutorialRolePickerEntry entry)
    {
        if (string.Equals(entry.Category, PickerCategoryStartHere, StringComparison.OrdinalIgnoreCase))
            return 0;

        if (string.Equals(entry.Category, PickerCategoryStationJobs, StringComparison.OrdinalIgnoreCase))
            return 1;

        if (entry.RoleId == "TutorialPassenger")
            return 2;

        if (string.Equals(entry.Category, PickerCategoryServerSpecific, StringComparison.OrdinalIgnoreCase))
            return 4;

        if (string.Equals(entry.Category, PickerCategoryAntagonist, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.Category, PickerCategoryWizdenAntags, StringComparison.OrdinalIgnoreCase))
            return 5;

        return 3;
    }

    private static bool IsErtTutorialRole(TutorialRolePrototype proto)
    {
        if (proto.ID.Contains("ERT", StringComparison.OrdinalIgnoreCase))
            return true;

        return proto.Job is { } job &&
               job.Id.StartsWith("ERT", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Picker label: explicit <see cref="TutorialRolePrototype.Name"/>, else antag, else job, else id.
    /// Antag is preferred over job so packages that outfit as Passenger still show the antag name.
    /// </summary>
    public string GetRoleDisplayName(TutorialRolePrototype proto)
    {
        if (!string.IsNullOrEmpty(proto.Name))
            return Loc.GetString(proto.Name);

        if (proto.Antag != null && ProtoMan.TryIndex(proto.Antag.Value, out AntagPrototype? antag))
            return Loc.GetString(antag.Name);

        if (proto.Job != null && ProtoMan.TryIndex(proto.Job.Value, out JobPrototype? job))
            return job.LocalizedName;

        return proto.ID;
    }

    private bool TryStartTutorial(
        ICommonSession player,
        Content.Shared.Preferences.HumanoidCharacterProfile profile,
        EntityUid ruleUid,
        TutorialServerRuleComponent rule,
        RespawnTrackerComponent tracker,
        TutorialRolePrototype roleProto)
    {
        if (!_tutorialMaps.TryLoadTutorialMap(roleProto, out var mapUid, out var gridUid, out var spawnCoords))
        {
            _chat.DispatchServerMessage(player, Loc.GetString("tutorial-server-map-load-failed"));
            return false;
        }

        // Role spawnOffset moves the body when the crop zone origin is outside the practice room.
        if (roleProto.SpawnOffset != System.Numerics.Vector2.Zero)
            spawnCoords = spawnCoords.Offset(roleProto.SpawnOffset);

        profile = CoerceBlockedSpecies(player, profile, roleProto);

        var mob = roleProto.SpawnEntity != null
            ? Spawn(roleProto.SpawnEntity.Value, spawnCoords)
            : roleProto.StartingGear != null
                ? SpawnAntagTutorialMob(spawnCoords, profile, player, roleProto)
                : _stationSpawning.SpawnPlayerMob(spawnCoords, roleProto.Job ?? "Passenger", profile, null);

        // Everything the player arrives wearing and carrying, and everything nested inside it, so a
        // curriculum can build a drill around the PDA and ID they already have rather than handing
        // them a tutorial-only copy. Sensors that watch their target need the component to be there
        // before the first beat, and job gear is equipped by the spawn call above.
        EnsureSensorTargetsRecursive(mob);

        // Wipe first (same as GameTicker.Respawn). CreateMind alone clears the old mind's UserId
        // and PlayerDetaches the observer while that mind still owns it; GhostOnShutdown then
        // respawns a playerless idle ghost on the lobby/observer map every tutorial start.
        _mind.WipeMind(player);
        var mindId = _mind.CreateMind(player.UserId, profile.Name);
        _mind.SetUserId(mindId, player.UserId);
        _mind.TransferTo(mindId, mob);

        // Antag tutorials use StartingGear — do not attach Passenger job clothes/roles.
        // SpawnEntity + Job is allowed (e.g. TutorialBorg uses a constrained chassis body).
        if (roleProto.StartingGear == null && roleProto.Job != null)
            _jobs.MindAddJob(mindId, roleProto.Job.Value);

        // Mind roles first so RoleRequirement placeholder objectives (e.g. dragon rifts) can attach.
        _antagBootstrap.ApplyTutorialAntag(mob, mindId, roleProto.Antag);

        if (roleProto.DragonArena != null)
        {
            _antagSelection.SendBriefing(
                player,
                Loc.GetString("tutorial-antag-dragon-briefing"),
                Color.FromHex("#c41e3a"),
                briefingSound: null);
        }

        if (TryComp<MindComponent>(mindId, out var mindComp))
        {
            foreach (var objectiveId in roleProto.PlaceholderObjectives)
                _mind.TryAddObjective(mindId, mindComp, objectiveId);

            AssignCurriculumObjectives(mindId, mindComp, roleProto);
        }

        SpawnPracticeEntities(roleProto, gridUid, spawnCoords);

        if (roleProto.Antag is { } antagId && antagId.Id == "Thief")
            _antagBootstrap.PrepareThiefPracticeMobs(gridUid);

        // Starting-gear belt/pocket tools are not practiceSpawns — tag them too so
        // UseInHand (and any future item sensors) accept belt or floor sources.
        EnsureInventorySensorTargets(mob);

        if (roleProto.ID == TutorialMedicalDoctorRole)
            TryEquipTutorialMedicalBelt(mob);

        var session = rule.Sessions.GetValueOrDefault(player.UserId) ?? new TutorialSessionData();
        session.State = TutorialSessionState.InTutorial;
        session.SelectedRoleId = roleProto.ID;
        session.MapUid = mapUid;
        session.GridUid = gridUid;
        session.BodyUid = mob;
        session.StepIndex = 0;
        session.GoalIndex = 0;
        session.SubGoalIndex = 0;
        session.Completed = false;
        session.AwaitingChamberEntryPad = false;
        session.LastChattedHint = null;
        // Sessions are reused across role reselects; forget where the last coach was projected.
        session.MentorHoloPad = EntityUid.Invalid;
        session.MentorHoloRoom = -1;
        session.MentorWalkPoint = EntityUid.Invalid;
        session.MentorWalkRoom = -1;
        rule.Sessions[player.UserId] = session;

        // Chamber 0 starts open. Later chambers unlock only when a goal sets EnterRoom
        // (or legacy room==goalIndex practice spawns).
        _tutorialRooms.UnlockGatesForGoal(gridUid, 0);

        EnsureComp<TutorialParticipantComponent>(mob);
        RefreshParticipantHud(mob, roleProto, session);
        GiveTutorialCoach(mob, session, spawnCoords, player, roleProto);
        // The HUD refresh above ran before the coach existed, so place a holopad coach now.
        _holoMentor.RefreshProjection(mob, roleProto, session);
        _leadMentor.RefreshLead(mob, roleProto, session);
        EnsureTutorialChooseAction(mob);
        rule.Sessions[player.UserId] = session;

        _respawn.AddToTracker(player.UserId, (ruleUid, tracker));
        Log.Info($"TUTORIAL_E2E: private_map_loaded role={roleProto.ID} map={mapUid} body={mob} player={player.Name}");
        RaiseNetworkEvent(new TutorialStartedEvent(), player); // Starlight
        return true;
    }

    /// <summary>
    /// Travel/off-grid arenas use a speaking handheld guide; single-grid roles (and Space Dragon,
    /// who cannot hold the tablet) get a soft-following mentor.
    /// </summary>
    public static bool UsesTravelingCoach(TutorialRolePrototype role) =>
        role.ShuttleArena != null || role.SalvageArena != null;

    private void GiveTutorialCoach(
        EntityUid mob,
        TutorialSessionData session,
        EntityCoordinates spawnCoords,
        ICommonSession player,
        TutorialRolePrototype roleProto)
    {
        // Clear any leftover coach entities when switching roles.
        if (session.GuideUid != EntityUid.Invalid && !Deleted(session.GuideUid))
            QueueDel(session.GuideUid);
        if (session.MentorUid != EntityUid.Invalid && !Deleted(session.MentorUid))
            QueueDel(session.MentorUid);
        session.GuideUid = EntityUid.Invalid;
        session.MentorUid = EntityUid.Invalid;
        session.GuideAutoOpened = false;

        if (UsesTravelingCoach(roleProto))
        {
            GiveTutorialGuide(mob, session, spawnCoords, player, roleProto);
            if (roleProto.SpawnStationaryMentor)
                GiveTutorialMentor(mob, session, spawnCoords, player, roleProto);
        }
        else
        {
            GiveTutorialMentor(mob, session, spawnCoords, player, roleProto);
        }
    }

    private void GiveTutorialGuide(
        EntityUid mob,
        TutorialSessionData session,
        EntityCoordinates spawnCoords,
        ICommonSession player,
        TutorialRolePrototype roleProto)
    {
        var guide = Spawn(TutorialGuideProto, spawnCoords);
        // Droppable so travel tutorials can free a hand (Q / Drop).
        session.GuideUid = guide;
        // Off-hand (left) so the active right hand stays free for pickup practice.
        GiveTutorialGuideToOffHand(mob, guide);

        // One-shot discoverability: chat tip + highlight popup on the tablet.
        _chat.DispatchServerMessage(player, Loc.GetString("tutorial-server-guide-tip"));
        _popup.PopupEntity(Loc.GetString("tutorial-server-guide-highlight"), guide, player, PopupType.Medium);

        if (roleProto.AutoOpenGuide)
        {
            _ui.OpenUi(guide, TutorialPromptUiKey.Key, mob);
            session.GuideAutoOpened = true;
        }
    }

    private void GiveTutorialMentor(
        EntityUid mob,
        TutorialSessionData session,
        EntityCoordinates spawnCoords,
        ICommonSession player,
        TutorialRolePrototype roleProto)
    {
        // Roles may name their own mentor body; the id chain stays for the two that predate the field.
        var mentorProto = roleProto.MentorEntity
            ?? (roleProto.ID == TutorialPassengerRole.Id
                ? TutorialPassengerMentorProto
                : roleProto.ID == TutorialCargoTechnicianRole.Id
                    ? TutorialCargoQmMentorProto
                    : TutorialMentorProto);

        var mentor = Spawn(mentorProto, spawnCoords.Offset(roleProto.MentorSpawnOffset));
        var mentorComp = EnsureComp<TutorialMentorComponent>(mentor);
        mentorComp.PlayerUid = mob;
        mentorComp.Leads = roleProto.MentorMode == TutorialMentorMode.Lead;
        // A coach carrying gloves, a belt and a card the curriculum leans on is a coach worth
        // robbing, and a player who strips him is a player whose tutorial quietly stops working.
        RemComp<StrippableComponent>(mentor);

        // Dragon coaches spawn beside the player in vacuum and snap-follow into the bay.
        if (roleProto.DragonArena != null)
            _godmode.EnableGodmode(mentor);

        // The three dots over his head between lines: a pause with no indicator reads as a coach
        // who has finished talking. Added here rather than on the prototype because the visualiser
        // reserves its sprite layer when the component arrives, and doing that before the humanoid
        // sprite is assembled puts the indicator in the wrong place. Same order the engine uses
        // when a player takes a body.
        EnsureComp<AppearanceComponent>(mentor);
        EnsureComp<TypingIndicatorComponent>(mentor);

        session.MentorUid = mentor;

        var mentorName = string.IsNullOrWhiteSpace(roleProto.MentorName)
            ? Loc.GetString("tutorial-server-mentor-default-name")
            : roleProto.MentorName;
        _meta.SetEntityName(mentor, mentorName);

        // A leading coach is aimed at the room's walk point by TutorialLeadMentorSystem on its next
        // tick; pointing him at the player first would have him take a step toward them and stop.
        if (roleProto.MentorFollows && !mentorComp.Leads && TryComp<HTNComponent>(mentor, out var htn))
        {
            _npc.SetBlackboard(mentor, NPCBlackboard.FollowTarget,
                new EntityCoordinates(mob, Vector2.Zero), htn);
            _htn.Replan(htn);
        }

        // Holopad coaches introduce themselves in character; no "follow the mentor" chatter.
        if (roleProto.MentorMode != TutorialMentorMode.Holopad)
        {
            _chat.DispatchServerMessage(player, Loc.GetString("tutorial-server-mentor-tip"));
            _popup.PopupEntity(Loc.GetString("tutorial-server-mentor-highlight"), mentor, player, PopupType.Medium);
        }
    }

    /// <summary>
    /// Puts the unremoveable tutorial tablet in the left hand and keeps the right hand active/empty.
    /// </summary>
    private void GiveTutorialGuideToOffHand(EntityUid mob, EntityUid guide)
    {
        if (!TryComp<HandsComponent>(mob, out var hands))
        {
            _hands.PickupOrDrop(mob, guide, checkActionBlocker: false);
            return;
        }

        string? leftHand = null;
        string? rightHand = null;
        foreach (var handId in _hands.EnumerateHands((mob, hands)))
        {
            if (!_hands.TryGetHand((mob, hands), handId, out var hand))
                continue;

            if (hand.Value.Location == HandLocation.Left)
                leftHand = handId;
            else if (hand.Value.Location == HandLocation.Right)
                rightHand = handId;
        }

        if (leftHand != null &&
            _hands.TryPickup(mob, guide, leftHand, checkActionBlocker: false, handsComp: hands))
        {
            if (rightHand != null)
                _hands.TrySetActiveHand((mob, hands), rightHand);
            return;
        }

        _hands.PickupOrDrop(mob, guide, checkActionBlocker: false, handsComp: hands);
        if (rightHand != null && _hands.HandIsEmpty((mob, hands), rightHand))
            _hands.TrySetActiveHand((mob, hands), rightHand);
    }

    private void TryEquipTutorialMedicalBelt(EntityUid mob)
    {
        if (_inventory.TryGetSlotEntity(mob, "belt", out _))
            return;

        var belt = Spawn(TutorialMedicalBeltProto, Transform(mob).Coordinates);
        _inventory.TryEquip(mob, belt, "belt", force: true);
        EnsureInventorySensorTargets(mob);
    }

    /// <summary>
    /// Opens the deferred guide Bound UI once. Must not run synchronously from a UseInHand /
    /// drink completion — that steals focus mid-interaction (Passenger water bottle).
    /// </summary>
    private void TryOpenDeferredGuide(ICommonSession player, EntityUid mob, ref TutorialSessionData session)
    {
        if (session.GuideAutoOpened)
            return;

        if (session.GuideUid == EntityUid.Invalid || Deleted(session.GuideUid))
            return;

        _ui.OpenUi(session.GuideUid, TutorialPromptUiKey.Key, mob);
        session.GuideAutoOpened = true;
    }

    /// <summary>
    /// Spawns a humanoid with antag starting gear + optional survival loadout (no job/Passenger gear).
    /// </summary>
    private EntityUid SpawnAntagTutorialMob(
        EntityCoordinates coordinates,
        HumanoidCharacterProfile profile,
        ICommonSession player,
        TutorialRolePrototype roleProto)
    {
        var speciesId = profile.Species;
        if (!_protos.TryIndex<SpeciesPrototype>(speciesId, out var species))
            throw new ArgumentException($"Invalid species prototype was used: {speciesId}");

        var entity = Spawn(species.Prototype, coordinates);
        // _visualBody.ApplyProfileTo(entity, profile);
        // _humanoidProfile.ApplyProfileTo(entity, profile);
        _humanoidProfile.LoadProfile(entity, profile); // Starlight
        _meta.SetEntityName(entity, profile.Name);

        if (profile.FlavorText != "" && _cfg.GetCVar(CCVars.FlavorText))
            EnsureComp<DetailExaminableComponent>(entity).Content = profile.FlavorText;

        _stationSpawning.EquipStartingGear(entity, roleProto.StartingGear, raiseEvent: false);

        var loadoutId = roleProto.RoleLoadout ?? new ProtoId<RoleLoadoutPrototype>("RoleSurvivalNukie");
        if (_protos.TryIndex(loadoutId, out RoleLoadoutPrototype? loadoutProto))
        {
            var loadout = new RoleLoadout(loadoutId);
            loadout.SetDefault(profile, player, _protos);
            _stationSpawning.EquipRoleLoadout(entity, loadout, loadoutProto);
        }

        var gearEquippedEv = new StartingGearEquippedEvent(entity);
        RaiseLocalEvent(entity, ref gearEquippedEv);
        _identity.QueueIdentityUpdate(entity);
        return entity;
    }

    private void SpawnPracticeEntities(
        TutorialRolePrototype roleProto,
        EntityUid gridUid,
        EntityCoordinates spawnCoords)
    {
        // Precompute authored coords and cluster nearby loose items so piles get sub-tile offsets.
        var authored = new EntityCoordinates[roleProto.PracticeSpawns.Count];
        var scatterable = new bool[roleProto.PracticeSpawns.Count];
        for (var i = 0; i < roleProto.PracticeSpawns.Count; i++)
        {
            var spawn = roleProto.PracticeSpawns[i];
            authored[i] = HasComp<TutorialRoomLayoutComponent>(gridUid)
                ? _tutorialRooms.GetChamberCoords(gridUid, spawn.Room, spawn.Offset)
                : spawnCoords.Offset(spawn.Offset);
            scatterable[i] = IsScatterablePracticeItem(spawn);
        }

        var scatterIdx = new int[roleProto.PracticeSpawns.Count];
        Array.Fill(scatterIdx, -1);
        const float pileRangeSq = PracticePileLookupRange * PracticePileLookupRange;
        for (var i = 0; i < roleProto.PracticeSpawns.Count; i++)
        {
            if (!scatterable[i])
                continue;

            var piled = false;
            var earlier = 0;
            for (var j = 0; j < roleProto.PracticeSpawns.Count; j++)
            {
                if (i == j || !scatterable[j])
                    continue;
                if (roleProto.PracticeSpawns[i].Room != roleProto.PracticeSpawns[j].Room)
                    continue;
                if ((authored[i].Position - authored[j].Position).LengthSquared() > pileRangeSq)
                    continue;
                piled = true;
                if (j < i)
                    earlier++;
            }

            if (!piled)
                continue;

            var idx = earlier + CountNearbyLooseItems(authored[i]);
            scatterIdx[i] = idx;
        }

        for (var i = 0; i < roleProto.PracticeSpawns.Count; i++)
        {
            var spawn = roleProto.PracticeSpawns[i];
            var coords = authored[i];

            if (scatterIdx[i] >= 0)
            {
                var idx = scatterIdx[i];
                var scatter = PracticePileScatter[idx % PracticePileScatter.Length];
                if (scatter == Vector2.Zero && idx > 0)
                    scatter = PracticePileScatter[1];
                coords = coords.Offset(scatter);
            }

            var ent = Spawn(spawn.Id, coords);
            // Include nested storage / entity-storage fills (closet tools, etc.).
            EnsureSensorTargetsRecursive(ent);

            // A door that spawns bolted has the state but not the lights: SharedDoorSystem only
            // refreshes those when something moves the bolts, and nothing has. Without this a drill
            // that opens on "see, it's bolted" has nothing for the player to see.
            if (TryComp<DoorBoltComponent>(ent, out var bolts) && bolts.BoltsDown)
                _doors.UpdateBoltLightStatus((ent, bolts));

            if (spawn.Id == TutorialHackAirlockProto)
                _tutorialRooms.PrepareHackPracticeDoor(gridUid, ent, coords.Position);

            if (!string.IsNullOrEmpty(spawn.Marker))
            {
                var marker = EnsureComp<TutorialStepMarkerComponent>(ent);
                marker.MarkerId = spawn.Marker;
                Dirty(ent, marker);
            }

            // Stamped pads all carry the prototype's default room, which would leave a holopad
            // coach projecting to wherever the player already is instead of the chamber the next
            // goal sends them to. Record the chamber this pad was actually placed in.
            if (TryComp<TutorialHoloPointComponent>(ent, out var holoPoint))
            {
                holoPoint.Room = spawn.Room;
                Dirty(ent, holoPoint);
            }

            // Same reasoning for the walking coach's waypoints: every stamped copy carries the
            // prototype's default room, so the chamber has to be recorded where it is known.
            if (TryComp<TutorialWalkPointComponent>(ent, out var walkPoint))
            {
                walkPoint.Room = spawn.Room;
                Dirty(ent, walkPoint);
            }

            if (spawn.AlwaysPowered)
                _power.SetNeedsPower(ent, false);

            if (TryComp<TutorialPracticeMobComponent>(ent, out var practiceMob) &&
                !practiceMob.SpawnDamageApplied &&
                practiceMob.SpawnDamage.AnyPositive())
            {
                _damageable.TryChangeDamage(ent, practiceMob.SpawnDamage, ignoreResistances: true, interruptsDoAfters: false);
                practiceMob.SpawnDamageApplied = true;
            }
        }

        SpawnChamberEntryPads(gridUid, roleProto);

        _tegBootstrap.TryConfigureOnGrid(gridUid);
        _researchBootstrap.TryConfigureOnGrid(gridUid);
        _cargoBootstrap.TryConfigureOnGrid(gridUid, roleProto);
        _commandBootstrap.TryConfigureOnGrid(gridUid, roleProto);
        _chemBootstrap.TryConfigureOnGrid(gridUid, roleProto);
    }

    private bool IsScatterablePracticeItem(TutorialPracticeSpawn spawn)
    {
        if (!string.IsNullOrEmpty(spawn.Marker))
            return false;

        if (!_protos.TryIndex(spawn.Id, out EntityPrototype? proto))
            return false;

        // Prefer the component registry name so inherited Item on BaseItem / BaseBeaker is found.
        return proto.Components.ContainsKey("Item");
    }

    private int CountNearbyLooseItems(EntityCoordinates coords)
    {
        var count = 0;
        foreach (var other in _lookup.GetEntitiesInRange(
                     coords,
                     PracticePileLookupRange,
                     LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Uncontained | LookupFlags.Approximate))
        {
            if (!HasComp<ItemComponent>(other))
                continue;
            if (Transform(other).Anchored)
                continue;
            count++;
        }

        return count;
    }

    /// <summary>
    /// Marks starting-gear inventory (and nested belt/pocket storage) as tutorial sensor targets
    /// so players can use either job-belt tools or floor practice spawns interchangeably.
    /// </summary>
    private void EnsureInventorySensorTargets(EntityUid mob)
    {
        var enumerator = _inventory.GetSlotEnumerator(mob);
        while (enumerator.NextItem(out var item))
            EnsureSensorTargetsRecursive(item);
    }

    /// <summary>
    /// Adds <see cref="TutorialSensorTargetComponent"/> to an entity and every nested container
    /// entity (utility-belt fills, tool closets, etc.).
    /// </summary>
    private void EnsureSensorTargetsRecursive(EntityUid uid)
    {
        EnsureComp<TutorialSensorTargetComponent>(uid);

        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return;

        foreach (var container in _containers.GetAllContainers(uid, manager))
        {
            foreach (var contained in container.ContainedEntities)
                EnsureSensorTargetsRecursive(contained);
        }
    }

    /// <summary>
    /// Spawns a glowing pad marker in each chamber after the first so chamber-entry steps can target them.
    /// </summary>
    private void SpawnChamberEntryPads(EntityUid gridUid, TutorialRolePrototype roleProto)
    {
        // Opted out: don't leave unreferenced glowing markers lying in every chamber.
        if (!roleProto.ChamberEntryPads)
            return;

        if (!TryComp<TutorialRoomLayoutComponent>(gridUid, out var layout))
            return;

        var existing = new HashSet<string>();
        foreach (var spawn in roleProto.PracticeSpawns)
        {
            if (!string.IsNullOrEmpty(spawn.Marker))
                existing.Add(spawn.Marker);
        }

        // Place just inside the chamber, toward the previous room's gate, so the glowing X is
        // visible as soon as the player walks through (not buried under mid-room props).
        for (var i = 1; i < layout.ChamberCenters.Count; i++)
        {
            var markerId = TutorialRoomLayoutComponent.ChamberEntryMarkerId(i);
            if (!existing.Add(markerId))
                continue;

            var padOffset = ResolveChamberEntryPadOffset(layout, i);
            var coords = _tutorialRooms.GetChamberCoords(gridUid, i, padOffset);
            var ent = Spawn("TutorialStepMarker", coords);
            EnsureComp<TutorialSensorTargetComponent>(ent);
            var marker = EnsureComp<TutorialStepMarkerComponent>(ent);
            marker.MarkerId = markerId;
            Dirty(ent, marker);
        }
    }

    private static TutorialSubGoalData CreateChamberEntryPadSubGoal(int chamberIndex)
    {
        return new TutorialSubGoalData
        {
            Id = $"chamber-entry-{chamberIndex}",
            Text = "tutorial-server-chamber-pad",
            Hint = "tutorial-server-chamber-pad-hint",
            StuckHint = "tutorial-server-chamber-pad-stuck",
            Complete = TutorialStepComplete.ReachMarker,
            Marker = TutorialRoomLayoutComponent.ChamberEntryMarkerId(chamberIndex),
        };
    }

    /// <summary>
    /// Offset from chamber center toward the previous chamber so the pad sits near the entry gate.
    /// </summary>
    private static System.Numerics.Vector2 ResolveChamberEntryPadOffset(
        TutorialRoomLayoutComponent layout,
        int chamberIndex)
    {
        var fallback = new System.Numerics.Vector2(0f, -1.5f);
        if (chamberIndex <= 0 || chamberIndex >= layout.ChamberCenters.Count)
            return fallback;

        var fromPrev = layout.ChamberCenters[chamberIndex] - layout.ChamberCenters[chamberIndex - 1];
        if (fromPrev.LengthSquared() < 0.01f)
            return fallback;

        // Step back from this chamber's center toward the gate (opposite of stamp direction).
        var towardGate = -System.Numerics.Vector2.Normalize(fromPrev) * 2.5f;
        return towardGate;
    }

    /// <summary>
    /// Chamber this goal unlocks/walks into, if any.
    /// Prefers explicit <see cref="TutorialGoalData.EnterRoom"/>; falls back to legacy
    /// room-index == goal-index only when no goal uses explicit chambers.
    /// </summary>
    private static int? ResolveGoalEnterRoom(TutorialRolePrototype role, int goalIndex)
    {
        if (goalIndex < 0 || goalIndex >= role.Goals.Count)
            return null;

        var goal = role.Goals[goalIndex];
        if (goal.EnterRoom is { } explicitRoom)
            return explicitRoom;

        // Mixed curricula (e.g. Technical Assistant: hack stays in room 0, spacing uses
        // enterRoom 1) must not infer room==goalIndex for goals without EnterRoom — that
        // incorrectly inserts a chamber-pad step and blocks sensors like hold-screwdriver.
        if (role.Goals.Any(g => g.EnterRoom != null))
            return null;

        if (goalIndex > 0 && role.PracticeSpawns.Any(s => s.Room == goalIndex))
            return goalIndex;

        return null;
    }

    /// <summary>
    /// True when this goal sends the player into a new chamber that needs a glowing-pad check-in.
    /// </summary>
    private bool ShouldAwaitChamberEntryPad(TutorialSessionData session, TutorialRolePrototype role)
    {
        if (!role.ChamberEntryPads)
            return false;

        var goalIndex = session.GoalIndex;
        if (goalIndex <= 0 || goalIndex >= role.Goals.Count)
            return false;

        if (!TryComp<TutorialRoomLayoutComponent>(session.GridUid, out var layout))
            return false;

        var enterRoom = ResolveGoalEnterRoom(role, goalIndex);
        if (enterRoom is not { } chamberIndex || chamberIndex <= 0)
            return false;

        if (chamberIndex >= layout.ChamberCenters.Count)
            return false;

        // Only when this stage has practice content in that chamber.
        if (!role.PracticeSpawns.Any(s => s.Room == chamberIndex))
            return false;

        // Passenger-style pry gates teach door mechanics instead of walking through.
        var gateIdx = chamberIndex - 1;
        if (gateIdx >= 0 && gateIdx < layout.GateDoors.Count)
        {
            var gate = layout.GateDoors[gateIdx];
            if (Exists(gate) &&
                TryComp<TutorialGateDoorComponent>(gate, out var gateComp) &&
                gateComp.RequirePry)
            {
                return false;
            }
        }

        var goal = role.Goals[goalIndex];

        // Goal already guides the player onto a marker in this chamber.
        foreach (var sub in goal.SubGoals)
        {
            if (sub.Complete != TutorialStepComplete.ReachMarker || string.IsNullOrEmpty(sub.Marker))
                continue;

            if (role.PracticeSpawns.Any(s => s.Marker == sub.Marker && s.Room == chamberIndex))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves the tutorial role a participant is currently running.
    /// </summary>
    public bool TryGetRole(EntityUid mob, out TutorialRolePrototype role)
    {
        role = default!;

        if (!TryGetSession(mob, out var session) || session.SelectedRoleId == null)
            return false;

        return ProtoMan.TryIndex(session.SelectedRoleId, out role!);
    }

    /// <summary>
    /// Chamber index the player is expected to be in for their current goal.
    /// </summary>
    public int ResolveCurrentRoom(TutorialRolePrototype role, TutorialSessionData session)
    {
        return ResolveGoalEnterRoom(role, session.GoalIndex) ?? 0;
    }

    /// <summary>
    /// Resolves the active sub-goal for a tutorial participant (goals curriculum or legacy steps).
    /// </summary>
    public bool TryGetCurrentSubGoal(EntityUid mob, TutorialParticipantComponent part, out TutorialSubGoalData subGoal)
    {
        subGoal = default!;

        if (!TryGetActiveRule(out _, out var rule, out _))
            return false;

        if (!TryComp<ActorComponent>(mob, out var actor))
            return false;

        if (!rule.Sessions.TryGetValue(actor.PlayerSession.UserId, out var session) ||
            session.SelectedRoleId == null ||
            !ProtoMan.TryIndex<TutorialRolePrototype>(session.SelectedRoleId, out var role))
            return false;

        if (role.Goals.Count > 0)
        {
            if (session.GoalIndex < 0 || session.GoalIndex >= role.Goals.Count)
                return false;

            if (session.AwaitingChamberEntryPad)
            {
                var padRoom = ResolveGoalEnterRoom(role, session.GoalIndex) ?? session.GoalIndex;
                subGoal = CreateChamberEntryPadSubGoal(padRoom);
                return true;
            }

            var goal = role.Goals[session.GoalIndex];
            if (session.SubGoalIndex < 0 || session.SubGoalIndex >= goal.SubGoals.Count)
                return false;

            subGoal = goal.SubGoals[session.SubGoalIndex];
            return true;
        }

        if (session.StepIndex < 0 || session.StepIndex >= role.Steps.Count)
            return false;

        var step = role.Steps[session.StepIndex];
        subGoal = new TutorialSubGoalData
        {
            Id = step.Id,
            Text = step.Text,
            Hint = step.Hint,
            StuckHint = step.StuckHint,
            Complete = step.Complete,
            Tag = step.Tag,
            Entity = step.Entity,
            Marker = step.Marker,
        };
        return true;
    }

    /// <summary>
    /// Returns true when a closed-UI progress tip may be shown (and records the timestamp).
    /// </summary>
    public bool TryConsumeProgressPopup(ICommonSession player)
    {
        if (!TryGetActiveRule(out _, out var rule, out _))
            return false;

        if (!rule.Sessions.TryGetValue(player.UserId, out var session))
            return false;

        if (session.Completed || session.State != TutorialSessionState.InTutorial)
            return false;

        var now = _timing.CurTime;
        if (now - session.LastProgressPopup < ProgressPopupCooldown)
            return false;

        session.LastProgressPopup = now;
        rule.Sessions[player.UserId] = session;
        return true;
    }

    /// <summary>
    /// True when the player's handheld guide Bound UI is currently open.
    /// </summary>
    public bool IsGuideUiOpen(EntityUid mob)
    {
        if (!TryGetSession(mob, out var session))
            return false;

        if (session.GuideUid == EntityUid.Invalid || TerminatingOrDeleted(session.GuideUid))
            return false;

        return _ui.IsUiOpen(session.GuideUid, TutorialPromptUiKey.Key);
    }

    /// <summary>
    /// Sends a tip to the player's chat. Markup may include <c>[keybind]</c> tags resolved client-side.
    /// </summary>
    public void SendTipChat(EntityUid mob, string markup)
    {
        if (!TryComp<ActorComponent>(mob, out var actor))
            return;

        SendTipChat(actor.PlayerSession, markup);
    }

    /// <summary>
    /// Sends a tip to the player's chat. Markup may include <c>[keybind]</c> tags resolved client-side.
    /// </summary>
    public void SendTipChat(ICommonSession player, string markup)
    {
        if (string.IsNullOrWhiteSpace(markup))
            return;

        RaiseNetworkEvent(new TutorialTipChatEvent { Markup = markup }, player.Channel);
    }

    /// <summary>
    /// Pushes the on-screen control hint for the current sub-goal, or hides the banner when the
    /// step teaches no control. <paramref name="locId"/> markup may include <c>[keybind]</c> tags,
    /// which the client resolves against the player's own bindings.
    /// </summary>
    /// <summary>
    /// Shown in the control-hint banner when the curriculum runs out.
    /// </summary>
    private const string TutorialFinishedHint = "tutorial-server-tutorial-finished-hint";

    /// <summary>
    /// Stands in for a sub-goal once the curriculum runs out, until the session is closed off.
    /// </summary>
    private const string CompleteText = "tutorial-server-complete";

    /// <summary>
    /// A beat that ends itself once the coach has finished asks nothing of the player, so it gets
    /// no banner: the objective line only says "listen", and it would still be up after she had
    /// stopped. Acknowledge beats that wait to be clicked through do get one.
    /// </summary>
    private static bool IsSelfAdvancingNarration(TutorialSubGoalData sub)
        => sub.Complete == TutorialStepComplete.Acknowledge && sub.AutoAdvanceSeconds != null;

    public void SendControlHint(EntityUid mob, string? locId)
    {
        if (!TryComp<ActorComponent>(mob, out var actor))
            return;

        var show = !string.IsNullOrEmpty(locId);
        RaiseNetworkEvent(
            new TutorialControlHintEvent
            {
                Markup = show ? Loc.GetString(locId!) : string.Empty,
                Show = show,
            },
            actor.PlayerSession.Channel);
    }

    /// <summary>
    /// Time the player's current sub-goal has been active. Used for narration beats that
    /// auto-advance before the player has been taught to click anything.
    /// </summary>
    public bool TryGetSubGoalElapsed(EntityUid mob, out TimeSpan elapsed)
    {
        elapsed = TimeSpan.Zero;

        if (!TryGetSession(mob, out var session))
            return false;

        elapsed = _timing.CurTime - session.SubGoalStartedAt;
        return true;
    }

    /// <summary>
    /// True while this sub-goal's control hint is still waiting on the coach to finish.
    /// </summary>
    public bool HasPendingControlHint(EntityUid mob)
        => TryGetSession(mob, out var session) && !session.ControlHintShown;

    /// <summary>
    /// Pushes the held-back control hint to the client. No-op once already shown.
    /// </summary>
    public void ShowPendingControlHint(EntityUid mob)
    {
        if (!TryGetSession(mob, out var session) || session.ControlHintShown)
            return;

        session.ControlHintShown = true;
        SendControlHint(mob, session.PendingControlHint);
        EchoControlHintToChat(mob, session);
    }

    /// <summary>
    /// Repeats the banner into chat as it goes up, so an instruction the player looked away from
    /// is still somewhere they can scroll back to.
    /// </summary>
    /// <remarks>
    /// Skips the terminal hints: <see cref="CompleteTutorial"/> dispatches its own sign-off, and
    /// echoing these would leave two endings in the log.
    /// </remarks>
    private void EchoControlHintToChat(EntityUid mob, TutorialSessionData session)
    {
        var hint = session.PendingControlHint;
        if (string.IsNullOrEmpty(hint) ||
            hint == TutorialFinishedHint ||
            hint == CompleteText ||
            hint == session.LastChattedHint)
        {
            return;
        }

        session.LastChattedHint = hint;
        SendTipChat(mob, Loc.GetString(hint));
    }

    public bool TryGetSession(EntityUid mob, out TutorialSessionData session)
    {
        session = default!;

        if (!TryComp<ActorComponent>(mob, out var actor))
            return false;

        if (!TryGetActiveRule(out _, out var rule, out _))
            return false;

        return rule.Sessions.TryGetValue(actor.PlayerSession.UserId, out session!);
    }

    /// <summary>
    /// True when a living mentor is on the same map within coach range of the player.
    /// Hybrid cargo uses this so the bay QM and orange tip chat do not double the same step.
    /// </summary>
    public bool IsMentorCoachingInRange(EntityUid mob)
    {
        if (!TryGetSession(mob, out var session))
            return false;

        if (session.MentorUid == EntityUid.Invalid || TerminatingOrDeleted(session.MentorUid))
            return false;

        if (!TryComp(mob, out TransformComponent? mobXform) ||
            !TryComp(session.MentorUid, out TransformComponent? mentorXform))
            return false;

        if (mobXform.MapUid == null || mobXform.MapUid != mentorXform.MapUid)
            return false;

        var delta = _transform.GetWorldPosition(mobXform) - _transform.GetWorldPosition(mentorXform);
        return delta.Length() <= MentorCoachRange;
    }

    private void AssignCurriculumObjectives(EntityUid mindId, MindComponent mind, TutorialRolePrototype role)
    {
        if (role.Goals.Count == 0)
            return;

        for (var i = 0; i < role.Goals.Count; i++)
        {
            if (_objectives.TryCreateObjective(mindId, mind, TutorialCurriculumGoalProto) is not { } objectiveUid)
                continue;

            var cond = EnsureComp<TutorialGoalConditionComponent>(objectiveUid);
            cond.GoalIndex = i;

            var goal = role.Goals[i];
            _meta.SetEntityName(objectiveUid, Loc.GetString(goal.Title));
            var desc = goal.SubGoals.Count > 0
                ? FormattedMessage.RemoveMarkupPermissive(Loc.GetString(goal.SubGoals[0].Text))
                : Loc.GetString("tutorial-server-objective-goal-pending");
            _meta.SetEntityDescription(objectiveUid, desc);

            _mind.AddObjective(mindId, mind, objectiveUid);
        }
    }

    private void SyncCurriculumObjectives(EntityUid mob, TutorialRolePrototype role, TutorialParticipantComponent part)
    {
        if (role.Goals.Count == 0)
            return;

        if (!_mind.TryGetMind(mob, out var mindId, out var mind))
            return;

        foreach (var objectiveUid in mind.Objectives)
        {
            if (!TryComp<TutorialGoalConditionComponent>(objectiveUid, out var cond))
                continue;

            _goalObjectives.SyncObjectiveText(objectiveUid, cond, role, part);
        }
    }

    public void AdvanceSubGoal(EntityUid mob)
    {
        if (!_advancing.Add(mob))
            return;

        try
        {
            if (!TryComp<ActorComponent>(mob, out var actor))
                return;

            AdvanceSubGoal(actor.PlayerSession, mob);
        }
        finally
        {
            _advancing.Remove(mob);
        }
    }

    private void RefreshParticipantHud(EntityUid mob, TutorialRolePrototype role, TutorialSessionData session)
    {
        var part = EnsureComp<TutorialParticipantComponent>(mob);
        var oldGoalIndex = part.GoalIndex;
        var oldProgress = role.Goals.Count > 0 ? part.SubGoalIndex : part.StepIndex;
        part.SubGoalStates.Clear();

        // Locale id of the on-screen control hint for the sub-goal that ends up current.
        string? controlHint = null;

        if (role.Goals.Count > 0)
        {
            part.GoalCount = role.Goals.Count;
            part.GoalIndex = session.GoalIndex;
            part.StepCount = 0;
            part.StepIndex = 0;

            if (session.GoalIndex >= 0 && session.GoalIndex < role.Goals.Count)
            {
                var goal = role.Goals[session.GoalIndex];
                part.GoalTitle = Loc.GetString(goal.Title);

                var needsPad = ShouldAwaitChamberEntryPad(session, role);
                var padActive = session.AwaitingChamberEntryPad;
                var padOffset = needsPad ? 1 : 0;
                part.SubGoalCount = goal.SubGoals.Count + padOffset;
                part.SubGoalIndex = padActive ? 0 : session.SubGoalIndex + padOffset;

                if (needsPad)
                {
                    part.SubGoalStates.Add(new TutorialHudSubGoalState
                    {
                        Text = Loc.GetString("tutorial-server-chamber-pad"),
                        Completed = !padActive,
                    });
                }

                for (var i = 0; i < goal.SubGoals.Count; i++)
                {
                    part.SubGoalStates.Add(new TutorialHudSubGoalState
                    {
                        Text = Loc.GetString(goal.SubGoals[i].Text),
                        Completed = !padActive && i < session.SubGoalIndex,
                    });
                }

                if (padActive)
                {
                    var padRoom = ResolveGoalEnterRoom(role, session.GoalIndex) ?? session.GoalIndex;
                    var pad = CreateChamberEntryPadSubGoal(padRoom);
                    controlHint = pad.Text;
                    part.StepText = Loc.GetString(pad.Text);
                    part.StepComplete = pad.Complete;
                    part.HintText = Loc.GetString(pad.Hint!);
                    part.StuckHintText = Loc.GetString(pad.StuckHint!);
                }
                else if (session.SubGoalIndex >= 0 && session.SubGoalIndex < goal.SubGoals.Count)
                {
                    var sub = goal.SubGoals[session.SubGoalIndex];
                    controlHint = sub.ControlHint;

                    // No control to teach still gets a banner: the objective line is already the
                    // short imperative the player needs, and a blank corner reads as "nothing to do".
                    if (string.IsNullOrEmpty(controlHint) &&
                        !IsSelfAdvancingNarration(sub) &&
                        !sub.SuppressControlHint)
                        controlHint = sub.Text;
                    else if (sub.SuppressControlHint)
                        controlHint = null;
                    part.StepText = Loc.GetString(sub.Text);
                    part.StepComplete = sub.Complete;
                    part.HintText = string.IsNullOrEmpty(sub.Hint) ? string.Empty : Loc.GetString(sub.Hint);
                    part.StuckHintText = string.IsNullOrEmpty(sub.StuckHint)
                        ? string.Empty
                        : Loc.GetString(sub.StuckHint);
                }
                else
                {
                    controlHint = CompleteText;
                    part.StepText = Loc.GetString(CompleteText);
                    part.StepComplete = TutorialStepComplete.Acknowledge;
                    part.HintText = string.Empty;
                    part.StuckHintText = string.Empty;
                }
            }
            else
            {
                part.GoalTitle = Loc.GetString("tutorial-server-complete");
                part.StepText = Loc.GetString("tutorial-server-complete");
                part.StepComplete = TutorialStepComplete.Acknowledge;
                part.HintText = string.Empty;
                part.StuckHintText = string.Empty;
                part.SubGoalCount = 0;
                part.SubGoalIndex = 0;
            }
        }
        else
        {
            part.GoalCount = 0;
            part.GoalIndex = 0;
            part.GoalTitle = string.Empty;
            part.SubGoalCount = 0;
            part.SubGoalIndex = 0;
            part.StepIndex = session.StepIndex;
            part.StepCount = role.Steps.Count;

            if (session.StepIndex >= 0 && session.StepIndex < role.Steps.Count)
            {
                var step = role.Steps[session.StepIndex];
                part.StepText = Loc.GetString(step.Text);
                part.StepComplete = step.Complete;
                part.HintText = string.IsNullOrEmpty(step.Hint) ? string.Empty : Loc.GetString(step.Hint);
                part.StuckHintText = string.IsNullOrEmpty(step.StuckHint)
                    ? string.Empty
                    : Loc.GetString(step.StuckHint);
            }
            else
            {
                part.StepText = Loc.GetString("tutorial-server-complete");
                part.StepComplete = TutorialStepComplete.Acknowledge;
                part.HintText = string.Empty;
                part.StuckHintText = string.Empty;
            }
        }

        Dirty(mob, part);

        // Here rather than in AdvanceSubGoal, so a reconnect cannot leave a gate shut behind them.
        if (session.GridUid != EntityUid.Invalid && role.Goals.Count > 0)
            _tutorialRooms.RefreshSubGoalGates(session.GridUid, id => HasReachedSubGoal(role, session, id));

        // Held back until the coach stops talking — see TutorialGoalSensorSystem.TryReleaseControlHint.
        // Two things competing for the player's eye at once is how they end up reading neither.
        session.PendingControlHint = controlHint;
        session.ControlHintShown = false;
        SendControlHint(mob, null);

        SyncCurriculumObjectives(mob, role, part);

        session.SubGoalStartedAt = _timing.CurTime;

        // Always notify: guide UI sync and/or closed-UI progress tips (mentor roles speak instead).
        var ev = new TutorialParticipantProgressChangedEvent(session.GuideUid, oldGoalIndex, oldProgress);
        RaiseLocalEvent(mob, ref ev);

        // Holopad coaches skip the walk entirely: they re-project at the new chamber's pad.
        _holoMentor.RefreshProjection(mob, role, session);
        _leadMentor.RefreshLead(mob, role, session);

        // Chamber transitions can leave the mentor behind a sealed gate — give them time to walk
        // before TutorialMentorFollowSystem path-checks and (only if stuck) snaps.
        if (role.MentorFollows &&
            session.MentorUid != EntityUid.Invalid &&
            !TerminatingOrDeleted(session.MentorUid) &&
            oldGoalIndex != session.GoalIndex)
        {
            _mentorFollow.RequestCatchUp(session.MentorUid, restart: true);
        }
    }

    /// <summary>
    /// True when the player is at, or past, the sub-goal with this id. At, because what is keyed to
    /// a sub-goal is the staging for it. An unknown id is never reached, so a typo fails shut.
    /// </summary>
    private static bool HasReachedSubGoal(TutorialRolePrototype role, TutorialSessionData session, string subGoalId)
    {
        for (var g = 0; g < role.Goals.Count; g++)
        {
            var subs = role.Goals[g].SubGoals;
            for (var s = 0; s < subs.Count; s++)
            {
                if (subs[s].Id != subGoalId)
                    continue;

                return session.GoalIndex > g || (session.GoalIndex == g && session.SubGoalIndex >= s);
            }
        }

        return false;
    }

    private static bool TryGetCurrentSubGoalId(
        TutorialRolePrototype role,
        TutorialSessionData session,
        out string subGoalId)
    {
        subGoalId = string.Empty;
        if (session.AwaitingChamberEntryPad)
            return false;
        if (session.GoalIndex < 0 || session.GoalIndex >= role.Goals.Count)
            return false;
        var goal = role.Goals[session.GoalIndex];
        if (session.SubGoalIndex < 0 || session.SubGoalIndex >= goal.SubGoals.Count)
            return false;
        subGoalId = goal.SubGoals[session.SubGoalIndex].Id;
        return !string.IsNullOrEmpty(subGoalId);
    }

    private void OnAcknowledgeStep(TutorialAcknowledgeStepEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } mob)
            return;

        if (!TryComp<TutorialParticipantComponent>(mob, out var part))
            return;

        if (part.StepComplete != TutorialStepComplete.Acknowledge)
            return;

        AdvanceSubGoal(args.SenderSession, mob);
    }

    private void AdvanceSubGoal(ICommonSession player, EntityUid mob)
    {
        if (!TryGetActiveRule(out _, out var rule, out _))
            return;

        if (!rule.Sessions.TryGetValue(player.UserId, out var session) ||
            session.SelectedRoleId == null ||
            !ProtoMan.TryIndex<TutorialRolePrototype>(session.SelectedRoleId, out var role))
            return;

        if (role.Goals.Count > 0)
        {
            if (session.GoalIndex < 0 || session.GoalIndex >= role.Goals.Count)
                return;

            if (session.AwaitingChamberEntryPad)
            {
                session.AwaitingChamberEntryPad = false;
                // Safe moment: player finished walking onto the chamber pad (not mid-UseInHand).
                if (!role.AutoOpenGuide && session.GoalIndex >= 1)
                    TryOpenDeferredGuide(player, mob, ref session);

                rule.Sessions[player.UserId] = session;
                RefreshParticipantHud(mob, role, session);
                return;
            }

            var goal = role.Goals[session.GoalIndex];

            // The coach may owe this beat a reaction: a line written to land after the player has
            // done the thing rather than while they are still being told to. Hold the beat where it
            // is and let him say it; he calls back here when he runs out of words.
            if (session.SubGoalIndex >= 0 &&
                session.SubGoalIndex < goal.SubGoals.Count &&
                _trainer.TryStartReaction(mob, goal.SubGoals[session.SubGoalIndex].Id))
            {
                return;
            }

            session.SubGoalIndex++;

            if (session.SubGoalIndex >= goal.SubGoals.Count)
            {
                session.GoalIndex++;
                session.SubGoalIndex = 0;

                if (session.GoalIndex >= role.Goals.Count)
                {
                    session.Completed = true;
                    rule.Sessions[player.UserId] = session;
                    CompleteTutorial(player, session);
                    return;
                }

                // Open the door into the chamber this goal enters (if any).
                if (session.GridUid != EntityUid.Invalid &&
                    ResolveGoalEnterRoom(role, session.GoalIndex) is { } enterRoom &&
                    enterRoom > 0)
                {
                    _tutorialRooms.UnlockGatesForGoal(session.GridUid, enterRoom);
                }

                session.AwaitingChamberEntryPad = ShouldAwaitChamberEntryPad(session, role);

                // Do NOT open the deferred guide here. Passenger welcome ends on drink-water
                // (UseInHand); opening the Bound UI in that same AdvanceSubGoal steals focus
                // from the bottle. Open on chamber-pad check-in instead (see above), or leave
                // the tablet for the player when there is no pad (e.g. pry-exit Passenger).
            }

            rule.Sessions[player.UserId] = session;
            RefreshParticipantHud(mob, role, session);
            return;
        }

        session.StepIndex++;
        if (session.StepIndex >= role.Steps.Count)
        {
            session.Completed = true;
            rule.Sessions[player.UserId] = session;
            CompleteTutorial(player, session);
            return;
        }

        rule.Sessions[player.UserId] = session;
        RefreshParticipantHud(mob, role, session);
    }

    private void CompleteTutorial(ICommonSession player, TutorialSessionData session)
    {
        // Stay on the practice map with Choose a tutorial — do not force-respawn to the picker.
        _chat.DispatchServerMessage(player, Loc.GetString("tutorial-server-tutorial-finished"));

        var mob = session.BodyUid;
        if (mob == EntityUid.Invalid || TerminatingOrDeleted(mob))
            return;

        // Same banner the control hints use: chat is where the player has been told to stop
        // looking by this point in the tutorial.
        session.PendingControlHint = TutorialFinishedHint;
        session.ControlHintShown = true;
        SendControlHint(mob, TutorialFinishedHint);

        _holoMentor.EndProjection(session, Transform(mob).MapUid);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!TryComp<ActorComponent>(args.Target, out var actor))
            return;

        if (!TryGetActiveRule(out _, out var rule, out _))
            return;

        if (!rule.Sessions.TryGetValue(actor.PlayerSession.UserId, out var session))
            return;

        if (session.State != TutorialSessionState.InTutorial || session.BodyUid != args.Target)
            return;

        // RespawnRule will rejoin; unload the private map. Mind leaves via respawn delete.
        EndTutorialSession(actor.PlayerSession.UserId, queueRespawn: false, unloadMap: true, deleteBody: false);
    }

    private void OnTutorialMindRemoved(Entity<TutorialParticipantComponent> ent, ref MindRemovedMessage args)
    {
        if (!TryGetActiveRule(out _, out var rule, out _))
            return;

        NetUserId? userId = null;
        foreach (var (id, session) in rule.Sessions)
        {
            if (session.State == TutorialSessionState.InTutorial && session.BodyUid == ent.Owner)
            {
                userId = id;
                break;
            }
        }

        if (userId == null)
            return;

        // Death keeps the RespawnDeadRule delay → MakeJoinGame → picker. Alive /ghost needs
        // an immediate picker, and must leave the private map before unload deletes the ghost.
        var bodyDead = TryComp<MobStateComponent>(ent, out var mobState) &&
                       mobState.CurrentState == MobState.Dead;

        // Choose a tutorial / JoinAsObserver / /ghost already TransferTo a ghost. Nesting another
        // JoinAsObserver here spawned a spare observer and unloaded the private map mid-TransferTo,
        // which corrupted client game-state and surfaced as "Failed to deserialize packet" on the
        // next Passenger select.
        var transferringToGhost = args.TransferEntity is { } dest && HasComp<GhostComponent>(dest);
        var uid = userId.Value;
        var needObserver = !bodyDead && !transferringToGhost;
        var openPicker = !bodyDead;
        var deleteBody = !bodyDead;

        // Finish TransferTo / actor attach before QueueDel of the practice map.
        Timer.Spawn(0, () =>
        {
            if (!TryGetActiveRule(out _, out var activeRule, out _))
                return;

            if (!activeRule.Sessions.TryGetValue(uid, out var session) ||
                session.State != TutorialSessionState.InTutorial)
                return;

            if (needObserver && _players.TryGetSessionById(uid, out var playerSession))
                GameTicker.JoinAsObserver(playerSession);

            EndTutorialSession(uid, queueRespawn: false, unloadMap: true, deleteBody: deleteBody);

            if (openPicker && _players.TryGetSessionById(uid, out playerSession))
                TryOpenPickerForGhost(playerSession);
        });
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Disconnected)
            return;

        EndTutorialSession(e.Session.UserId, queueRespawn: false, unloadMap: true, deleteBody: false);
        if (_openPickers.Remove(e.Session.UserId, out var eui))
            eui.Close();
    }

    /// <summary>
    /// Ordered teardown: mark exiting, optional body delete, unload map, clear session, optional respawn.
    /// </summary>
    public void EndTutorialSession(
        NetUserId userId,
        bool queueRespawn,
        bool unloadMap,
        bool deleteBody)
    {
        if (!TryGetActiveRule(out var ruleUid, out var rule, out var tracker))
            return;

        if (!rule.Sessions.TryGetValue(userId, out var session))
            return;

        session.State = TutorialSessionState.Exiting;
        var mapUid = session.MapUid;
        var bodyUid = session.BodyUid;

        var guideUid = session.GuideUid;
        var mentorUid = session.MentorUid;
        session.MapUid = EntityUid.Invalid;
        session.GridUid = EntityUid.Invalid;
        session.BodyUid = EntityUid.Invalid;
        session.GuideUid = EntityUid.Invalid;
        session.MentorUid = EntityUid.Invalid;
        session.SelectedRoleId = null;
        session.StepIndex = 0;
        session.GoalIndex = 0;
        session.SubGoalIndex = 0;
        session.Completed = false;
        session.GuideAutoOpened = false;
        session.PickerQuit = false;
        session.State = TutorialSessionState.PendingSelect;
        rule.Sessions[userId] = session;

        if (guideUid != EntityUid.Invalid && !TerminatingOrDeleted(guideUid))
            QueueDel(guideUid);

        if (mentorUid != EntityUid.Invalid && !TerminatingOrDeleted(mentorUid))
            QueueDel(mentorUid);

        if (deleteBody && bodyUid != EntityUid.Invalid && !TerminatingOrDeleted(bodyUid))
            QueueDel(bodyUid);

        if (unloadMap && mapUid != EntityUid.Invalid)
            _tutorialMaps.UnloadTutorialMap(mapUid);

        // Starlight begin
        if (!_players.TryGetSessionById(userId, out var playerSession))
            return;

        if (queueRespawn)
        {
            _respawn.AddToTracker(userId, (ruleUid, tracker));
            GameTicker.MakeJoinGame(playerSession, GetAnyStation(), silent: true);
        }

        RaiseNetworkEvent(new TutorialEndedEvent(), playerSession);
        // Starlight end
    }

    private EntityUid GetAnyStation()
    {
        foreach (var station in _station.GetStations())
            return station;
        return EntityUid.Invalid;
    }
}
