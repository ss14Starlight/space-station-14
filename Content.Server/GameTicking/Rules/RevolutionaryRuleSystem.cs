using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.EUI;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Revolutionary;
using Content.Server.Revolutionary.Components;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Database;
using Content.Shared.Flash;
using Content.Shared.GameTicking.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Revolutionary.Components;
using Content.Shared.Roles.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Cuffs.Components;
using Content.Shared.Store;
using Robust.Shared.Player;
using Content.Server._Starlight.Achievement;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.Store.Systems;
using Content.Shared.Whitelist;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Store.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Content.Shared.Station.Components;
using Content.Server.Shuttles.Components;
using Content.Server._Starlight.GameTicking;
using Content.Shared._Starlight.Implants.Components;
using Content.Shared._Starlight.Revolutionary.Components;
using Content.Server._Starlight.Revolutionary.Components;
using Content.Server._Starlight.Statistics;

namespace Content.Server.GameTicking.Rules;

/// <summary>
/// Where all the main stuff for Revolutionaries happens (Assigning Head Revs, Command on station, and checking for the game to end.)
/// </summary>
public sealed partial class RevolutionaryRuleSystem : GameRuleSystem<RevolutionaryRuleComponent>
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private EuiManager _euiMan = default!;
    [Dependency] private IAdminLogManager _adminLogManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private RoleSystem _role = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private RoundStatisticsSystem _roundStatistics = default!; // Starlight
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StationSystem _stationSystem = default!;

    // Starlight-start
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SpecialLobbyContentSystem _specialLobbyContent = default!;
    [Dependency] private AchievementSystem _achievements = default!;
    [Dependency] private AlertLevelSystem _alert = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private StoreSystem _store = default!;

    // last time at least 1 command member was on station
    private TimeSpan _commandLastTimeOnStation = default;
    // Starlight-end

    //Used in OnPostFlash, no reference to the rule component is available
    public readonly ProtoId<NpcFactionPrototype> RevolutionaryNpcFaction = "Revolutionary";
    public readonly ProtoId<NpcFactionPrototype> RevPrototypeId = "Rev";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CommandStaffComponent, MobStateChangedEvent>(OnCommandMobStateChanged);

        SubscribeLocalEvent<HeadRevolutionaryComponent, AfterFlashedEvent>(OnPostFlash);
        SubscribeLocalEvent<HeadRevolutionaryComponent, ComponentInit>(OnHeadRevStartup);
        SubscribeLocalEvent<HeadRevolutionaryComponent, MobStateChangedEvent>(OnHeadRevMobStateChanged);

        SubscribeLocalEvent<RevolutionaryRoleComponent, GetBriefingEvent>(OnGetBriefing);
        SubscribeLocalEvent<RevolutionaryRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagEntitySelected); // Starlight
    }

    // Starlight Start
    private void OnAfterAntagEntitySelected(EntityUid uid, RevolutionaryRuleComponent comp, ref AfterAntagEntitySelectedEvent args)
    {
        // Send a custom briefing with the character's name
        var name = Identity.Name(args.EntityUid, EntityManager);
        _antag.SendBriefing(args.Session, Loc.GetString("head-rev-role-greeting", ("name", name)), Color.LightYellow, new SoundPathSpecifier("/Audio/Ambience/Antag/headrev_start.ogg"));
    }
    // Starlight End

    protected override void Started(EntityUid uid, RevolutionaryRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        component.CommandCheck = _timing.CurTime + component.TimerWait;

        _commandLastTimeOnStation = _timing.CurTime; // Starlight
    }

    protected override void ActiveTick(EntityUid uid, RevolutionaryRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);
        if (component.CommandCheck <= _timing.CurTime)
        {
            component.CommandCheck = _timing.CurTime + component.TimerWait;

            //starlight, check if revs have lost
            if (CheckRevsLose())
            {
                GameTicker.EndGameRule(uid, gameRule);
            }

            #region Starlight
            if (CheckCommandLose(component))
            {
                _roundEnd.CancelRoundEndCountdown(null, null, false);
                AwardRevolutionaryVictoryAchievements();

                // Play the revolutionary end sound globally
                var filter = Filter.Broadcast();
                _audioSystem.PlayGlobal("/Audio/_Starlight/Effects/sov_choir_global.ogg", filter, false);

                // First, end the game rule
                GameTicker.EndGameRule(uid, gameRule);

                // Check if the emergency shuttle is already called (not just arrived)
                if (_roundEnd.IsRoundEndRequested())
                {
                    // If the shuttle is already called, we need to recall it
                    // Cancel the current shuttle call - force it with false for checkCooldown
                    _roundEnd.CancelRoundEndCountdown(null, null, false);
                }

                // Use a safer approach for scheduling the announcements
                // Schedule the first announcement after 7 seconds
                Timer.Spawn(TimeSpan.FromSeconds(7), () =>
                {
                    try
                    {
                        // Send Central Command announcement
                        _chatSystem.DispatchGlobalAnnouncement(
                            Loc.GetString("central-command-revolution-announcement"),
                            Loc.GetString("central-command-sender"),
                            true,
                            new SoundPathSpecifier("/Audio/_Starlight/Announcements/announce_broken.ogg"),
                            Color.Red
                        );

                        // Remove event schedulers
                        RemoveEventSchedulers();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error during first announcement: {ex}");
                    }
                });

                // Schedule the second announcement separately after 22 seconds (7 + 15)
                Timer.Spawn(TimeSpan.FromSeconds(32), () =>
                {
                    try
                    {
                        // Send Soviet People's Commissariat announcement
                        _chatSystem.DispatchGlobalAnnouncement(
                            Loc.GetString("soviet-commissariat-revolution-announcement"),
                            Loc.GetString("soviet-commissariat-sender"),
                            true,
                            new SoundPathSpecifier("/Audio/_Starlight/Announcements/sov_announce.ogg"),
                            Color.Yellow
                        );

                        // Wait a short time to ensure the announcement is heard before ending the round
                        Timer.Spawn(TimeSpan.FromSeconds(4), () =>
                        {
                            // STARLIGHT: Set special lobby content for revolutionary victory using the modular system
                            _specialLobbyContent.SetSpecialLobbyContent(uid);

                            // End the round
                            // _audioSystem.PlayGlobal("/Audio/_Starlight/Misc/sov_win.ogg", filter, false);
                            _roundEnd.EndRound();
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error during second announcement: {ex}");
                        // Still try to end the round even if the announcement fails
                        _roundEnd.EndRound();
                    }
                });
            }
            #endregion Starlight
        }
    }

    protected override void AppendRoundEndText(EntityUid uid,
        RevolutionaryRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        var revsLost = CheckRevsLose();
        var commandLost = CheckCommandLose(component); // Starlight
        // This is (revsLost, commandsLost) concatted together
        // (moony wrote this comment idk what it means)
        var index = (commandLost ? 1 : 0) | (revsLost ? 2 : 0);
        args.AddLine(Loc.GetString(Outcomes[index]));
        _roundStatistics.RecordAntagOutcome(uid, "Revolutionary", _results[index]); // Starlight

        var sessionData = _antag.GetAntagIdentifiers(uid).ToList();
        args.AddLine(Loc.GetString("rev-headrev-count", ("initialCount", sessionData.Count)));
        _roundStatistics.RecordAntagOutcomeStat("Revolutionary", "head_revs", sessionData.Count); // Starlight
        foreach (var (mind, data, name) in sessionData)
        {
            _role.MindHasRole<RevolutionaryRoleComponent>(mind, out var role);
            var count = CompOrNull<RevolutionaryRoleComponent>(role)?.ConvertedCount ?? 0;
            _roundStatistics.RecordAntagOutcomeStat("Revolutionary", "converts", count); // Starlight

            args.AddLine(Loc.GetString("rev-headrev-name-user",
                ("name", name),
                ("username", data.UserName),
                ("count", count)));

            // TODO: someone suggested listing all alive? revs maybe implement at some point
        }
        args.AddLine("");
    }

    private void OnGetBriefing(EntityUid uid, RevolutionaryRoleComponent comp, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;
        var head = HasComp<HeadRevolutionaryComponent>(ent);
        args.Append(Loc.GetString(head ? "head-rev-briefing" : "rev-briefing"));
    }
    // Starlight Start: Rev Victory Achievement
    private void AwardRevolutionaryVictoryAchievements()
    {
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } attached)
                continue;

            if (!_mind.TryGetMind(session.UserId, out var mindId, out _)
                || mindId is not { } resolvedMindId)
                continue;

            if (!_role.MindHasRole<RevolutionaryRoleComponent>(resolvedMindId)
                && !HasComp<HeadRevolutionaryComponent>(attached))
            {
                continue;
            }

            _achievements.QueueUnlockAchievement(attached, "viva");
        }
    }
    // Starlight End

    private void OnHeadRevStartup(EntityUid uid, HeadRevolutionaryComponent comp, ComponentInit args)
    {
        EnsureHeadStore(uid);
        _npcFaction.AddFaction(uid, RevolutionaryNpcFaction);  // Starlight: Add Revolutionary faction to headrevs (why didn't they have this before?)
    }

    /// <summary>
    /// Returns the nullspace store for a head revolutionary, spawning it if this rule does not have one yet.
    /// </summary>
    public EntityUid EnsureHeadStore(EntityUid head)
    {
        var rules = EntityQueryEnumerator<RevolutionaryRuleComponent>();
        while (rules.MoveNext(out _, out var rule))
        {
            if (rule.HeadStores.TryGetValue(head, out var existing) && Exists(existing))
            {
                AssignHeadStoreOwner(head, existing);
                return existing;
            }

            var store = Spawn("USSPHeadUplinkStore", MapCoordinates.Nullspace);
            rule.HeadStores[head] = store;
            AssignHeadStoreOwner(head, store);
            return store;
        }

        var orphan = Spawn("USSPHeadUplinkStore", MapCoordinates.Nullspace);
        AssignHeadStoreOwner(head, orphan);
        return orphan;
    }

    /// <summary>
    /// Every head revolutionary store currently held by a revolutionary rule.
    /// </summary>
    public IEnumerable<EntityUid> EnumerateHeadStores()
    {
        var rules = EntityQueryEnumerator<RevolutionaryRuleComponent>();
        while (rules.MoveNext(out _, out var rule))
        {
            foreach (var store in rule.HeadStores.Values)
            {
                if (Exists(store))
                    yield return store;
            }
        }
    }

    private void AssignHeadStoreOwner(EntityUid head, EntityUid store)
    {
        if (!TryComp<StoreComponent>(store, out var storeComp))
            return;

        if (storeComp.AccountOwner == null && _mind.TryGetMind(head, out var mindId, out _))
            storeComp.AccountOwner = mindId;
    }

    /// <summary>
    /// The head's existing nullspace store, if a revolutionary rule already created one.
    /// </summary>
    private EntityUid? FindHeadStore(EntityUid head)
    {
        var rules = EntityQueryEnumerator<RevolutionaryRuleComponent>();
        while (rules.MoveNext(out _, out var rule))
        {
            if (rule.HeadStores.TryGetValue(head, out var existing) && Exists(existing))
                return existing;
        }

        return null;
    }

    private void AddConversionToHeadStores()
    {
        var currency = new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> { { "Conversion", FixedPoint2.New(1) } };
        foreach (var store in EnumerateHeadStores())
            _store.TryAddCurrency(currency, store);

        // Currency is applied immediately. The popup waits so it does not stack on the Telebond popup.
        var popups = new List<(EntityUid Head, FixedPoint2 Total)>();
        var heads = EntityQueryEnumerator<HeadRevolutionaryComponent>();
        while (heads.MoveNext(out var head, out _))
        {
            var total = FixedPoint2.Zero;
            if (FindHeadStore(head) is { } store && TryComp<StoreComponent>(store, out var storeComp))
                total = storeComp.Balance.GetValueOrDefault("Conversion", FixedPoint2.Zero);

            popups.Add((head, total));
        }

        Timer.Spawn(TimeSpan.FromSeconds(1), () =>
        {
            foreach (var (head, total) in popups)
            {
                if (!Exists(head))
                    continue;

                _popup.PopupEntity(Loc.GetString("ussp-uplink-conversion", ("total", total)), head, head, PopupType.Medium);
            }
        });
    }
    // STARLIGHT END

    private void OnPostFlash(EntityUid uid, HeadRevolutionaryComponent comp, ref AfterFlashedEvent ev)
    {
        if (uid != ev.User || !ev.Melee)
            return;

        var alwaysConvertible = HasComp<AlwaysRevolutionaryConvertibleComponent>(ev.Target);

        if (!_mind.TryGetMind(ev.Target, out var mindId, out var mind) && !alwaysConvertible)
            return;

        // Starlight Begin
        if (IsAlreadyRevolutionary(ev.Target))
            return;
        // Starlight End

        if (!_whitelistSystem.CheckBoth(ev.Target, comp.Blacklist, alwaysConvertible ? null : comp.Whitelist) || // Starlight-edit: rework all has comp to whitelist & blacklist.
            !_mobState.IsAlive(ev.Target))
        {
            return;
        }

        _npcFaction.AddFaction(ev.Target, RevolutionaryNpcFaction);
        var revComp = EnsureComp<RevolutionaryComponent>(ev.Target);

        // Starlight: Add a component to track which head revolutionary converted this revolutionary
        if (ev.User != null && HasComp<HeadRevolutionaryComponent>(ev.User.Value))
        {
            var converterComp = EnsureComp<RevolutionaryConverterComponent>(ev.Target);
            converterComp.ConverterUid = ev.User.Value;
        }
        // Starlight End

        if (ev.User != null)
        {
            _adminLogManager.Add(LogType.Mind,
                LogImpact.Medium,
                $"{ToPrettyString(ev.User.Value)} converted {ToPrettyString(ev.Target)} into a Revolutionary");

            // STARLIGHT START
            if (HasComp<HeadRevolutionaryComponent>(ev.User.Value))
            {
                var store = EnsureHeadStore(ev.User.Value);
                var currencyToAdd = new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> { { "Telebond", FixedPoint2.New(1) } };
                _store.TryAddCurrency(currencyToAdd, store);

                var finalTelebond = FixedPoint2.Zero;
                if (TryComp<StoreComponent>(store, out var storeComp))
                    finalTelebond = storeComp.Balance.GetValueOrDefault("Telebond", FixedPoint2.Zero);

                var converterName = Identity.Name(ev.User.Value, EntityManager);
                _popup.PopupEntity(Loc.GetString("ussp-uplink-telebond", ("total", finalTelebond)), ev.User.Value, ev.User.Value, PopupType.Medium);

                var notified = new HashSet<EntityUid> { ev.User.Value };
                if (TryComp<HeadRevolutionaryImplantComponent>(ev.User.Value, out var headImplant)
                    && headImplant.ImplantUid is { } implant
                    && TryComp<SubdermalImplantComponent>(implant, out var implanted)
                    && implanted.ImplantedEntity is { } host
                    && notified.Add(host))
                {
                    _popup.PopupEntity(Loc.GetString("ussp-uplink-telebond-total-for", ("total", finalTelebond), ("name", converterName)),
                        host, host, PopupType.Large);
                }

                // Other implants that point at this same store share its balance, so tell their hosts too.
                var remotes = EntityQueryEnumerator<RemoteStoreComponent, USSPUplinkOwnerComponent, SubdermalImplantComponent>();
                while (remotes.MoveNext(out _, out var remote, out var owner, out var subdermal))
                {
                    if (remote.Store != store || owner.OwnerUid != ev.User.Value)
                        continue;

                    if (subdermal.ImplantedEntity is not { } implantedEntity || !notified.Add(implantedEntity))
                        continue;

                    _popup.PopupEntity(Loc.GetString("ussp-uplink-telebond-for", ("name", converterName)),
                        implantedEntity, implantedEntity, PopupType.Medium);
                }

                AddConversionToHeadStores();
            }

            // STARLIGHT END

            if (_mind.TryGetMind(ev.User.Value, out var revMindId, out _))
            {
                if (_role.MindHasRole<RevolutionaryRoleComponent>(revMindId, out var role))
                {
                    role.Value.Comp2.ConvertedCount++;
                    Dirty(role.Value.Owner, role.Value.Comp2);
                }
            }
        }

        if (mindId == default || !_role.MindHasRole<RevolutionaryRoleComponent>(mindId))
        {
            _role.MindAddRole(mindId, "MindRoleRevolutionary");
        }

        if (mind?.UserId != null && _player.TryGetSessionById(mind.UserId.Value, out var session))
            _antag.SendBriefing(session, Loc.GetString("rev-role-greeting", ("name", Identity.Name(ev.Target, EntityManager))), Color.LightYellow, revComp.RevStartSound); // STARLIGHT
    }

    //TODO: Enemies of the revolution
    private void OnCommandMobStateChanged(EntityUid uid, CommandStaffComponent comp, MobStateChangedEvent ev)
    {
        #region Starlight
        if (ev.NewMobState == MobState.Dead || ev.NewMobState == MobState.Invalid)
        {
            // need to pass the RevolutionaryRule comp to CheckCommandLose()
            var query = EntityQueryEnumerator<RevolutionaryRuleComponent, GameRuleComponent>();

            while (query.MoveNext(out var _, out var ruleComp, out var gameRule))
            {
                CheckCommandLose(ruleComp);
            }
        }
        #endregion Starlight
    }

    /// <summary>
    /// Checks if all of command is dead and if so will remove all sec and command jobs if there were any left.
    /// </summary>
    #region Starlight
    private bool CheckCommandLose(RevolutionaryRuleComponent component)
    {
        var commandList = new List<EntityUid>();

        var heads = AllEntityQuery<CommandStaffComponent>();
        while (heads.MoveNext(out var id, out _))
        {
            commandList.Add(id);
        }

        // check if all command are dead
        var allCommandDead = IsGroupDetainedOrDead(commandList, false, true, true);

        // check if there at least one command member alive, uncuffed, and unconverted that is on the station
        var anyCommandOnStation = !IsGroupDetainedOrDead(commandList, true, true, true);

        // grace period - prevents round instantly ending if all of command leave a grid for just 1 tick
        if (anyCommandOnStation)
            _commandLastTimeOnStation = _timing.CurTime;

        // check if command have abandoned the station
        var allCommandAbandonedStation = !anyCommandOnStation && (_timing.CurTime.Subtract(_commandLastTimeOnStation) > component.CommandOffstationGracePeriod);

        // command loses if they're all dead, detained, or they left the station
        return allCommandDead || allCommandAbandonedStation;
    }

    /// <summary>
    /// Removes various event schedulers from the game rules.
    /// </summary>
    private void RemoveEventSchedulers()
    {
        // Remove BasicStationEventScheduler
        var basicSchedulersQuery = EntityQueryEnumerator<BasicStationEventSchedulerComponent>();
        while (basicSchedulersQuery.MoveNext(out var schedulerId, out _))
        {
            RemComp<BasicStationEventSchedulerComponent>(schedulerId);
        }

        // Remove RampingStationEventScheduler
        var rampingSchedulersQuery = EntityQueryEnumerator<RampingStationEventSchedulerComponent>();
        while (rampingSchedulersQuery.MoveNext(out var schedulerId, out _))
        {
            RemComp<RampingStationEventSchedulerComponent>(schedulerId);
        }

        // Get all game rule entities
        // var gameRuleQuery = EntityManager.EntityQuery<GameRuleComponent>();
    }
    #endregion Starlight

    private void OnHeadRevMobStateChanged(EntityUid uid, HeadRevolutionaryComponent comp, MobStateChangedEvent ev)
    {
        if (ev.NewMobState == MobState.Dead || ev.NewMobState == MobState.Invalid)
            CheckRevsLose();
    }

    /// <summary>
    /// Checks if all the Head Revs are dead and if so will deconvert all regular revs.
    /// </summary>
    private bool CheckRevsLose()
    {
        var stunTime = TimeSpan.FromSeconds(4);
        var headRevList = new List<EntityUid>();

        var headRevs = AllEntityQuery<HeadRevolutionaryComponent, MobStateComponent>();
        while (headRevs.MoveNext(out var uid, out _, out _))
        {
            headRevList.Add(uid);
        }

        // If no Head Revs are alive all normal Revs will lose their Rev status and rejoin Nanotrasen
        // Cuffing Head Revs is not enough - they must be killed.
        if (IsGroupDetainedOrDead(headRevList, false, false, false))
        {
            // STARLIGHT: Delete all USSP uplinks and turn supply rifts and SKB implanters to ash
            DeleteUplinksTurnItemsToAsh();

            var rev = AllEntityQuery<RevolutionaryComponent, MindContainerComponent>();
            while (rev.MoveNext(out var uid, out _, out var mc))
            {
                if (HasComp<HeadRevolutionaryComponent>(uid)||HasComp<SiliconLawBoundComponent>(uid)) // Starlight silicons cannot be deconverted
                    continue;

                // Play the deconversion sound for the revolutionary
                _audioSystem.PlayGlobal("/Audio/_Starlight/Misc/rev_end.ogg", Filter.Entities(uid), false, AudioParams.Default.WithVolume(0f));

                _npcFaction.RemoveFaction(uid, RevolutionaryNpcFaction);
                _stun.TryUpdateParalyzeDuration(uid, stunTime);
                RemCompDeferred<RevolutionaryComponent>(uid);
                _popup.PopupEntity(Loc.GetString("rev-break-control", ("name", Identity.Name(uid, EntityManager))), uid); //STARLIGHT
                _adminLogManager.Add(LogType.Mind, LogImpact.Medium, $"{ToPrettyString(uid)} was deconverted due to all Head Revolutionaries dying.");

                if (!_mind.TryGetMind(uid, out var mindId, out var mind, mc))
                    continue;

                // remove their antag role
                _role.MindRemoveRole<RevolutionaryRoleComponent>(mindId);

                // make it very obvious to the rev they've been deconverted since
                // they may not see the popup due to antag and/or new player tunnel vision
                if (_player.TryGetSessionById(mind.UserId, out var session))
                    _euiMan.OpenEui(new DeconvertedEui(), session);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// STARLIGHT: Deletes all USSP uplinks and turns supply rifts and SKB implanters to ash when all head revolutionaries are dead.
    /// </summary>
    private void DeleteUplinksTurnItemsToAsh()
    {
        EntityUid uid = default;
        var rules = EntityQueryEnumerator<RevolutionaryRuleComponent>();
        while (rules.MoveNext(out _, out var rule))
        {
            foreach (var store in rule.HeadStores.Values)
            {
                if (Exists(store))
                    QueueDel(store);
            }

            rule.HeadStores.Clear();
        }

        // Find all supply rifts and collect them for deletion
        var riftsToDelete = new List<(EntityUid Entity, Robust.Shared.Map.EntityCoordinates Coordinates)>();
        var riftQuery = EntityQueryEnumerator<RevSupplyRiftComponent, TransformComponent>();

        while (riftQuery.MoveNext(out var riftId, out _, out var transform))
        {
            riftsToDelete.Add((riftId, transform.Coordinates));
        }

        // Process all supply rifts
        foreach (var (entity, coordinates) in riftsToDelete)
        {
            if (Exists(entity))
            {
                // Spawn ash at the rift's location
                Spawn("Ash", coordinates);

                if (uid == default)
                {
                    var xform = Transform(entity);
                    var station = _stationSystem.GetStationInMap(xform.MapID);
                    if (station != null)
                    {
                        uid = station.Value;
                        _chatSystem.DispatchGlobalAnnouncement(
                            Loc.GetString("centcomm-revs-alldead"),
                            Loc.GetString("central-command-sender"), colorOverride: Color.Yellow);
                        _alert.SetLevel(station.Value, "blue", true, true, true);
                        _roundEnd.SetShuttleCallsEnabled(true);
                    }
                }

                // Delete the rift
                QueueDel(entity);
            }
        }

        // Find all SKB implanters and collect them for deletion
        var implantersToDelete = new List<(EntityUid Entity, Robust.Shared.Map.EntityCoordinates Coordinates)>();
        var implanterQuery = EntityQueryEnumerator<ImplanterComponent, TransformComponent>();

        while (implanterQuery.MoveNext(out var implanterId, out var implanter, out var transform))
        {
            if (implanter.Implant is { } implant && implant == "USSPUplinkImplant")
                implantersToDelete.Add((implanterId, transform.Coordinates));
        }

        // Process all SKB implanters
        foreach (var (entity, coordinates) in implantersToDelete)
        {
            if (Exists(entity))
            {
                // Spawn ash at the implanter's location
                Spawn("Ash", coordinates);

                // Delete the implanter
                QueueDel(entity);
            }
        }
    }

    /// <summary>
    /// Will take a group of entities and check if these entities are alive, dead or cuffed.
    /// </summary>
    /// <param name="list">The list of the entities</param>
    /// <param name="checkOffStation">Bool for if you want to check if someone is in space and consider them missing in action. (Won't check when emergency shuttle arrives just in case)</param>
    /// <param name="countCuffed">Bool for if you don't want to count cuffed entities.</param>
    /// <param name="countRevolutionaries">Bool for if you want to count revolutionaries.</param>
    /// <returns></returns>
    private bool IsGroupDetainedOrDead(List<EntityUid> list, bool checkOffStation, bool countCuffed, bool countRevolutionaries)
    {
        var gone = 0;

        foreach (var entity in list)
        {
            if (TryComp<CuffableComponent>(entity, out var cuffed) && cuffed.CuffedHandCount > 0 && countCuffed)
            {
                gone++;
                continue;
            }

            if (TryComp<MobStateComponent>(entity, out var state))
            {
                if (state.CurrentState == MobState.Dead || state.CurrentState == MobState.Invalid)
                {
                    gone++;
                    continue;
                }

                #region Starlight
                // Starlight: previous 'off station check' straight-up never worked. replaced with a new helper method 'IsOnMainStation'
                if (checkOffStation && !IsOnMainStation(entity) && !_emergencyShuttle.EmergencyShuttleArrived)
                {
                    gone++;
                    continue;
                }
                #endregion Starlight
            }
            //If they don't have the MobStateComponent they might as well be dead.
            else
            {
                gone++;
                continue;
            }

            if ((HasComp<RevolutionaryComponent>(entity) || HasComp<HeadRevolutionaryComponent>(entity)) && countRevolutionaries)
            {
                gone++;
                continue;
            }
        }

        return gone == list.Count || list.Count == 0;
    }

#region Starlight
    /// <summary>
    /// Determines whether an entity is located on a "main" station grid.
    /// Being on an unlaunched escape pod counts as "being on the main station."
    /// Grids like the Arrivals or Cargo Shuttle are not considered "being on the main station."
    /// </summary>
    /// <param name="entity">Entity to be evaluated.</param>
    /// <returns>True if entity is on the main station grid, false otherwise.</returns>
    private bool IsOnMainStation(EntityUid entity)
    {
        // get transform data. used at the end to check if the entity is actually *on* the station grid
        var xform = EntityManager.GetComponentOrNull<TransformComponent>(entity);
        if(xform == null || xform.GridUid == null)
            return false;

        var grid = xform.GridUid.Value;

        // special case: unlaunched (docked) escape pods are still "part of the main station"
        // escape pods lose their EscapePodComponent when they are launched, so this check will fail on launched escape pods
        if (HasComp<EscapePodComponent>(grid))
        {
            return true;
        }

        // check that some station actually owns this entity
        var station = _stationSystem.GetOwningStation(entity);

        if(station is not { } stationUid)
            return false;

        // station data contains all of the station grids
        var stationData = EntityManager.GetComponentOrNull<StationDataComponent>(stationUid);
        if(stationData == null)
            return false;

        // main station grid check. if main grids is (somehow) empty, fallback to any grid in station data
        var grids = stationData.MainGrids.Count > 0
            ? stationData.MainGrids
            : stationData.Grids;

        // finally, check if the entity is on the main grid
        return grids.Contains(grid);
    }
#endregion Starlight

    private static readonly string[] Outcomes =
    {
        // revs survived and heads survived... how
        "rev-reverse-stalemate",
        // revs won and heads died
        "rev-won",
        // revs lost and heads survived
        "rev-lost",
        // revs lost and heads died
        "rev-stalemate"
    };

    #region Starlight
    private static readonly string[] _results =
    {
        "ReverseStalemate",
        "RevsWin",
        "RevsLose",
        "Stalemate"
    };
    #endregion

}
