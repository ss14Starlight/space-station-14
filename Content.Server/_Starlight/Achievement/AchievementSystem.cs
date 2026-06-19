using System.Linq;
using System.Threading.Tasks;
using Content.Server._NullLink.Helpers;
using Content.Server._NullLink.PlayerData;
using Content.Server.AlertLevel;
using Content.Server.Antag;
using Content.Server._FarHorizons.Power.Generation.FissionGenerator;
using Content.Server.Dragon;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.ImmovableRod;
using Content.Server.KillTracking;
using Content.Server.Mind;
using Content.Server.Mining;
using Content.Server.Nuke;
using Content.Server.NukeOps;
using Content.Server.Objectives;
using Content.Server.Objectives.Components;
using Content.Server.Revolutionary.Components;
using Content.Server.Roles;
using Content.Server.Salvage.Expeditions;
using Content.Server.Shuttles.Events;
using Content.Shared._Starlight.Antags.Vampires;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Achievement;
using Content.Shared._Starlight.Chemistry.Events;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.Flash;
using Content.Shared.Follower;
using Content.Shared.Follower.Components;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.MedicalScanner;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NukeOps;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Content.Shared.Projectiles;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Revolutionary.Components;
using Content.Shared.Roles.Jobs;
using Content.Shared.Shuttles.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.Laws;
using Content.Shared.Smoking;
using Content.Shared.Station.Components;
using Content.Shared.Stunnable;
using Content.Shared.Store.Events;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared._Starlight.VentCrawl.Components;
using Content.Shared._Starlight.Revolutionary.Components;
using Content.Shared._Starlight.Store.Events;
using Content.Server._Starlight.Roles;
using Content.Shared._Starlight.Medical;
using Content.Shared._Starlight.Shuttles.Components;

namespace Content.Server._Starlight.Achievement;

public sealed partial class AchievementSystem : EntitySystem
{
    [Dependency] private INullLinkPlayerManager _nullLinkPlayers = default!;
    [Dependency] private IAchievementRewardManager _achievementRewards = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private ObjectivesSystem _objectives = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan _achievementHydrationRetryDelay = TimeSpan.FromSeconds(3);
    private const int HauntedGhostFollowerThreshold = 20;
    private static readonly TimeSpan VentKillWindow = TimeSpan.FromSeconds(30);
    private const float HesDeadJimDamageThreshold = 2000f;
    private const string EthanolReagentId = "Ethanol";
    private const string SalineReagentId = "Saline";
    private const string AvaliSpeciesId = "Avali";
    private const string ResomiSpeciesId = "Resomi";
    private const string UplinkCatEarsListingId = "UplinkCatEars";
    private readonly Dictionary<Guid, Dictionary<string, double>> _roundProgress = [];
    private readonly HashSet<Guid> _achievementFetchInFlight = [];
    private readonly HashSet<EntityUid> _commandStaffMindsThatDied = [];
    private readonly HashSet<EntityUid> _handledReactorMeltdowns = [];
    private readonly Dictionary<EntityUid, TimeSpan> _recentVentCrawlExits = [];
    private bool _firstCrewKillOccurred;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<AfterAntagEntitySelectedEvent>(OnTraitorSelected);
        SubscribeLocalEvent<VampireComponent, VampireBloodDrankEvent>(OnVampireBloodDrank);
        SubscribeLocalEvent<BorgChassisComponent, GotEmaggedEvent>(OnBorgEmagged, after: new[] { typeof(SharedSiliconLawSystem) });
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<RevolutionaryConverterComponent, ComponentStartup>(OnRevolutionaryConverterStartup);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
        SubscribeLocalEvent<WarDeclaredEvent>(OnWarDeclared, after: new[] { typeof(NukeopsRuleSystem) });
        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<NukeExplodedEvent>(OnNukeExploded);
        SubscribeLocalEvent<ActorComponent, RailroadingReagentMetabolizedEvent>(OnActorReagentMetabolized);
        SubscribeLocalEvent<StunbatonComponent, MeleeHitEvent>(OnStunbatonMeleeHit);
        SubscribeLocalEvent<StorePurchaseCompletedEvent>(OnStorePurchaseCompleted);
        SubscribeLocalEvent<SuccessfulInjectEvent>(OnSuccessfulInject);
        SubscribeLocalEvent<KillReportedEvent>(OnKillReported);
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageableChanged);
        SubscribeLocalEvent<NuclearReactorComponent, NuclearReactorMeltdownEvent>(OnNuclearReactorMeltdown);
        SubscribeLocalEvent<BeingVentCrawlComponent, ComponentRemove>(OnBeingVentCrawlRemoved);
        SubscribeLocalEvent<FollowedComponent, EntityStartedFollowingEvent>(OnEntityStartedFollowing);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status == SessionStatus.Disconnected)
                continue;

            QueueAchievementHydration(session);
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    #region Achievement Management
    private bool HasAchievementUnlocked(ICommonSession session, string achievementId)
        => _nullLinkPlayers.HasAchievementUnlocked(session.UserId, achievementId);

    public ValueTask<bool> HasAchievementUnlockedAsync(ICommonSession session, string achievementId)
        => _nullLinkPlayers.HasAchievementUnlockedAsync(session.UserId, achievementId);

    private ValueTask<bool> UnlockAchievement(ICommonSession session, string achievementId, string? characterName = null)
        => _nullLinkPlayers.UnlockAchievement(session.UserId, achievementId, characterName ?? GetCharacterName(session));

    private ValueTask<bool> LockAchievement(ICommonSession session, string achievementId)
        => _nullLinkPlayers.LockAchievement(session.UserId, achievementId);

    public async ValueTask<bool> TryUnlockAchievementAsync(ICommonSession session, string achievementId, string? characterName = null)
    {
        if (await HasAchievementUnlockedAsync(session, achievementId))
            return false;

        var result = await UnlockAchievement(session, achievementId, characterName);
        if (result)
        {
            _achievementRewards.GrantRewards(session, achievementId);
            _nullLinkPlayers.SendAchievementNotification(session.UserId, achievementId);
            _nullLinkPlayers.SendAchievementList(session.UserId);
        }

        return result;
    }

    public async ValueTask<bool> TryLockAchievementAsync(ICommonSession session, string achievementId)
    {
        if (!await HasAchievementUnlockedAsync(session, achievementId))
            return false;

        var result = await LockAchievement(session, achievementId);
        if (result)
            _nullLinkPlayers.SendAchievementList(session.UserId);

        return result;
    }
    #endregion

    #region Progress Management
    public double AddProgress(ICommonSession session, string progressType, double amount = 1)
        => AddProgress(session.UserId, progressType, amount);

    public double AddProgress(Guid userId, string progressType, double amount = 1)
    {
        AddRoundProgress(userId, progressType, amount);
        var value = _nullLinkPlayers.AddAchievementProgress(userId, progressType, amount);
        _nullLinkPlayers.SendAchievementList(userId);
        return value;
    }

    public double AddProgressAndCheck(ICommonSession session, string progressType, double amount = 1)
    {
        var value = AddProgress(session, progressType, amount);
        CheckProgressAchievementsAsync(session, progressType)
            .AsTask()
            .FireAndForget();
        return value;
    }

    public double AddProgressAndCheck(Guid userId, string progressType, double amount = 1)
    {
        var value = AddProgress(userId, progressType, amount);
        CheckProgressAchievementsAsync(userId, progressType)
            .AsTask()
            .FireAndForget();
        return value;
    }

    public double AddProgressAndCheck(EntityUid uid, string progressType, double amount = 1)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session))
            return 0;

        return AddProgressAndCheck(session, progressType, amount);
    }

    public double AddProgress(EntityUid uid, string progressType, double amount = 1)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session))
            return 0;

        return AddProgress(session, progressType, amount);
    }

    public double GetProgress(ICommonSession session, string progressType)
        => GetProgress(session.UserId, progressType);

    public double GetProgress(Guid userId, string progressType)
        => _nullLinkPlayers.GetCachedAchievementProgress(userId, progressType);

    public void ResetProgress(ICommonSession session, string? progressType = null)
        => ResetProgress(session.UserId, progressType);

    public void ResetProgress(Guid userId, string? progressType = null)
    {
        _nullLinkPlayers.ResetAchievementProgress(userId, progressType);
        _nullLinkPlayers.SendAchievementList(userId);
    }

    public async ValueTask<bool> TryUnlockAtProgressAsync(ICommonSession session, string achievementId, string progressType, double requiredProgress, string? characterName = null)
        => GetProgress(session, progressType) >= requiredProgress
        && await TryUnlockAchievementAsync(session, achievementId, characterName);

    public void CheckProgressAchievements(ICommonSession session, string progressType, string? characterName = null)
        => CheckProgressAchievementsAsync(session, progressType, characterName)
            .AsTask()
            .FireAndForget();

    public async ValueTask CheckProgressAchievementsAsync(ICommonSession session, string progressType, string? characterName = null)
    {
        foreach (var achievement in _prototypeManager.EnumeratePrototypes<AchievementPrototype>())
        {
            if (!achievement.IsRelevantForProgress(progressType)
                || !achievement.AreRequirementsMet((type, perRound) => perRound
                    ? GetRoundProgress(session.UserId, type)
                    : GetProgress(session, type)))
                continue;

            await TryUnlockAchievementAsync(session, achievement.ID, characterName);
        }
    }

    public void CheckProgressAchievements(Guid userId, string progressType, string? characterName = null)
        => CheckProgressAchievementsAsync(userId, progressType, characterName)
            .AsTask()
            .FireAndForget();

    public async ValueTask CheckProgressAchievementsAsync(Guid userId, string progressType, string? characterName = null)
    {
        if (!_playerManager.TryGetSessionById(new NetUserId(userId), out var session))
            return;

        await CheckProgressAchievementsAsync(session, progressType, characterName);
    }

    public async ValueTask<bool> TryUnlockAtProgressAsync(Guid userId, string achievementId, string progressType, double requiredProgress, string? characterName = null)
    {
        if (GetProgress(userId, progressType) < requiredProgress)
            return false;

        if (!_playerManager.TryGetSessionById(new NetUserId(userId), out var session))
            return false;

        return await TryUnlockAchievementAsync(session, achievementId, characterName);
    }
    #endregion

    #region Event Handlers
    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        switch (e.NewStatus)
        {
            case SessionStatus.Connected:
                QueueAchievementHydration(e.Session);
                break;
            case SessionStatus.InGame:
                if (_nullLinkPlayers.TryGetPlayerData(e.Session.UserId, out var playerData)
                    && playerData.AchievementCacheHydrated)
                {
                    _nullLinkPlayers.SendAchievementList(e.Session.UserId);
                }
                else
                {
                    QueueAchievementHydration(e.Session);
                }
                break;
            case SessionStatus.Disconnected:
                _achievementFetchInFlight.Remove(e.Session.UserId);
                break;
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        AddProgress(ev.Player, AchievementProgressKeys.SpawnCount);
        AddProgress(ev.Player, ev.LateJoin ? AchievementProgressKeys.SpawnLateJoinCount : AchievementProgressKeys.SpawnRoundStartCount);

        CheckProgressAchievements(ev.Player, AchievementProgressKeys.SpawnCount);
        CheckProgressAchievements(ev.Player, ev.LateJoin ? AchievementProgressKeys.SpawnLateJoinCount : AchievementProgressKeys.SpawnRoundStartCount);

        if (!string.IsNullOrEmpty(ev.JobId))
        {
            var progressType = AchievementProgressKeys.SpawnJob(ev.JobId);
            AddProgress(ev.Player, progressType);
            CheckProgressAchievements(ev.Player, progressType);

            if (ev.JobId is "DeathSquad" or "Decimus")
                QueueUnlockAchievement(ev.Player, "remember_no_galcom");
        }
    }

    private void OnTraitorSelected(ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Session == null || !HasComp<TraitorRuleComponent>(args.GameRule.Owner))
            return;

        QueueUnlockAchievement(args.Session, "whiskey_echo_whiskey");
    }

    private void OnVampireBloodDrank(EntityUid uid, VampireComponent _, VampireBloodDrankEvent ev)
        => AddProgressAndCheck(uid, AchievementProgressKeys.VampireBloodDrank, ev.Amount);

    private void OnBorgEmagged(EntityUid uid, BorgChassisComponent _, ref GotEmaggedEvent args)
    {
        if (!args.Handled
            || args.EmagComponent == null
            || (args.Type & EmagType.Interaction) != EmagType.Interaction
            || args.EmagComponent.Lawset != null)
            return;

        QueueUnlockAchievement(args.UserUid, "assuming_direct_control");
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState >= args.NewMobState)
            return;

        if (HasComp<DragonComponent>(args.Target)
            && args.Origin is { } origin)
        {
            var session = ResolvePlayerSessionFromOrigin(origin);
            if (session != null)
            {
                TryUnlockAchievementAsync(session, "dragon_slayer")
                    .AsTask()
                    .FireAndForget();
            }
        }

        if (HasComp<CommandStaffComponent>(args.Target)
            && _mind.TryGetMind(args.Target, out var mindId, out var _))
        {
            _commandStaffMindsThatDied.Add(mindId);
        }
    }

    private void OnStartCollide(ref StartCollideEvent args)
    {
        if (!HasComp<ActorComponent>(args.OtherEntity))
            return;

        if (HasComp<MeteorComponent>(args.OurEntity))
            QueueUnlockAchievement(args.OtherEntity, "homing_meteors");

        if (HasComp<ImmovableRodComponent>(args.OurEntity))
            QueueUnlockAchievement(args.OtherEntity, "should_have_braced");
    }

    private void OnRevolutionaryConverterStartup(EntityUid uid, RevolutionaryConverterComponent _, ComponentStartup args)
    {
        if (!HasComp<RevolutionaryComponent>(uid)
            || !TryGetJobId(uid, out var jobId)
            || jobId != "Quartermaster")
        {
            return;
        }

        QueueUnlockAchievement(uid, "viva_cargonia");
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent ev)
    {
        if (ev.OldAlertLevel == ev.AlertLevel)
            return;

        if (ev.AlertLevel == "epsilon")
            QueueUnlockAchievementForCrew("contract_terminated", station: ev.Station);
    }

    private void OnWarDeclared(ref WarDeclaredEvent ev)
    {
        if (ev.Status != WarConditionStatus.WarReady)
            return;

        QueueUnlockAchievementForCrew("special_circumstances");
    }

    private void OnFTLStarted(ref FTLStartedEvent ev)
    {
        if (ev.FromMapUid is not { } mapUid || !HasComp<SalvageExpeditionComponent>(mapUid))
            return;

        var shuttleQuery = EntityQueryEnumerator<ShuttleComponent, TransformComponent>();

        while (shuttleQuery.MoveNext(out _, out var shuttleXform))
        {
            if (shuttleXform.MapUid == mapUid)
                return;
        }

        var strandedQuery = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();

        while (strandedQuery.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (xform.MapUid != mapUid || _mobState.IsDead(uid, mobState))
                continue;

            QueueUnlockAchievement(uid, "stranded");
        }
    }

    private void OnNukeExploded(NukeExplodedEvent ev)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } uid)
                continue;

            if (!TryComp<MobStateComponent>(uid, out var mobState)
                || mobState.CurrentState == MobState.Dead)
                continue;

            if (ev.OwningStation != null && Transform(uid).GridUid != ev.OwningStation)
                continue;

            if (!_inventory.TryGetSlotEntity(uid, "mask", out var maskItem)
                || !TryComp<SmokableComponent>(maskItem, out var smokable)
                || smokable.State != SmokableState.Lit)
                continue;

            TryUnlockAchievementAsync(session, "oppenheimer")
                .AsTask()
                .FireAndForget();
        }
    }

    private void OnActorReagentMetabolized(EntityUid uid, ActorComponent _, ref RailroadingReagentMetabolizedEvent args)
    {
        if (args.Reagent.Reagent.Prototype == EthanolReagentId)
        {
            AddProgressAndCheck(uid, AchievementProgressKeys.AlcoholDrank, args.Reagent.Quantity.Float());
        }

        if (args.Reagent.Reagent.Prototype != "Frezon")
            return;

        QueueUnlockAchievement(uid, "that_frezon_stare");
    }

    private void OnStunbatonMeleeHit(Entity<StunbatonComponent> entity, ref MeleeHitEvent args)
    {
        if (!args.IsHit
            || !args.HitEntities.Any(target => HasComp<MobStateComponent>(target))
            || !IsCrewEntity(args.User, requiredDepartmentId: "Security")
            || TryGetStunbatonCharge(entity, out var charge) && charge >= entity.Comp.EnergyPerUse)
        {
            return;
        }

        QueueUnlockAchievement(args.User, "how_to_charge");
    }

    private void OnStorePurchaseCompleted(ref StorePurchaseCompletedEvent args)
    {
        if (args.ListingId != UplinkCatEarsListingId
            || !_playerManager.TryGetSessionByEntity(args.Buyer, out var session))
        {
            return;
        }

        AddRoundProgress(session.UserId, AchievementProgressKeys.StorePurchase(args.ListingId), 1);
    }

    private void OnSuccessfulInject(SuccessfulInjectEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(args.TargetGettingInjected, out var humanoid)
            || (humanoid.Species != AvaliSpeciesId && humanoid.Species != ResomiSpeciesId))
            return;

        if (!args.TransferredSolution.Contents.Any(reagentQuantity => reagentQuantity.Reagent.Prototype == SalineReagentId))
            return;

        QueueUnlockAchievement(args.EntityUsingInjector, "you_monster");
    }

    private void OnProjectileHit(EntityUid uid, ProjectileComponent _, ref ProjectileHitEvent args)
    {
        if (_tag.HasTag(uid, "Arrow")
            && ResolvePlayerSessionFromParentChain(args.Target) is { } arrowSession)
        {
            QueueUnlockAchievement(arrowSession, "took_an_arrow_to_the_knee");
        }
    }

    private void OnKillReported(ref KillReportedEvent ev)
    {
        if (ev.Primary is not KillPlayerSource playerSource
            || !_playerManager.TryGetSessionById(playerSource.PlayerId, out var killerSession)
            || killerSession.AttachedEntity is not { } killerUid)
        {
            return;
        }

        if (killerUid != ev.Entity
            && _recentVentCrawlExits.TryGetValue(killerUid, out var lastVentExit)
            && _timing.CurTime - lastVentExit <= VentKillWindow
            && IsCrewEntity(ev.Entity))
        {
            QueueUnlockAchievement(killerSession, "sus");
        }

        if (!_firstCrewKillOccurred
            && killerUid != ev.Entity
            && TryGetOwningStation(ev.Entity, out var victimStation)
            && IsEligibleCrewSession(killerSession, victimStation)
            && IsCrewEntity(ev.Entity, victimStation))
        {
            _firstCrewKillOccurred = true;
            QueueUnlockAchievement(killerSession, "first_blood");
        }

        if (!_mind.TryGetMind(ev.Entity, out var victimMindId, out _))
            return;

        if (TryGetJobId(killerSession, out var killerJobId)
            && killerJobId is "SalvageSpecialist" or "SalvageLead"
            && _roles.MindHasRole<NukeopsRoleComponent>(victimMindId))
        {
            QueueUnlockAchievement(killerSession, "the_robust_salvagers");
        }

        if (_roles.MindHasRole<TerminatorRoleComponent>(victimMindId))
            QueueUnlockAchievement(killerSession, "john_connor");

        if (!_roles.MindHasRole<ParadoxCloneRoleComponent>(victimMindId)
            || !_mind.TryGetMind(killerSession.UserId, out var killerMindId, out _)
            || !TryComp<TargetOverrideComponent>(ev.Entity, out var targetOverride)
            || targetOverride.Target != killerMindId)
        {
            return;
        }

        QueueUnlockAchievement(killerSession, "paradox_undone");
    }

    private void OnDamageableChanged(EntityUid uid, DamageableComponent damageable, ref DamageChangedEvent args)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session)
            || damageable.TotalDamage.Float() < HesDeadJimDamageThreshold
            || !HasRequiredDamageGroups(damageable))
        {
            return;
        }

        QueueUnlockAchievement(session, "hes_dead_jim");
    }

    private void OnNuclearReactorMeltdown(EntityUid uid, NuclearReactorComponent _, NuclearReactorMeltdownEvent args)
    {
        if (!_handledReactorMeltdowns.Add(uid))
            return;

        if (args.Station is not { } station)
            return;

        QueueUnlockAchievementForCrew("graphite_fire", station);
    }

    private void OnEntityStartedFollowing(Entity<FollowedComponent> ent, ref EntityStartedFollowingEvent args)
    {
        var ghostFollowers = ent.Comp.Following.Count(follower => HasComp<GhostComponent>(follower));
        if (ghostFollowers >= HauntedGhostFollowerThreshold)
            QueueUnlockAchievement(ent.Owner, "haunted");
    }

    private void OnBeingVentCrawlRemoved(EntityUid uid, BeingVentCrawlComponent component, ComponentRemove args)
    {
        if (_playerManager.TryGetSessionByEntity(uid, out _))
            _recentVentCrawlExits[uid] = _timing.CurTime;
    }

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (!_mind.TryGetMind(session.UserId, out Entity<MindComponent>? mindEnt)
                || mindEnt is not { } mind
                || !_roles.MindHasRole<TraitorRoleComponent>(mind.Owner)
                || GetRoundProgress(session.UserId, AchievementProgressKeys.StorePurchase(UplinkCatEarsListingId)) <= 0
                || !DidMindGreentext(mind))
            {
                continue;
            }

            QueueUnlockAchievement(session, "syndie_cat", mind.Comp.CharacterName);
        }

        var currentCommandStaffMinds = GetCurrentCommandStaffMinds();
        if (currentCommandStaffMinds.Count == 0)
            return;

        var anyCommandStaffDied = currentCommandStaffMinds.Any(_commandStaffMindsThatDied.Contains);
        var allCommandStaffDied = currentCommandStaffMinds.All(_commandStaffMindsThatDied.Contains);

        if (!anyCommandStaffDied && !allCommandStaffDied)
        {
            foreach (var session in _playerManager.Sessions)
            {
                if (TryGetJobId(session, out var jobId) && jobId == "BlueShield")
                    QueueUnlockAchievement(session, "guard_dog");
            }

            return;
        }

        if (!allCommandStaffDied)
            return;

        foreach (var session in _playerManager.Sessions)
        {
            if (TryGetJobId(session, out var jobId) && jobId == "BlueShield")
                QueueUnlockAchievement(session, "bad_dog");
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _roundProgress.Clear();
        _commandStaffMindsThatDied.Clear();
        _handledReactorMeltdowns.Clear();
        _recentVentCrawlExits.Clear();
        _firstCrewKillOccurred = false;
    }
    #endregion

    #region Round Progress
    private double AddRoundProgress(Guid userId, string progressType, double amount)
    {
        if (!_roundProgress.TryGetValue(userId, out var progress))
            _roundProgress[userId] = progress = [];

        progress.TryGetValue(progressType, out var current);
        return progress[progressType] = current + amount;
    }

    public double GetRoundProgress(Guid userId, string progressType)
    {
        if (_roundProgress.TryGetValue(userId, out var progress)
            && progress.TryGetValue(progressType, out var value))
            return value;

        return 0;
    }
    #endregion

    #region Helpers

    private void QueueUnlockAchievement(ICommonSession session, string achievementId, string? characterName = null)
    {
        TryUnlockAchievementAsync(session, achievementId, characterName)
            .AsTask()
            .FireAndForget();
    }

    public void QueueUnlockAchievement(EntityUid uid, string achievementId)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session))
            return;

        QueueUnlockAchievement(session, achievementId);
    }

    private void QueueUnlockAchievementForCrew(string achievementId, EntityUid? station = null, string? requiredDepartmentId = null)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (!IsEligibleCrewSession(session, station, requiredDepartmentId))
                continue;

            QueueUnlockAchievement(session, achievementId);
        }
    }

    public void QueueUnlockAchievementForJobs(string achievementId, EntityUid? station = null, params string[] requiredJobIds)
    {
        if (requiredJobIds.Length == 0)
            return;

        var requiredJobs = requiredJobIds.ToHashSet();

        foreach (var session in _playerManager.Sessions)
        {
            if (!IsEligibleCrewSession(session, station))
                continue;

            if (!TryGetJobId(session, out var jobId) || !requiredJobs.Contains(jobId))
                continue;

            QueueUnlockAchievement(session, achievementId);
        }
    }

    private string GetCharacterName(ICommonSession session)
    {
        if (session.AttachedEntity is { } attached && TryComp<MetaDataComponent>(attached, out var meta))
            return meta.EntityName;

        return session.Name;
    }

    private ICommonSession? ResolvePlayerSessionFromOrigin(EntityUid origin)
    {
        if (_playerManager.TryGetSessionByEntity(origin, out var session))
            return session;

        if (TryComp<ProjectileComponent>(origin, out var projectile)
            && projectile.Shooter is { } shooter
            && ResolvePlayerSessionFromParentChain(shooter) is { } shooterSession)
        {
            return shooterSession;
        }

        return ResolvePlayerSessionFromParentChain(origin);
    }

    private ICommonSession? ResolvePlayerSessionFromParentChain(EntityUid uid)
    {
        if (_playerManager.TryGetSessionByEntity(uid, out var session))
            return session;

        var current = uid;

        for (var depth = 0; depth < 4; depth++)
        {
            if (Transform(current).ParentUid is not { Valid: true } parent || parent == current)
                return null;

            if (_playerManager.TryGetSessionByEntity(parent, out session))
                return session;

            current = parent;
        }

        return null;
    }

    private bool IsEligibleCrewSession(ICommonSession session, EntityUid? station = null, string? requiredDepartmentId = null)
    {
        if (session.AttachedEntity is not { } uid)
            return false;

        if (!TryComp<MobStateComponent>(uid, out var mobState)
            || mobState.CurrentState == MobState.Dead)
        {
            return false;
        }

        if (station is { } stationUid && !IsOnStation(uid, stationUid))
            return false;

        return IsCrewEntity(uid, station, requiredDepartmentId);
    }

    private bool TryGetJobId(EntityUid uid, out string jobId)
    {
        jobId = string.Empty;

        if (!_mind.TryGetMind(uid, out var mindId, out _)
            || !_jobs.MindTryGetJob(mindId, out var job))
        {
            return false;
        }

        jobId = job.ID;
        return true;
    }

    private bool TryGetJobId(ICommonSession session, out string jobId)
    {
        jobId = string.Empty;

        if (!_mind.TryGetMind(session.UserId, out var mindId, out _)
            || !_jobs.MindTryGetJob(mindId, out var job))
        {
            return false;
        }

        jobId = job.ID;
        return true;
    }

    private bool IsCrewEntity(EntityUid uid, EntityUid? station = null, string? requiredDepartmentId = null)
    {
        if (station is { } stationUid && !IsOnStation(uid, stationUid))
            return false;

        if (HasComp<NukeOperativeComponent>(uid))
            return false;

        return TryGetJobId(uid, out var jobId) && IsCrewJob(jobId, requiredDepartmentId);
    }

    private bool IsCrewJob(string jobId, string? requiredDepartmentId = null)
    {
        if (!_prototypeManager.TryIndex<JobPrototype>(jobId, out var job)
            || !job.SetPreference)
        {
            return false;
        }

        return requiredDepartmentId == null || JobHasDepartment(job.ID, requiredDepartmentId);
    }

    private bool HasRequiredDamageGroups(DamageableComponent damageable)
    {
        foreach (var groupId in HealthAnalyzerFormatting.DamageGroupOrder)
        {
            if (!damageable.DamagePerGroup.TryGetValue(groupId, out var damage)
                || damage.Float() <= 0f)
            {
                return false;
            }
        }

        return true;
    }

    private bool DidMindGreentext(Entity<MindComponent> mind)
    {
        if (mind.Comp.Objectives.Count == 0)
            return false;

        foreach (var objective in mind.Comp.Objectives)
        {
            if (!_objectives.IsCompleted(objective, mind))
                return false;
        }

        return true;
    }

    private bool JobHasDepartment(string jobId, string departmentId)
    {
        if (!_jobs.TryGetAllDepartments(jobId, out var departments))
            return false;

        foreach (var department in departments)
        {
            if (department.ID == departmentId)
                return true;
        }

        return false;
    }

    private HashSet<EntityUid> GetCurrentCommandStaffMinds()
    {
        var commandStaffMinds = new HashSet<EntityUid>();
        var commandStaffQuery = EntityQueryEnumerator<CommandStaffComponent>();

        while (commandStaffQuery.MoveNext(out var uid, out _))
        {
            if (_mind.TryGetMind(uid, out var mindId, out _))
                commandStaffMinds.Add(mindId);
        }

        return commandStaffMinds;
    }

    private bool TryGetStunbatonCharge(Entity<StunbatonComponent> baton, out float charge)
    {
        charge = 0f;

        if (TryComp<BatteryComponent>(baton.Owner, out var battery))
        {
            charge = _battery.GetCharge((baton.Owner, battery));
            return true;
        }

        if (!_powerCell.TryGetBatteryFromSlot(baton.Owner, out var batteryEnt))
            return false;

        charge = _battery.GetCharge(batteryEnt.Value.AsNullable());
        return true;
    }

    private bool IsOnStation(EntityUid uid, EntityUid station)
        => TryGetOwningStation(uid, out var owningStation) && owningStation == station;

    private bool TryGetOwningStation(EntityUid uid, out EntityUid station)
    {
        station = default;

        if (Transform(uid).GridUid is not { } gridUid
            || !TryComp<StationMemberComponent>(gridUid, out var stationMember))
        {
            return false;
        }

        station = stationMember.Station;
        return true;
    }

    private void QueueAchievementHydration(ICommonSession session)
    {
        if (session.Status == SessionStatus.Disconnected)
            return;

        if (_nullLinkPlayers.TryGetPlayerData(session.UserId, out var playerData)
            && playerData.AchievementCacheHydrated)
        {
            _nullLinkPlayers.SendAchievementList(session.UserId);
            return;
        }

        if (!_achievementFetchInFlight.Add(session.UserId))
            return;

        HydrateAchievementsAsync(session.UserId)
            .FireAndForget();
    }

    private async Task HydrateAchievementsAsync(Guid userId)
    {
        try
        {
            await _nullLinkPlayers.GetUnlockedAchievements(userId);
        }
        finally
        {
            _achievementFetchInFlight.Remove(userId);
        }

        if (!_nullLinkPlayers.TryGetPlayerData(userId, out var playerData))
            return;

        if (playerData.Session.Status == SessionStatus.Disconnected)
            return;

        if (playerData.AchievementCacheHydrated)
        {
            _nullLinkPlayers.SendAchievementList(userId);
            return;
        }

        Timer.Spawn(_achievementHydrationRetryDelay, () =>
        {
            if (!_playerManager.TryGetSessionById(new NetUserId(userId), out var retrySession))
                return;

            if (retrySession.Status == SessionStatus.Disconnected)
                return;

            QueueAchievementHydration(retrySession);
        });
    }
    #endregion
}
