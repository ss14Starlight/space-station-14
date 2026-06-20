using Content.Server._Starlight.CosmicCult.Components;
using Content.Server.Actions;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.Atmos.Components;
using Content.Server.Audio;
using Content.Server.Bible.Components;
using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Popups;
using Content.Server.Revolutionary;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Server.Voting.Managers;
using Content.Server.Voting;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.CosmicCult.Components.Examine;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.CosmicCult.Prototypes;
using Content.Shared._Starlight.CosmicCult;
using Content.Shared._Starlight.CosmicCult.Roles;
using Content.Shared.Alert;
using Content.Shared.Audio;
using Content.Shared.Coordinates;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Revolutionary.Components;
using Content.Shared.Stunnable;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Collections.Immutable;
using System.Linq;
using Content.Shared.Gibbing;
using Content.Shared.Light.Components;
using Content.Server._Starlight.Language;
using Content.Shared._Starlight.Language;
using Content.Server.Weather;
using Content.Shared.Shuttles.Components;
using Content.Shared.Radio.Components;
using Content.Shared.Mind.Components;
using Content.Shared._Starlight.Shadekin.Components;

namespace Content.Server._Starlight.CosmicCult;

/// <summary>
/// Where all the main stuff for Cosmic Cultists happens.
/// </summary>
public sealed partial class CosmicCultRuleSystem : GameRuleSystem<CosmicCultRuleComponent>
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private EmergencyShuttleSystem _emergency = default!;
    [Dependency] private EuiManager _euiMan = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _playerMan = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IRobustRandom _rand = default!;
    [Dependency] private IVoteManager _votes = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private MonumentSystem _monument = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private ServerGlobalSoundSystem _sound = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private VisibilitySystem _visibility = default!;
    [Dependency] private LanguageSystem _languageSystem = default!;
    [Dependency] private WeatherSystem _weather = default!;

    private ISawmill _sawmill = default!;
    private TimeSpan _t3RevealDelay = default!;
    private TimeSpan _t2RevealDelay = default!;
    private TimeSpan _finaleDelay = default!;
    private TimeSpan _voteDelay = default!;
    private TimeSpan _voteTimer = default!;

    private readonly SoundSpecifier _briefingSound = new SoundPathSpecifier("/Audio/_Starlight/CosmicCult/antag_cosmic_briefing.ogg");
    private readonly SoundSpecifier _deconvertSound = new SoundPathSpecifier("/Audio/_Starlight/CosmicCult/antag_cosmic_deconvert.ogg");
    private readonly SoundSpecifier _tier3Sound = new SoundPathSpecifier("/Audio/_Starlight/CosmicCult/tier3.ogg");
    private readonly SoundSpecifier _tier2Sound = new SoundPathSpecifier("/Audio/_Starlight/CosmicCult/tier2.ogg");
    private readonly SoundSpecifier _monumentAlert = new SoundPathSpecifier("/Audio/_Starlight/CosmicCult/tier_up.ogg");

    private readonly ProtoId<LanguagePrototype> _cultLanguage = "Cosmic";

    /// <summary>
    /// Mind role to add to cultists.
    /// </summary>
    public static readonly EntProtoId MindRole = "MindRoleCosmicCult";

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("cosmiccult");

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<CosmicCultAssociateRuleEvent>(OnAssociateRule);

        SubscribeLocalEvent<CosmicCultRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelect);

        SubscribeLocalEvent<CosmicCultComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<CosmicGodComponent, ComponentInit>(OnGodSpawn);
        SubscribeLocalEvent<CosmicCultComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CosmicCultLeadComponent, MindRemovedMessage>(HandleMindRemoved);

        Subs.CVar(_config,
            StarlightCCVars.CosmicCultT2RevealDelaySeconds,
            value => _t2RevealDelay = TimeSpan.FromSeconds(value),
            true);
        Subs.CVar(_config,
            StarlightCCVars.CosmicCultT3RevealDelaySeconds,
            value => _t3RevealDelay = TimeSpan.FromSeconds(value),
            true);
        Subs.CVar(_config,
            StarlightCCVars.CosmicCultFinaleDelaySeconds,
            value => _finaleDelay = TimeSpan.FromSeconds(value),
            true);
        Subs.CVar(_config,
            StarlightCCVars.CosmicCultStewardVoteTimer,
            value => _voteTimer = TimeSpan.FromSeconds(value),
            true);
        Subs.CVar(_config,
            StarlightCCVars.CosmicCultStewardVoteDelayTimer,
            value => _voteDelay = TimeSpan.FromSeconds(value),
            true);
    }

    #region Starting Events
    protected override void Started(EntityUid uid, CosmicCultRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
        => component.StewardVoteTimer = _timing.CurTime + _voteDelay;

    protected override void ActiveTick(EntityUid uid, CosmicCultRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        if (component.StewardVoteTimer is { } voteTimer && _timing.CurTime >= voteTimer)
        {
            component.StewardVoteTimer = null;
            StewardVote();
        }
        if (component.ExtraRiftTimer is { } riftTimer && _timing.CurTime >= riftTimer && !component.RiftStop)
        {
            component.ExtraRiftTimer = _timing.CurTime + _rand.Next(TimeSpan.FromSeconds(230), TimeSpan.FromSeconds(360)); //3min50 to 6min between new rifts. Seconds instead of minutes for granularity.
            SpawnRift();
        }
        if (component.PrepareFinaleTimer is { } finalePrepTimer && _timing.CurTime >= finalePrepTimer)
        {
            component.PrepareFinaleTimer = null;

            if (TryComp<CosmicFinaleComponent>(component.MonumentInGame, out var finaleComp))
            {
                _monument.ReadyFinale(component.MonumentInGame, finaleComp);
                UpdateCultData(component.MonumentInGame); //duplicated work but it looks nicer than calling updateAppearance on it's own
                return;
            }
        }
        if (component.Tier3DelayTimer is { } tier3Timer && _timing.CurTime >= tier3Timer)
        {
            component.Tier3DelayTimer = null;
            component.ExtraRiftTimer = null; // stop spawning more rifts

            //do spooky things
            var query = EntityQueryEnumerator<CosmicCultComponent>();
            while (query.MoveNext(out var cultist, out var cultComp))
            {
                EnsureComp<CosmicStarMarkComponent>(cultist);
            }

            var sender = Loc.GetString("cosmiccult-announcement-sender");
            var mapData = _map.GetMap(_transform.GetMapId(component.MonumentInGame.Owner.ToCoordinates()));
            _chatSystem.DispatchStationAnnouncement(component.MonumentInGame, Loc.GetString("cosmiccult-announce-tier3-progress"), sender, false, null, Color.FromHex("#4cabb3"));
            _chatSystem.DispatchStationAnnouncement(component.MonumentInGame, Loc.GetString("cosmiccult-announce-tier3-warning"), null, false, null, Color.FromHex("#cae8e8"));
            _audio.PlayGlobal(_tier3Sound, Filter.Broadcast(), false, AudioParams.Default);

            _weather.TryAddWeather(mapData, "WeatherCosmic", out _);

            // EnsureComp<ParallaxComponent>(mapData, out var parallax);
            // parallax.Parallax = "CosmicFinaleParallax";
            // Dirty(mapData, parallax);

            EnsureComp<MapLightComponent>(mapData, out var mapLight);
            mapLight.AmbientLightColor = Color.FromHex("#210746");
            Dirty(mapData, mapLight);

            EnsureComp<PreventFTLComponent>(mapData); // This will prevent all Shuttles to exist the station map... OH BOY...

            var lights = EntityQueryEnumerator<PoweredLightComponent>();
            while (lights.MoveNext(out var light, out _))
            {
                if (!_rand.Prob(0.25f))
                    continue;
                _ghost.DoGhostBooEvent(light);
            }

            var collideQuery = EntityQueryEnumerator<MonumentCollisionComponent>();
            while (collideQuery.MoveNext(out var collideEnt, out _))
            {
                RemComp<MonumentCollisionComponent>(collideEnt);
            }

            if (TryComp<VisibilityComponent>(component.MonumentInGame, out var visComp))
                _visibility.SetLayer((component.MonumentInGame, visComp), 1);

            component.MonumentSlowZone = Spawn("MonumentSlowZone", Transform(component.MonumentInGame).Coordinates); // spawn The Monument's slowing fixture entity that supresses non-cult / non-mindshielded / non-chaplain crew.
            MonumentSystem.SetCanTierUp(component.MonumentInGame, true);
            UpdateCultData(component.MonumentInGame); //instantly go up a tier if they manage it.
            _ui.SetUiState(component.MonumentInGame.Owner, MonumentKey.Key, new MonumentBuiState(component.MonumentInGame.Comp)); //not sure if this is needed but I'll be safe
        }
        if (component.Tier2DelayTimer is { } tier2Timer && _timing.CurTime >= tier2Timer)
        {
            component.Tier2DelayTimer = null;
            component.ExtraRiftTimer = _timing.CurTime + TimeSpan.FromSeconds(15);

            //do spooky effects
            var mobquery = EntityQueryEnumerator<MobStateComponent>();
            while (mobquery.MoveNext(out var ent, out var _))
                _popup.PopupEntity(Loc.GetString("cosmiccult-announce-tier2-progress"), ent, ent, PopupType.LargeCaution);

            _audio.PlayGlobal(_tier2Sound, Filter.Broadcast(), false, AudioParams.Default);

            for (var i = 0; i < Convert.ToInt16(component.TotalCrew / 6); i++) // spawn # malign rifts equal to 16.67% of the playercount
            {
                SpawnRift();
            }

            var lights = EntityQueryEnumerator<PoweredLightComponent>();
            while (lights.MoveNext(out var light, out _))
            {
                if (!_rand.Prob(0.50f))
                    continue;
                _ghost.DoGhostBooEvent(light);
            }

            MonumentSystem.SetCanTierUp(component.MonumentInGame, true);
            UpdateCultData(component.MonumentInGame); //instantly go up a tier if they manage it
            _ui.SetUiState(component.MonumentInGame.Owner, MonumentKey.Key, new MonumentBuiState(component.MonumentInGame.Comp)); //not sure if this is needed but I'll be safe
        }
    }

    private void StewardVote()
    {
        var ruleQuery = QueryActiveRules();
        var foundRule = false;

        while (ruleQuery.MoveNext(out var ruleUid, out var activeRuleComp, out var rule, out var gameRule))
        {
            foundRule = true;

            // Once the Monument is placed there are no more votes
            if (rule.CurrentTier > 0)
                return;

            break;
        }

        if (!foundRule)
            return;

        var cultists = new List<(string, EntityUid)>();
        var cultQuery = EntityQueryEnumerator<CosmicCultComponent, MetaDataComponent>();

        while (cultQuery.MoveNext(out var cultist, out _, out var metadata))
        {
            if (!IsValidStewardCandidate(cultist))
                continue;

            cultists.Add((metadata.EntityName, cultist));
        }

        if (cultists.Count == 0)
        {
            _sawmill.Info("Steward vote started, but found no valid living non-leader cultist candidates.");
            return;
        }

        _sawmill.Info("Starting steward vote with {0} valid candidate(s).", cultists.Count);

        var options = new VoteOptions
        {
            Title = Loc.GetString("cosmiccult-vote-steward-title"),
            InitiatorText = Loc.GetString("cosmiccult-vote-steward-initiator"),
            Duration = _voteTimer,
            VoterEligibility = VoteManager.VoterEligibility.CosmicCult
        };

        foreach (var (name, ent) in cultists)
        {
            options.Options.Add((name, ent));
        }

        var vote = _votes.CreateVote(options);

        vote.OnFinished += (voteHandle, args) =>
        {
            if (args.Winner == null && !args.Winners.Any())
                return;

            var picked = args.Winner == null
                ? (EntityUid) _rand.Pick(args.Winners)
                : (EntityUid) args.Winner;

            if (!IsValidStewardCandidate(picked))
            {
                var activeRuleQuery = QueryActiveRules();
                while (activeRuleQuery.MoveNext(out var activeRuleUid, out var activeRuleComp2, out var activeRule, out var activeGameRule))
                {
                    if (activeRule.CurrentTier > 0)
                        return;

                    activeRule.StewardVoteTimer ??= _timing.CurTime + _voteDelay;
                    break;
                }

                return;
            }

            EnsureComp<CosmicCultLeadComponent>(picked);
            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Cult stewardship vote finished: {Identity.Entity(picked, EntityManager)} is now steward.");
            _antag.SendBriefing(picked, Loc.GetString("cosmiccult-vote-steward-briefing"), Color.FromHex("#4cabb3"), _monumentAlert);
        };
    }

    private void SpawnRift()
    {
        if (TryFindRandomTile(out var _, out var _, out var _, out var coords))
        {
            Spawn("CosmicMalignRift", coords);
        }
    }

    private void OnAntagSelect(Entity<CosmicCultRuleComponent> uid, ref AfterAntagEntitySelectedEvent args)
        => TryStartCult(args.EntityUid, uid);
    #endregion

    #region Round & Objectives

    private void OnGodSpawn(Entity<CosmicGodComponent> uid, ref ComponentInit args)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var ruleUid, out _, out var cultRule, out _))
        {
            SetWinType((ruleUid, cultRule), WinType.CultComplete); //here's no coming back from this. Cult wins this round
            var monumentMap = Transform(cultRule.MonumentInGame).MapUid;
            QueueDel(cultRule.MonumentInGame); // The monument doesn't need to stick around postround! Into the bin with you.
            QueueDel(cultRule.MonumentSlowZone); // cease exist

            _roundEnd.EndRound(); //Woo game over yeaaaah

            var spawnPoints = EntityManager.GetAllComponents(typeof(CosmicVoidSpawnComponent)).ToImmutableList();
            if (spawnPoints.IsEmpty)
                return;

            var endQuery = EntityQueryEnumerator<HumanoidAppearanceComponent, MobStateComponent>();
            while (endQuery.MoveNext(out var player, out _, out _))
            {
                var newSpawn = _rand.Pick(spawnPoints);
                var spawnTgt = Transform(newSpawn.Uid).Coordinates;

                if (cultRule.Cultists.Contains(player))
                    Timer.Spawn(TimeSpan.FromSeconds(30), () => EndRoundVoid(player, spawnTgt, cultRule, null));
                else
                    Timer.Spawn(_rand.Next(TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(30)), () => EndRoundVoid(player, spawnTgt, cultRule, monumentMap));
            }
        }
    }

    private void EndRoundVoid(EntityUid player, EntityCoordinates spawnTgt, CosmicCultRuleComponent cultRule, EntityUid? monumentMap)
    {
        if (_mobStateSystem.IsDead(player) || !_mind.TryGetMind(player, out var mind, out _))
            return;

        if (monumentMap is not null && Transform(player).MapUid != monumentMap)
            return;

        if (cultRule.Cultists.Contains(player))
        {
            var mob = Spawn(cultRule.CosmicAscended, spawnTgt);
            _mind.TransferTo(mind, mob);
            _metaData.SetEntityName(mob, Loc.GetString("cosmiccult-astral-ascendant", ("name", player))); //Renames cultists' ascendant forms to "[CharacterName], Ascendant"
        }
        else
        {
            var mob = Spawn(_rand.Pick(cultRule.CosmicMobs), spawnTgt);
            _mind.TransferTo(mind, mob);
            _metaData.SetEntityName(mob, Loc.GetString("cosmiccult-astral-minion", ("name", player))); //Renames non-cultists to "[CharacterName], Malign"
        }
        Spawn(cultRule.WarpVFX, spawnTgt);
        Spawn(cultRule.WarpVFX, Transform(player).Coordinates);
        _audio.PlayPvs(cultRule.WarpSFX, spawnTgt, AudioParams.Default.WithVolume(3f));
        _gibbing.Gib(player); // you don't need that body anymore
    }

    private static void SetWinType(Entity<CosmicCultRuleComponent> ent, WinType type)
    {
        if (ent.Comp.WinLocked)
            return;
        ent.Comp.WinType = type;

        if (type is WinType.CultComplete or WinType.CrewComplete) //Let's lock in our WinType to prevent us from setting a worse win if a better win's been achieved.
            ent.Comp.WinLocked = true;
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New is not GameRunLevel.PostRound) //Are we moving to post-round?
            return;

        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var cultRule, out _))
        {
            ConfirmWinState((uid, cultRule)); //If so, let's consult our Winconditions and set an appropriate WinType.
        }
    }

    private bool CultistsAlive()
    {
        var query = EntityQueryEnumerator<CosmicCultComponent, MobStateComponent>();
        while (query.MoveNext(out _, out var comp, out var mob))
        {
            if (mob.Running && mob.CurrentState == MobState.Alive)
                return true;
        }

        return false;
    }

    private void OnMobStateChanged(Entity<CosmicCultComponent> ent, ref MobStateChangedEvent args)
    {
        if (HasComp<CosmicCultLeadComponent>(ent) && _mobStateSystem.IsDead(ent))
        {
            RemCompDeferred<CosmicCultLeadComponent>(ent);
            TryQueueStewardRevote(ent, removeStewardComp: false);
        }

        if (CultistsAlive())
            return;

        var query = QueryActiveRules();
        while (query.MoveNext(out var ruleUid, out var _, out var ruleComp, out var _))
        {
            ConfirmWinState((ruleUid, ruleComp));
        }
    }

    private void ConfirmWinState(Entity<CosmicCultRuleComponent> ent)
    {
        var tier = ent.Comp.CurrentTier;
        var leaderAlive = false;
        var centcomm = _emergency.GetCentcommMaps();
        var wrapup = AllEntityQuery<CosmicCultComponent, TransformComponent>();
        while (wrapup.MoveNext(out var cultist, out _, out var cultistLocation))
        {
            if (cultistLocation.MapUid != null && centcomm.Contains(cultistLocation.MapUid.Value))
            {
                if (HasComp<CosmicCultLeadComponent>(cultist) && _mobStateSystem.IsAlive(cultist))
                    leaderAlive = true;
            }
        }
        if (tier < 3 && leaderAlive)
            SetWinType(ent, WinType.Neutral); //The Monument isn't Tier 3, but the cult leader's alive and at Centcomm! a Neutral outcome
        var monument = AllEntityQuery<CosmicFinaleComponent>();
        while (monument.MoveNext(out var monumentUid, out var comp))
        {
            _sound.StopStationEventMusic(ent, StationEventMusicType.CosmicCult);
            if (tier == 3 && comp.CurrentState == FinaleState.Unavailable)
            {
                SetWinType(ent, WinType.CultMinor); //The crew escaped, and The Monument wasn't fully empowered. a small win
            }
            else if (comp.CurrentState != FinaleState.Unavailable)
            {
                SetWinType(ent, WinType.CultMajor); //Despite the crew's escape, The Finale is available or active. Major win
            }
        }

        if (CultistsAlive())
            return; // There's still cultists alive! stop checking stuff

        _roundEnd.DoRoundEndBehavior(ent.Comp.RoundEndBehavior, ent.Comp.EvacShuttleTime, ent.Comp.RoundEndTextSender, ent.Comp.RoundEndTextShuttleCall, ent.Comp.RoundEndTextAnnouncement);
        ent.Comp.RoundEndBehavior = RoundEndBehavior.Nothing; // prevent this being called multiple times.
        ent.Comp.RiftStop = true; // rifts can stop spawning now.

        if (TryComp(ent.Comp.MonumentInGame, out TransformComponent? monumentTransform) && monumentTransform.MapUid is { } monumentMap)
        {
            _weather.TryRemoveWeather(monumentMap, "WeatherCosmic");
            RemComp<PreventFTLComponent>(monumentMap);
        }

        var gameruleMonument = ent.Comp.MonumentInGame;
        if (TryComp<CosmicFinaleComponent>(gameruleMonument, out var finComp) && TryComp(gameruleMonument, out TransformComponent? gameruleMonumentTransform))
        {
            MonumentSystem.Disable(gameruleMonument);
            finComp.CurrentState = FinaleState.Unavailable;
            _popup.PopupCoordinates(Loc.GetString("cosmiccult-monument-powerdown"), gameruleMonumentTransform.Coordinates, PopupType.Large);
            _sound.StopStationEventMusic(gameruleMonument, StationEventMusicType.CosmicCult);
            _monument.UpdateMonumentAppearance(gameruleMonument, false);
        }

        if (ent.Comp.TotalCult == 0)
            SetWinType(ent, WinType.CrewComplete); // No cultists registered! That means everyone got deconverted
        else
            SetWinType(ent, WinType.CrewMajor); // There's still cultists registered, but if we got here, that means they're all dead
    }

    protected override void AppendRoundEndText(EntityUid uid,
        CosmicCultRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        var ftlKey = component.WinType.ToString().ToLower();
        var winType = Loc.GetString($"cosmiccult-roundend-{ftlKey}");
        var summaryText = Loc.GetString($"cosmiccult-summary-{ftlKey}");
        args.AddLine(winType);
        args.AddLine(summaryText);
        args.AddLine(Loc.GetString("cosmiccult-roundend-cultist-count", ("initialCount", component.TotalCult)));
        args.AddLine(Loc.GetString("cosmiccult-roundend-cultpop-count", ("count", component.PercentConverted)));
        args.AddLine(Loc.GetString("cosmiccult-roundend-entropy-count", ("count", component.EntropySiphoned)));
        args.AddLine(Loc.GetString("cosmiccult-roundend-monument-stage", ("stage", component.CurrentTier)));
    }

    public void IncrementCultObjectiveEntropy(Entity<CosmicCultComponent> ent)
    {
        if (AssociatedGamerule(ent) is not { } cult)
            return;

        cult.Comp.EntropySiphoned += ent.Comp.CosmicSiphonQuantity;
        var query = EntityQueryEnumerator<CosmicEntropyConditionComponent>();
        while (query.MoveNext(out _, out var entropyComp))
        {
            entropyComp.Siphoned = cult.Comp.EntropySiphoned;
        }
    }

    public void AdjustCultObjectiveConversion(int value)
    {
        var query = EntityQueryEnumerator<CosmicConversionConditionComponent>();
        while (query.MoveNext(out _, out var conversionComp))
        {
            conversionComp.Converted += value;
        }
    }
    #endregion

    public void OnStartMonument(Entity<MonumentComponent> ent)
    {
        if (AssociatedGamerule(ent) is not { } cult)
            return;

        cult.Comp.CurrentTier = 1;
        cult.Comp.MonumentInGame = ent; //Since there's only one Monument per round, let's store its UID for the rest of the round. Saves us on spamming enumerators.
        _monument.MonumentTier1(ent);
        UpdateCultData(ent);
    }

    public void UpdateCultData(Entity<MonumentComponent> uid) // This runs every time Entropy is Inserted into The Monument, and every time a Cultist is Converted or Deconverted.
    {
        if (!TryComp<CosmicFinaleComponent>(uid, out var finaleComp))
            return;

        if (AssociatedGamerule(uid) is not { } cult)
            return;

        cult.Comp.TotalCrew = _playerMan.Sessions.Count(session => session.Status == SessionStatus.InGame && HasComp<HumanoidAppearanceComponent>(session.AttachedEntity));

#if DEBUG
        if (cult.Comp.TotalCrew < 25)
            cult.Comp.TotalCrew = 25;
#endif

        cult.Comp.PercentConverted = Math.Round((double)(100 * cult.Comp.TotalCult) / cult.Comp.TotalCrew);

        //this can probably be somewhere else but
        _monument.UpdateMonumentReqsForTier(uid, cult.Comp.CurrentTier);
        _monument.UpdateMonumentProgress(uid, cult);

        if (uid.Comp.CurrentProgress >= uid.Comp.TargetProgress && cult.Comp.CurrentTier == 3 && finaleComp.CurrentState == FinaleState.Unavailable)
        {
            if (!finaleComp.FinaleDelayStarted) //check if we've not already started the finale delay
            {
                finaleComp.FinaleDelayStarted = true; //set that we've started it
                //do everything else

                var timer = _finaleDelay;
                var cultistQuery = EntityQueryEnumerator<CosmicCultComponent>();
                while (cultistQuery.MoveNext(out var cultist, out var cultistComp))
                {
                    var mins = timer.Minutes;
                    var secs = timer.Seconds;
                    _antag.SendBriefing(cultist,
                        Loc.GetString("cosmiccult-finale-autocall-briefing",
                            ("minutesandseconds", $"{mins} minutes and {secs} seconds")),
                        Color.FromHex("#4cabb3"),
                        _monumentAlert);
                }

                cult.Comp.PrepareFinaleTimer = _timing.CurTime + timer;
            }
        }
        else if (finaleComp.CurrentState != FinaleState.Unavailable)
            MonumentSystem.SetTargetProgess(uid, uid.Comp.CurrentProgress);
        else if (uid.Comp.CurrentProgress >= uid.Comp.TargetProgress && cult.Comp.CurrentTier == 2 && uid.Comp.CanTierUp)
        {
            MonumentSystem.SetCanTierUp(uid, false);

            var cultistQuery = EntityQueryEnumerator<CosmicCultComponent>();
            while (cultistQuery.MoveNext(out var cultist, out var cultistComp))
            {
                _antag.SendBriefing(cultist, Loc.GetString("cosmiccult-monument-stage3-briefing", ("time", _t3RevealDelay.TotalSeconds)), Color.FromHex("#4cabb3"), _monumentAlert);
            }

            _monument.MonumentTier3(uid);
            _monument.UpdateMonumentReqsForTier(uid, cult.Comp.CurrentTier);
            cult.Comp.CurrentTier = 3;

            cult.Comp.Tier3DelayTimer = _timing.CurTime + _t3RevealDelay;
        }
        else if (uid.Comp.CurrentProgress >= uid.Comp.TargetProgress && cult.Comp.CurrentTier == 1 && uid.Comp.CanTierUp)
        {
            MonumentSystem.SetCanTierUp(uid, false);

            var cultistQuery = EntityQueryEnumerator<CosmicCultComponent>();
            while (cultistQuery.MoveNext(out var cultist, out var cultistComp))
            {
                _antag.SendBriefing(cultist, Loc.GetString("cosmiccult-monument-stage2-briefing", ("time", _t2RevealDelay.TotalSeconds)), Color.FromHex("#4cabb3"), _monumentAlert);
            }

            _monument.MonumentTier2(uid);
            cult.Comp.CurrentTier = 2;
            _monument.UpdateMonumentReqsForTier(uid, cult.Comp.CurrentTier);

            cult.Comp.Tier2DelayTimer = _timing.CurTime + _t2RevealDelay;
        }

        _monument.UpdateMonumentAppearance(uid, false);

        Dirty(uid);
        _ui.SetUiState(uid.Owner, MonumentKey.Key, new MonumentBuiState(uid.Comp));
    }

    /// <summary>
    /// if the steward cryos or ghosts (not dies),
    /// then call a revote to elect a new one
    /// </summary>
    /// <remarks>generally only so that stewards can be revoted without admin intervention.
    /// if it causes issues, this is easy to remove</remarks>
    private void HandleMindRemoved(Entity<CosmicCultLeadComponent> ent, ref MindRemovedMessage args)
    {
        if (HasComp<CosmicBlankComponent>(ent)) // Their mind got artificially removed, don't start a revote.
            return;

        // remove the comp. If they died and ghosted and come back to their body they will no longer be the leader.
        RemCompDeferred<CosmicCultLeadComponent>(ent);
        TryQueueStewardRevote(ent, removeStewardComp: false);
    }

    #region De- & Conversion
    public void TryStartCult(EntityUid uid, Entity<CosmicCultRuleComponent> rule)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;

        EnsureComp<CosmicCultComponent>(uid, out var cultComp);
        EnsureComp<CosmicCultAssociatedRuleComponent>(uid, out var associatedComp);

        associatedComp.CultGamerule = rule;

        if (!_role.MindHasRole<CosmicCultRoleComponent>(mindId))
            _role.MindAddRole(mindId, MindRole, mind, true);

        _antag.SendBriefing(uid, Loc.GetString("cosmiccult-role-roundstart-fluff"), Color.FromHex("#4cabb3"), _briefingSound);
        _antag.SendBriefing(uid, Loc.GetString("cosmiccult-role-short-briefing"), Color.FromHex("#cae8e8"), null);

        _languageSystem.AddLanguage(uid, _cultLanguage);

        if (_playerMan.TryGetSessionById(mind.UserId, out var session))
        {
            _euiMan.OpenEui(new CosmicRoundStartEui(), session);
        }

        rule.Comp.TotalCult++;

        Dirty(uid, cultComp);

        rule.Comp.Cultists.Add(uid);
    }

    private void OnAssociateRule(ref CosmicCultAssociateRuleEvent args)
    {
        TransferCultAssociation(args.Originator, args.Target);
        if (TryComp<MonumentComponent>(args.Target, out var monument))
        {
            OnStartMonument((args.Target, monument));
        }
    }

    public void TransferCultAssociation(EntityUid from, EntityUid to)
    {
        if (!TryComp<CosmicCultAssociatedRuleComponent>(from, out var source))
            return;

        var destination = EnsureComp<CosmicCultAssociatedRuleComponent>(to);
        destination.CultGamerule = source.CultGamerule;
    }

    public Entity<CosmicCultRuleComponent>? AssociatedGamerule(EntityUid uid)
    {
        if (!TryComp<CosmicCultAssociatedRuleComponent>(uid, out var associated))
        {
            _sawmill.Debug("{0} has no associated rule", uid);
            return null;
        }

        if (!TryComp<CosmicCultRuleComponent>(associated.CultGamerule, out var cult))
        {
            _sawmill.Debug("Associated gamerule {0} is not a cult gamerule", associated.CultGamerule);
            return null;
        }

        return (associated.CultGamerule, cult);
    }

    public void CosmicConversion(EntityUid converter, EntityUid uid)
    {
        if (AssociatedGamerule(converter) is not { } cult)
            return;

        if (!_mind.TryGetMind(uid, out var mindId, out var mind) ||
            !_playerMan.TryGetSessionById(mind.UserId, out var session))
            return;

        if (!_role.MindHasRole<CosmicCultRoleComponent>(mindId))
            _role.MindAddRole(mindId, MindRole, mind, true);

        _antag.SendBriefing(session, Loc.GetString("cosmiccult-role-conversion-fluff"), Color.FromHex("#4cabb3"), _briefingSound);
        _antag.SendBriefing(uid, Loc.GetString("cosmiccult-role-short-briefing"), Color.FromHex("#cae8e8"), null);

        var cultComp = EnsureComp<CosmicCultComponent>(uid);
        cultComp.EntropyBudget = 10; // pity balance
        EnsureComp<IntrinsicRadioReceiverComponent>(uid);
        TransferCultAssociation(converter, uid);

        if (cult.Comp.CurrentTier == 3)
        {
            cultComp.EntropyBudget = 48; // pity balance
            cultComp.Respiration = false;

            foreach (var influenceProto in _protoMan.EnumeratePrototypes<InfluencePrototype>().Where(influenceProto => influenceProto.Tier == 3))
            {
                cultComp.UnlockedInfluences.Add(influenceProto.ID);
            }

            EnsureComp<CosmicStarMarkComponent>(uid);
            EnsureComp<PressureImmunityComponent>(uid);
            EnsureComp<TemperatureImmunityComponent>(uid);
        }
        else if (cult.Comp.CurrentTier == 2)
        {
            cultComp.EntropyBudget = 26; // pity balance

            foreach (var influenceProto in _protoMan.EnumeratePrototypes<InfluencePrototype>().Where(influenceProto => influenceProto.Tier == 2))
            {
                cultComp.UnlockedInfluences.Add(influenceProto.ID);
            }
        }

        Dirty(uid, cultComp);

        _languageSystem.AddLanguage(uid, _cultLanguage);

        _mind.TryAddObjective(mindId, mind, "CosmicFinalityObjective");
        _mind.TryAddObjective(mindId, mind, "CosmicMonumentObjective");
        _mind.TryAddObjective(mindId, mind, "CosmicConversionObjective");
        _mind.TryAddObjective(mindId, mind, "CosmicEntropyObjective");

        _euiMan.OpenEui(new CosmicConvertedEui(), session);

        RemComp<BibleUserComponent>(uid);

        // Bright-eye Nerf - Yeah im not gona let them be immortal!
        if (TryComp<BrighteyeComponent>(uid, out var brighteye))
        {
            if (brighteye.Portal is not null)
            {
                SpawnAtPosition(brighteye.ShadekinShadow, Transform(brighteye.Portal.Value).Coordinates);
                QueueDel(brighteye.Portal.Value);
            }

            _actions.RemoveAction(uid, brighteye.PortalAction);
            _actions.RemoveAction(uid, brighteye.ShadeSkipAction);
        }

        cult.Comp.TotalCult++;
        cult.Comp.Cultists.Add(uid);

        AdjustCultObjectiveConversion(1);
        UpdateCultData(cult.Comp.MonumentInGame);
    }

    private bool IsValidStewardCandidate(EntityUid uid)
        => HasComp<CosmicCultComponent>(uid)
            && !HasComp<CosmicCultLeadComponent>(uid)
            && _mobStateSystem.IsAlive(uid)
            && _mind.TryGetMind(uid, out _, out var mind) &&
                _playerMan.TryGetSessionById(mind.UserId, out _);

    private bool TryQueueStewardRevote(
        EntityUid oldSteward,
        Entity<CosmicCultRuleComponent>? rule = null,
        bool removeStewardComp = true)
    {
        var cult = rule ?? AssociatedGamerule(oldSteward);

        if (cult is not { } cultRule)
        {
            _sawmill.Info("Did not queue steward revote for {0}: no associated cult rule.", ToPrettyString(oldSteward));
            return false;
        }

        if (cultRule.Comp.CurrentTier > 0)
        {
            _sawmill.Info(
                "Did not queue steward revote for {0}: monument is already placed, tier {1}.",
                ToPrettyString(oldSteward),
                cultRule.Comp.CurrentTier);
            return false;
        }

        // Stop duplicate revotes.
        if (cultRule.Comp.StewardVoteTimer != null)
        {
            _sawmill.Info(
                "Did not queue steward revote for {0}: steward vote timer is already pending for {1}.",
                ToPrettyString(oldSteward),
                cultRule.Comp.StewardVoteTimer);
            return false;
        }

        if (removeStewardComp && HasComp<CosmicCultLeadComponent>(oldSteward))
            RemCompDeferred<CosmicCultLeadComponent>(oldSteward);

        var cultistsList = new List<EntityUid>();
        var query = EntityQueryEnumerator<CosmicCultComponent>();

        while (query.MoveNext(out var cultist, out _))
        {
            if (cultist == oldSteward)
                continue;

            cultistsList.Add(cultist);
        }

        var sender = Loc.GetString("cosmiccult-announcement-sender");
        var allCultists = Filter.Empty().FromEntities([.. cultistsList]);

        _sawmill.Info(
            "Queued steward revote for old steward {0}. Vote starts at {1}, delay {2}, tier {3}, notified cultists {4}.",
            ToPrettyString(oldSteward),
            _timing.CurTime + _voteDelay,
            _voteDelay,
            cultRule.Comp.CurrentTier,
            cultistsList.Count);

        _chatSystem.DispatchFilteredAnnouncement(
            allCultists,
            Loc.GetString("cosmiccult-leader-abandonment-message"),
            sender: sender,
            playSound: false,
            colorOverride: Color.FromHex("#4eb1b1"));

        cultRule.Comp.StewardVoteTimer = _timing.CurTime + _voteDelay;
        return true;
    }

    private void OnComponentShutdown(Entity<CosmicCultComponent> uid, ref ComponentShutdown args)
    {
        if (AssociatedGamerule(uid) is not { } cult)
            return;

        var wasSteward = HasComp<CosmicCultLeadComponent>(uid);
        var cosmicGamerule = cult.Comp;

        var isDeleting = TerminatingOrDeleted(uid.Owner);
        if (isDeleting)
        {
            if (wasSteward)
                TryQueueStewardRevote(uid.Owner, cult, removeStewardComp: false);

            cosmicGamerule.TotalCult--;
            cosmicGamerule.Cultists.Remove(uid);
            AdjustCultObjectiveConversion(-1);
            UpdateCultData(cosmicGamerule.MonumentInGame);
            return;
        }

        if (wasSteward)
        {
            TryQueueStewardRevote(uid.Owner, cult, removeStewardComp: false);
            RemComp<CosmicCultLeadComponent>(uid);
        }

        _stun.TryAddStunDuration(uid.Owner, TimeSpan.FromSeconds(2));
        foreach (var actionEnt in uid.Comp.ActionEntities) _actions.RemoveAction(actionEnt);

        _languageSystem.RemoveLanguage(uid.Owner, _cultLanguage);
        RemComp<InfluenceVitalityComponent>(uid);
        RemComp<InfluenceStrideComponent>(uid);
        RemComp<PressureImmunityComponent>(uid);
        RemComp<TemperatureImmunityComponent>(uid);
        RemComp<CosmicStarMarkComponent>(uid);
        RemComp<CosmicCultExamineComponent>(uid);
        _antag.SendBriefing(uid, Loc.GetString("cosmiccult-role-deconverted-fluff"), Color.FromHex("#4cabb3"), _deconvertSound);
        _antag.SendBriefing(uid, Loc.GetString("cosmiccult-role-deconverted-briefing"), Color.FromHex("#cae8e8"), null);

        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;

        if (_mind.TryFindObjective((mindId, mind), "CosmicFinalityObjective", out var finalityObjective) && finalityObjective != null)
            _mind.TryRemoveObjective(mindId, mind, finalityObjective.Value);
        if (_mind.TryFindObjective((mindId, mind), "CosmicMonumentObjective", out var monumentObjective) && monumentObjective != null)
            _mind.TryRemoveObjective(mindId, mind, monumentObjective.Value);
        if (_mind.TryFindObjective((mindId, mind), "CosmicConversionObjective", out var conversionObjective) && conversionObjective != null)
            _mind.TryRemoveObjective(mindId, mind, conversionObjective.Value);
        if (_mind.TryFindObjective((mindId, mind), "CosmicEntropyObjective", out var entropyObjective) && entropyObjective != null)
            _mind.TryRemoveObjective(mindId, mind, entropyObjective.Value);

        _role.MindRemoveRole<CosmicCultRoleComponent>(mindId);
        _role.MindRemoveRole<RoleBriefingComponent>(mindId);
        if (_playerMan.TryGetSessionById(mind.UserId, out var session))
        {
            if (HasComp<RevolutionaryComponent>(uid) || HasComp<HeadRevolutionaryComponent>(uid))
                _euiMan.OpenEui(new DeconvertedEui(), session);
            else
                _euiMan.OpenEui(new CosmicDeconvertedEui(), session);
        }
        _eye.SetVisibilityMask(uid, 1);
        _alerts.ClearAlert(uid.Owner, uid.Comp.EntropyAlert);
        cosmicGamerule.TotalCult--;
        cosmicGamerule.Cultists.Remove(uid);
        AdjustCultObjectiveConversion(-1);
        UpdateCultData(cosmicGamerule.MonumentInGame);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);

        // Brighteye - Yeah, Lets give their portal back!
        if (TryComp<BrighteyeComponent>(uid, out var brighteye) && !brighteye.LesserKin)
        {
            _actions.AddAction(uid, ref brighteye.PortalAction, brighteye.BrighteyePortalAction, uid);
            _actions.AddAction(uid, ref brighteye.ShadeSkipAction, brighteye.BrighteyeShadeSkipAction, uid);
            _actions.SetCooldown(brighteye.PortalAction, TimeSpan.FromSeconds(300));
        }
    }
    #endregion
}
