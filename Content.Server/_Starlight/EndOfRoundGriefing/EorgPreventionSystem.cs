using System.Linq;
using System.Threading;
using Content.Server._Starlight.Station;
using Content.Server.AlertLevel;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Cuffs;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Implants;
using Content.Server.Nuke;
using Content.Server.Polymorph.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._NullLink;
using Content.Shared._Starlight.EndOfRoundGriefing;
using Content.Shared._Starlight.EndOfRoundGriefing.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.GameTicking;
using Content.Shared.Mech.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Polymorph;
using Content.Shared.Roles;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nuke;
using Content.Shared.NukeOps;
using Content.Shared.Zombies;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Starlight.EndOfRoundGriefing;

/// <summary>
/// <inheritdoc/>
///
/// The server side of the EORG prevent system manages state and is responsible for managing the <see cref="PreventEorgComponent"/>s on players.
/// </summary>
public sealed partial class EorgPreventionSystem : SharedEorgPreventionSystem
{
    [Dependency] private ChatSystem _cSys = null!;
    [Dependency] private StationSystem _station = null!;
    [Dependency] private MobStateSystem _state = null!;
    [Dependency] private CuffableSystem _cuffable = null!;
    [Dependency] private SharedRoleSystem _role = null!;
    [Dependency] private AlertLevelSystem _alertLevel = null!;
    [Dependency] private SharedTransformSystem _xform = null!;
    [Dependency] private SubdermalImplantSystem _implant = null!;
    [Dependency] private EmergencyShuttleSystem _shuttle = null!;
    [Dependency] private IChatManager _chat = null!;
    [Dependency] private IPrototypeManager _proto = null!;
    [Dependency] private IConfigurationManager _cfg = null!;
    [Dependency] private ISharedNullLinkPlayerRolesReqManager _rolesReq = null!;

    private const string FreedomImplantProtoId = "FreedomImplant";

    private readonly List<EntityUid> _pendingPacification = [];
    private readonly List<EntityUid> _pacified = [];

    private static readonly TimeSpan _nukeCheckInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nukeCheckAccumulator;
    private CancellationTokenSource? _cts;

    private List<EntityUid> _activeThreats = [];
    private bool _ccHadDefenders;

    /// <summary>
    /// Threat/clear announcements already made this round, so they aren't replayed when threats revive or die again.
    /// </summary>
    private readonly HashSet<string> _announced = [];

    /// <summary>
    /// How far zeds must be from CC to no longer count as a threat.
    /// </summary>
    private const float ZombieMaxDriftDistance = 32f;

    private readonly record struct ThreatArea(List<EntityUid?> EndGrids, EntityUid? CcMap, Box2? CcBounds);

    private static readonly TimeSpan _deltaDelay = TimeSpan.FromSeconds(3);

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(StarlightCCVars.PeacefulRoundEnd, v =>
        {
            IsEnabled = v;
            BroadcastState();
        }, true);
    }

    private void BroadcastState() =>
        RaiseNetworkEvent(new EorgPreventionStateEvent(IsEnabled, HasRoundEnded, IsPending));

    /// <summary>
    /// Validate an entity is eligible for pacification, and applies it if so.
    /// </summary>
    /// <param name="target">The entity to potentially pacify</param>
    private void SpreadPeaceNow(EntityUid target)
    {
        if (!IsEnabled || !HasRoundEnded) return;

        // Filters out ineligible targets and adds polymorph parents.
        var targets = new List<EntityUid>();
        SpreadPeaceQueueing(target, ref targets);

        if (IsPending)
        {
            _pendingPacification.AddRange(targets);
            return;
        }

        foreach (var uid in targets)
            Pacify(uid);
    }

    /// <summary>
    /// The queueing counterpart of <see cref="SpreadPeaceNow"/>. Checks if the target is a valid pacification candidate
    /// and adds them to the candidate list if they are valid pacification.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="candidates"></param>
    private void SpreadPeaceQueueing(EntityUid target, ref List<EntityUid> candidates)
    {
        if (!ShouldPacify(target)) return;
        candidates.Add(target);

        // If the entity is polymorphed, also pacify the parent(s).
        while (TryComp<PolymorphedEntityComponent>(target, out var polymorph)
            && polymorph.Parent is { } parent && !TerminatingOrDeleted(parent))
        {
            target = parent;
            candidates.Add(target);
        }
    }

    /// <summary>
    /// Determine if the target should be pacified. Does not do any polymorph-related checks.
    /// </summary>
    /// <param name="target">The entity to test</param>
    /// <returns>Whether the target should be pacified, not accounting or polymorphs</returns>
    private bool ShouldPacify(EntityUid target)
    {
        // OOC bypass (staff, extroles, ...).
        if (_rolesReq.IsPeacefulBypass(target))
            return false;

        // Only pacify people on Evac and CC grids.
        if (!IsGridPacificationTarget(target))
            return false;

        // IC bypasses only apply to a specific mind roles (taken roles of ERT, Decimus, CC, ...).
        // Note that in the case of polymorphs, the mind and thus the mind roles are only contained in the child entity
        // (the one that is being actively controlled).
        if (IsMindRolePacificationImmune(target))
            return false;

        // IC bypass (only when ghost role wasn't taken)
        return !IsGhostRolePacificationImmune(target);
    }

    /// <summary>
    /// Pacify a target immediately.
    /// </summary>
    /// <param name="target">The target to pacify</param>
    private void Pacify(EntityUid target)
    {
        if (!HasComp<PacifiedComponent>(target))
        {
            AddComp<PacifiedComponent>(target);
            _pacified.Add(target);
        }

        EnsureComp<PreventEorgComponent>(target);
    }

    private void BlockPacification()
    {
        IsPending = true;
        foreach (var uid in _pacified.Where(uid => !TerminatingOrDeleted(uid)))
        {
            RemComp<PacifiedComponent>(uid);
            RemComp<PreventEorgComponent>(uid);
            _pendingPacification.Add(uid);
        }

        _pacified.Clear();
        BroadcastState();
        _chat.DispatchServerAnnouncement(Loc.GetString("eor-pacifism-reblocked"), Color.Red);
    }

    private void AddThreat(EntityUid uid)
    {
        if (_activeThreats.Contains(uid))
            return;

        _activeThreats.Add(uid);
        UpdateThreatAlerts();
        if (!IsPending)
            BlockPacification();
    }

    private void RemoveThreat(EntityUid uid)
    {
        if (!_activeThreats.Remove(uid))
            return;

        UpdateThreatAlerts();

        if (_activeThreats.Count == 0)
            PacifyPending();
    }

    private void UpdateThreatAlerts()
    {
        if (!HasRoundEnded || _cts != null)
            return;

        if (EnsureCentCommStation() is not { } cc)
            return;

        var zeds = _activeThreats.Any(HasComp<ZombieComponent>);
        var nukies = _activeThreats.Any(uid => !HasComp<ZombieComponent>(uid));

        var level = nukies ? "gamma" : zeds ? "violet" : "green";
        if (_alertLevel.GetLevel(cc) != level)
            _alertLevel.SetLevel(cc, level, true, false, true, level != "green");

        if (zeds)
            AnnounceOnce(cc, "eor-zed-threat", Color.FromHex("#2B9CFF"));
        else if (_announced.Contains("eor-zed-threat"))
            AnnounceOnce(cc, "eor-zed-clear", Color.LimeGreen);

        if (nukies)
            AnnounceOnce(cc, "eor-nukie-threat", Color.FromHex("#E01B1B"));
        else if (_announced.Contains("eor-nukie-threat"))
            AnnounceOnce(cc, "eor-nukie-clear", Color.LimeGreen);
    }

    private void AnnounceOnce(EntityUid cc, string message, Color color)
    {
        if (!_announced.Add(message))
            return;

        _cSys.DispatchStationAnnouncement(cc, Loc.GetString(message), Loc.GetString("eor-cassie"),
            false, null, color);
    }

    private void PacifyPending()
    {
        IsPending = false;
        BroadcastState();
        _chat.DispatchServerAnnouncement(Loc.GetString("eor-pacifism-unblocked"), Color.LimeGreen);
        foreach (var uid in _pendingPacification.Where(uid => !TerminatingOrDeleted(uid)))
            Pacify(uid);
        _pendingPacification.Clear();
    }

    #region Pacification checks

    /// <summary>
    /// Checks if the entity has any mind roles that are exempt from pacification.
    /// </summary>
    private bool IsMindRolePacificationImmune(EntityUid uid)
    {
        if (!TryComp<MindContainerComponent>(uid, out var mindContainer) ||
            !TryComp<MindComponent>(mindContainer.Mind, out var mind))
            return false;

        foreach (var role in _role.MindGetAllRoleInfo((mindContainer.Mind.Value, mind)).Where(role => !role.Antagonist))
        {
            if (!_proto.TryIndex<JobPrototype>(role.Prototype, out var mindJob))
                continue;
            if (mindJob.BypassEorPacification)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the entity has any ghost roles that are exempt from pacification.
    /// </summary>
    private bool IsGhostRolePacificationImmune(EntityUid uid)
    {
        if (!TryComp<GhostRoleComponent>(uid, out var ghostRole) ||
            !_proto.TryIndex(ghostRole.JobProto, out var job))
            return false;
        return job.BypassEorPacification;
    }

    /// <summary>
    /// Check whether a grid is a target for pacification. Returns true for Evac and CentComm only.
    /// </summary>
    private bool IsGridPacificationTarget(EntityUid uid)
    {
        var xform = Transform(uid);
        var grid = xform.GridUid;

        if (HasComp<EmergencyShuttleComponent>(grid))
            return true; // Evac shuttle (escape pods don't count for this) = pacified

        AllEntityQuery<StationCentcommComponent>().MoveNext(out var centcomm);
        // CC = pacified
        // In all other cases we do not *mechanically* enforce it.
        // This way station-ending antags can still do their thing,
        // and sec can still fight back if they're left behind on station.
        return centcomm != null && centcomm.Entity == grid;
    }

    private IEnumerable<EntityUid?> GetEndGrids()
    {
        var endGrids = _shuttle.GetShuttles();
        var ccs = AllEntityQuery<StationCentcommComponent>();
        while (ccs.MoveNext(out var centcomm))
        {
            if (centcomm.Entity is { } grid)
                endGrids.Add(grid);
        }

        return endGrids;
    }

    #endregion

    #region Event handlers

    [SubscribeLocalEvent]
    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
        => SpreadPeaceNow(ev.Mob);

    [SubscribeLocalEvent]
    private void OnRehydrateEvent(ref GotRehydratedEvent ev)
        => SpreadPeaceNow(ev.Target);

    [SubscribeLocalEvent]
    private void OnRoundEndCancelled(RoundEndCancelMessageEvent ev)
    {
        foreach (var uid in _pacified.Where(uid => !TerminatingOrDeleted(uid)))
        {
            RemComp<PacifiedComponent>(uid);
            RemComp<PreventEorgComponent>(uid);
        }

        ResetState();
    }

    [SubscribeLocalEvent]
    private void OnRoundCleanup(RoundRestartCleanupEvent ev) => ResetState();

    private void ResetState()
    {
        HasRoundEnded = false;
        IsPending = false;
        _activeThreats.Clear();
        _pendingPacification.Clear();
        _pacified.Clear();
        _nukeCheckAccumulator = TimeSpan.Zero;
        _ccHadDefenders = false;
        _announced.Clear();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        BroadcastState();
    }

    [SubscribeLocalEvent]
    private void OnRoundEnded(RoundEndTextAppendEvent ev)
    {
        HasRoundEnded = true;
        BroadcastState();
        if (!IsEnabled) return;

        var candidates = new List<EntityUid>();

        // Collect candidate entities for pacification.
        var mobMoverQuery = EntityQueryEnumerator<MobMoverComponent>();
        while (mobMoverQuery.MoveNext(out var uid, out _))
            SpreadPeaceQueueing(uid, ref candidates);

        var mechQuery = EntityQueryEnumerator<MechComponent>();
        while (mechQuery.MoveNext(out var uid, out _))
            SpreadPeaceQueueing(uid, ref candidates);

        // Remove any candidates that are part of a polymorph chain where some immunity (OOC or job immunity) is at play.
        var polymorphedQuery = EntityQueryEnumerator<PolymorphedEntityComponent>();
        while (polymorphedQuery.MoveNext(out var uid, out _))
        {
            // Loop from the current node up the polymorph tree.
            var current = uid;
            var deleting = false;
            while (TryComp<PolymorphedEntityComponent>(current, out var polymorphed) && polymorphed.Parent.HasValue)
            {
                if (deleting || !candidates.Contains(current))
                {
                    deleting = true;
                    candidates.Remove(polymorphed.Parent.Value);
                }

                current = polymorphed.Parent.Value;
            }
        }

        // Now that we have all candidates, that means we also have anyone who would be an active threat too.
        var area = GetThreatArea();
        var ccGrid = GetCentCommGrid();
        _ccHadDefenders = false;
        var query = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState, out var xform))
            if (xform.GridUid == ccGrid && !_state.IsDead(uid, mobState) && !HasComp<ZombieComponent>(uid))
            {
                _ccHadDefenders = true;
                break;
            }

        List<EntityUid> zeds = [];
        var zombieQuery = EntityQueryEnumerator<ZombieComponent, TransformComponent>();
        while (zombieQuery.MoveNext(out var uid, out _, out var xform))
            if (IsActiveThreat(uid, xform, area))
                zeds.Add(uid);

        List<EntityUid> nukeOps = [];
        var nukieQuery = EntityQueryEnumerator<NukeOperativeComponent, TransformComponent>();
        while (nukieQuery.MoveNext(out var uid, out _, out var xform))
            if (!HasComp<ZombieComponent>(uid) && IsActiveThreat(uid, xform, area))
                nukeOps.Add(uid);

        _activeThreats = [.. zeds, .. nukeOps];

        if (_activeThreats.Count > 0)
        {
            UpdateThreatAlerts();
            IsPending = true;
            foreach (var uid in candidates)
                _pendingPacification.Add(uid);
            BroadcastState();
            _chat.DispatchServerAnnouncement(Loc.GetString("eor-pacifism-blocked"), Color.Red);
            return;
        }

        // Pacify the remaining candidates.
        foreach (var uid in candidates)
            Pacify(uid);
    }

    /// <summary>
    /// If someone with <see cref="PreventEorgComponent"/> polymorphs, also apply it to their polymorph.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnPolymorphed(EntityUid uid, PreventEorgComponent comp, PolymorphedEvent ev) => Pacify(ev.NewEntity);

    [SubscribeLocalEvent]
    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            RemoveThreat(args.Target);
            return;
        }

        // Revived on an end grid.
        if (args.OldMobState != MobState.Dead || !IsEnabled || !HasRoundEnded)
            return;

        if (IsActiveThreat(args.Target, Transform(args.Target), GetThreatArea()))
            AddThreat(args.Target);
    }

    [SubscribeLocalEvent]
    private void OnCuffed(Entity<CuffableComponent> ent, ref TargetHandcuffedEvent args)
    {
        if (HasComp<ZombieComponent>(ent)) return; // Zeds need to die godo
        if (_implant.TryGetImplants(ent, out var implants))
            if (implants.Any(uid =>
                {
                    if (!TryComp<SubdermalImplantComponent>(uid, out var implant)) return false;
                    return implant.ImplantAction == FreedomImplantProtoId;
                }))
                return;
        RemoveThreat(ent);
    }

    [SubscribeLocalEvent]
    private void OnEntityTerminating(ref EntityTerminatingEvent args)
    {
        _pacified.Remove(args.Entity);
        RemoveThreat(args.Entity);
    }

    [SubscribeLocalEvent]
    private void OnZombified(ref EntityZombifiedEvent args)
    {
        if (!IsEnabled || !HasRoundEnded) return;
        if (IsActiveThreat(args.Target, Transform(args.Target), GetThreatArea()))
            AddThreat(args.Target);
    }

    [SubscribeLocalEvent]
    private void OnParentChanged(ref EntParentChangedMessage args)
    {
        if (!IsEnabled || !HasRoundEnded)
            return;

        var uid = args.Entity;
        var tracked = _activeThreats.Contains(uid);
        if (!tracked && !HasComp<ZombieComponent>(uid) && !HasComp<NukeOperativeComponent>(uid))
            return;

        var area = GetThreatArea();

        if (tracked)
        {
            if (!IsInThreatArea(uid, args.Transform, area))
                RemoveThreat(uid);
            return;
        }

        if (IsActiveThreat(uid, args.Transform, area))
            AddThreat(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!IsEnabled || !HasRoundEnded)
            return;

        _nukeCheckAccumulator += TimeSpan.FromSeconds(frameTime);
        if (_nukeCheckAccumulator < _nukeCheckInterval)
            return;

        _nukeCheckAccumulator -= _nukeCheckInterval;
        var area = GetThreatArea();
        RefreshNukieThreats(area);
        RefreshZombieThreats(area);
        CheckZombieTakeover();
    }

    private void CheckZombieTakeover()
    {
        if (_cts != null || !_ccHadDefenders || !AllMobsOnEndGridsAreZombies([.. GetEndGrids()]))
            return;

        if (EnsureCentCommStation() is not { } cc)
            return;

        _cSys.DispatchStationAnnouncement(cc, Loc.GetString("eor-zed-takeover"),
            Loc.GetString("eor-cassie"), colorOverride: Color.Red);
        _cts = new CancellationTokenSource();
        Timer.Spawn(_deltaDelay, () => _alertLevel.SetLevel(cc, "delta", true, true, true, true), _cts.Token);
    }

    private EntityUid? EnsureCentCommStation()
    {
        var query = AllEntityQuery<StationCentcommComponent>();
        while (query.MoveNext(out var centcomm))
        {
            if (centcomm.Entity is not { } grid)
                continue;

            if (_station.GetOwningStation(grid) is { } station)
                return station;

            EnsureComp<BecomesStationMidRoundComponent>(grid);
            return _station.InitializeNewStationMidRound(grid, []);
        }

        return null;
    }

    private void RefreshNukieThreats(ThreatArea area)
    {
        if (!IsNukeArmed(area))
        {
            foreach (var uid in _activeThreats.Where(uid => !HasComp<ZombieComponent>(uid)).ToList())
                RemoveThreat(uid);
            return;
        }

        var query = EntityQueryEnumerator<NukeOperativeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
            if (IsActiveThreat(uid, xform, area))
                AddThreat(uid);
    }

    private void RefreshZombieThreats(ThreatArea area)
    {
        foreach (var uid in _activeThreats.Where(HasComp<ZombieComponent>).ToList()
            .Where(uid => !IsInThreatArea(uid, Transform(uid), area)))
            RemoveThreat(uid);

        var query = EntityQueryEnumerator<ZombieComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
            if (IsActiveThreat(uid, xform, area))
                AddThreat(uid);
    }

    private bool IsActiveThreat(EntityUid uid, TransformComponent xform, ThreatArea area)
    {
        if (_state.IsDead(uid) || !IsInThreatArea(uid, xform, area))
            return false;

        if (HasComp<ZombieComponent>(uid))
            return true;

        // Nukies are only a threat if the nuke and disk are near CC too.
        return HasComp<NukeOperativeComponent>(uid)
            && !_cuffable.IsCuffed(uid)
            && IsNukeArmed(area);
    }

    private bool IsInThreatArea(EntityUid uid, TransformComponent xform, ThreatArea area)
    {
        if (!IsNearCentComm(xform, area))
            return false;

        if (!HasComp<ZombieComponent>(uid) || area.EndGrids.Contains(xform.GridUid))
            return true;

        if (area.CcBounds is not { } bounds)
            return false;

        var pos = _xform.GetWorldPosition(xform);
        return (bounds.ClosestPoint(pos) - pos).Length() <= ZombieMaxDriftDistance;
    }

    private static bool IsNearCentComm(TransformComponent xform, ThreatArea area)
        => area.EndGrids.Contains(xform.GridUid) || (area.CcMap is not null && xform.MapUid == area.CcMap);

    private bool IsNukeArmed(ThreatArea area)
        => AnyNearCentComm<NukeComponent>(area) && AnyNearCentComm<NukeDiskComponent>(area);

    private bool AllMobsOnEndGridsAreZombies(List<EntityUid?> endGrids)
    {
        var any = false;
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var xform))
        {
            if (!endGrids.Contains(xform.GridUid) || _state.IsDead(uid, mobState))
                continue;

            if (HasComp<ZombieComponent>(uid))
            {
                any = true;
                continue;
            }

            if (HasComp<ActorComponent>(uid))
                return false;
        }

        return any;
    }

    private EntityUid? GetCentCommGrid()
    {
        var query = AllEntityQuery<StationCentcommComponent>();
        return query.MoveNext(out var centcomm) ? centcomm.Entity : null;
    }

    private ThreatArea GetThreatArea()
    {
        var endGrids = GetEndGrids().ToList();
        if (GetCentCommGrid() is not { } grid)
            return new ThreatArea(endGrids, null, null);

        var xform = Transform(grid);
        Box2? bounds = TryComp<MapGridComponent>(grid, out var mapGrid)
            ? _xform.GetWorldMatrix(xform).TransformBox(mapGrid.LocalAABB)
            : null;

        return new ThreatArea(endGrids, xform.MapUid, bounds);
    }

    private bool AnyNearCentComm<T>(ThreatArea area) where T : IComponent
    {
        var query = EntityQueryEnumerator<T, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
            if (IsNearCentComm(xform, area))
                return true;

        return false;
    }

    [SubscribeNetworkEvent]
    private void OnRequestEorgPreventionState(RequestEorgPreventionStateEvent msg, EntitySessionEventArgs args) =>
        RaiseNetworkEvent(new EorgPreventionStateEvent(IsEnabled, HasRoundEnded, IsPending));

    #endregion
}
