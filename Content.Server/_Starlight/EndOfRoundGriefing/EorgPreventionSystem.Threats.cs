using System.Linq;
using System.Threading;
using Content.Server._Starlight.Station;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Server.Cuffs;
using Content.Server.Implants;
using Content.Server.Nuke;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Charges.Systems;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nuke;
using Content.Shared.NukeOps;
using Content.Shared.Zombies;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Starlight.EndOfRoundGriefing;

/// <summary>
/// Detects and tracks the threats (zombies and nuclear operatives) that hold off end of round pacification.
/// </summary>
public sealed partial class EorgPreventionSystem
{
    [Dependency] private ChatSystem _cSys = null!;
    [Dependency] private StationSystem _station = null!;
    [Dependency] private MobStateSystem _state = null!;
    [Dependency] private CuffableSystem _cuffable = null!;
    [Dependency] private AlertLevelSystem _alertLevel = null!;
    [Dependency] private SharedTransformSystem _xform = null!;
    [Dependency] private SubdermalImplantSystem _implant = null!;
    [Dependency] private EmergencyShuttleSystem _shuttle = null!;
    [Dependency] private SharedAirlockSystem _airlock = null!;
    [Dependency] private SharedChargesSystem _charges = null!;

    private static readonly EntProtoId FreedomActionProtoId = "ActionActivateFreedomImplant";

    private static readonly TimeSpan NukeCheckInterval = TimeSpan.FromSeconds(1);
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

    private static readonly TimeSpan DeltaDelay = TimeSpan.FromSeconds(3);

    private readonly HashSet<EntityUid> _emergencyAccessAirlocks = [];

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
        var nukies = HasNukieThreat();

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

    /// <summary>
    /// Checks whether anyone alive and not zombified is on the CentComm grid.
    /// </summary>
    private bool FindCentCommDefenders(EntityUid? ccGrid)
    {
        var query = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState, out var xform))
            if (xform.GridUid == ccGrid && !_state.IsDead(uid, mobState) && !HasComp<ZombieComponent>(uid))
                return true;

        return false;
    }

    private List<EntityUid> FindZeds(ThreatArea area)
    {
        List<EntityUid> zeds = [];
        var query = EntityQueryEnumerator<ZombieComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
            if (IsActiveThreat(uid, xform, area))
                zeds.Add(uid);

        return zeds;
    }

    private List<EntityUid> FindNukies(ThreatArea area)
    {
        List<EntityUid> nukeOps = [];
        var query = EntityQueryEnumerator<NukeOperativeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
            if (!HasComp<ZombieComponent>(uid) && IsActiveThreat(uid, xform, area))
                nukeOps.Add(uid);

        return nukeOps;
    }

    private bool HasNukieThreat() => _activeThreats.Any(uid => !HasComp<ZombieComponent>(uid));

    private void RefreshEmergencyAccess(ThreatArea area)
    {
        if (HasNukieThreat())
            EnableEmergencyAccess(area);
        else
            ReleaseEmergencyAccess();
    }

    private void EnableEmergencyAccess(ThreatArea area)
    {
        var query = EntityQueryEnumerator<AirlockComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var airlock, out var xform))
        {
            if (airlock.EmergencyAccess || !area.EndGrids.Contains(xform.GridUid))
                continue;

            _airlock.SetEmergencyAccess((uid, airlock), true);
            if (airlock.EmergencyAccess)
                _emergencyAccessAirlocks.Add(uid);
        }
    }

    private void ReleaseEmergencyAccess() =>
        _emergencyAccessAirlocks.RemoveWhere(uid =>
        {
            if (!TryComp<AirlockComponent>(uid, out var airlock))
                return true;

            _airlock.SetEmergencyAccess((uid, airlock), false);
            return !airlock.EmergencyAccess;
        });

    private List<EntityUid?> GetEndGrids()
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
        if (HasComp<ZombieComponent>(ent) || HasUsableFreedomImplant(ent)) return;
        RemoveThreat(ent);
    }

    private bool IsRestrained(EntityUid uid) => _cuffable.IsCuffed(uid) && !HasUsableFreedomImplant(uid);

    private bool HasUsableFreedomImplant(EntityUid uid)
    {
        if (!_implant.TryGetImplants(uid, out var implants))
            return false;

        return implants.Any(implantUid =>
            TryComp<SubdermalImplantComponent>(implantUid, out var implant)
            && implant.ImplantAction == FreedomActionProtoId
            && implant.Action is { } action
            && _charges.HasCharges(action, 1));
    }

    [SubscribeLocalEvent]
    private void OnZombified(ref EntityZombifiedEvent args)
    {
        if (!IsEnabled || !HasRoundEnded) return;

        if (_activeThreats.Contains(args.Target))
            UpdateThreatAlerts();
        else if (IsActiveThreat(args.Target, Transform(args.Target), GetThreatArea()))
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
        if (_nukeCheckAccumulator < NukeCheckInterval)
            return;

        _nukeCheckAccumulator -= NukeCheckInterval;
        var area = GetThreatArea();
        RefreshNukieThreats(area);
        RefreshZombieThreats(area);
        RefreshEmergencyAccess(area);
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
        Timer.Spawn(DeltaDelay, () => _alertLevel.SetLevel(cc, "delta", true, true, true, true), _cts.Token);
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
        foreach (var uid in _activeThreats.Where(uid => !HasComp<ZombieComponent>(uid)).ToList()
            .Where(uid => !IsActiveThreat(uid, Transform(uid), area)))
            RemoveThreat(uid);

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
            && !IsRestrained(uid)
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
}
