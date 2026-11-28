using System.Numerics;
using Content.Server.Beam;
using Content.Server.Mining;
using Content.Server.Station.Systems;
using Content.Server._Starlight.StationGoals;
using Content.Shared._Starlight.MeteorShield;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.MeteorShield;

public sealed partial class MeteorShieldSystem : SharedMeteorShieldSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private BeamSystem _beam = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private StationGoalSystem _goals = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public const string CoverageKey = "MeteorShieldCoverage";

    private const int CoverageSamples = 72;
    private const float ApproachOffset = 2f;

    private static readonly TimeSpan _coverageInterval = TimeSpan.FromSeconds(5);

    private TimeSpan _nextCoverage;
    private int _nextDesignation = 1;
    private readonly Dictionary<EntityUid, float> _coverage = [];
    private readonly Dictionary<MapId, int> _destroyed = [];
    private readonly List<(EntityUid Uid, MapCoordinates Coords, float Range)> _activeSatellites = [];

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<MeteorShieldConsoleComponent>(MeteorShieldConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleOpened);
            subs.Event<MeteorShieldToggleMessage>(OnConsoleToggle);
        });
    }

    [SubscribeLocalEvent]
    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _nextDesignation = 1;
        _coverage.Clear();
        _destroyed.Clear();
    }

    [SubscribeLocalEvent]
    private void OnSatelliteMapInit(Entity<MeteorShieldSatelliteComponent> ent, ref MapInitEvent _)
    {
        ent.Comp.Designation = _nextDesignation++;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _activeSatellites.Clear();

        var query = EntityQueryEnumerator<MeteorShieldSatelliteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var satellite, out var xform))
        {
            if (!satellite.Active)
                continue;

            if (!IsInSpace(uid))
            {
                SetActive((uid, satellite), false);
                continue;
            }

            var coords = _transform.GetMapCoordinates(uid, xform);
            _activeSatellites.Add((uid, coords, satellite.Range));

            if (now < satellite.NextCheck)
                continue;

            satellite.NextCheck = now + satellite.CheckInterval;
            ShootMeteors((uid, satellite), coords);
        }

        if (now >= _nextCoverage)
        {
            _nextCoverage = now + _coverageInterval;
            UpdateCoverage();
        }
    }

    private void ShootMeteors(Entity<MeteorShieldSatelliteComponent> satellite, MapCoordinates coords)
    {
        foreach (var meteor in _lookup.GetEntitiesInRange<MeteorComponent>(coords, satellite.Comp.Range))
        {
            if (TerminatingOrDeleted(meteor)
                || !_interaction.InRangeUnobstructed(satellite.Owner, meteor.Owner, satellite.Comp.Range, CollisionGroup.Impassable))
                continue;

            _beam.TryCreateBeam(satellite, meteor, satellite.Comp.BeamPrototype);
            QueueDel(meteor);
            _destroyed[coords.MapId] = _destroyed.GetValueOrDefault(coords.MapId) + 1;
        }
    }

    private void UpdateCoverage()
    {
        var stations = EntityQueryEnumerator<StationDataComponent>();
        while (stations.MoveNext(out var station, out var data))
        {
            var coverage = ComputeCoverage((station, data));
            _coverage[station] = coverage;
            _goals.ReportProgress(station, CoverageKey, MathF.Floor(coverage * 100f));
        }

        var consoles = EntityQueryEnumerator<MeteorShieldConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
            UpdateConsole((uid, console));
    }

    private float ComputeCoverage(Entity<StationDataComponent> station)
    {
        if (_station.GetLargestGrid((station, station.Comp)) is not { } gridUid
            || !TryComp<MapGridComponent>(gridUid, out var grid))
            return 0f;

        var mapId = Transform(gridUid).MapID;
        var satellites = _activeSatellites.FindAll(s => s.Coords.MapId == mapId);
        if (satellites.Count == 0)
            return 0f;

        var bounds = _transform.GetWorldMatrix(gridUid).TransformBox(grid.LocalAABB);
        var center = bounds.Center;
        var radius = (bounds.Size.Length() / 2f) + 4f;

        var sampled = 0;
        var covered = 0;
        for (var i = 0; i < CoverageSamples; i++)
        {
            var direction = new Angle(Math.Tau * i / CoverageSamples).ToVec();
            if (!TryFindHull(gridUid, grid, center, direction, radius, out var hullDistance))
                continue;

            sampled++;
            var approach = new MapCoordinates(center + (direction * (hullDistance + ApproachOffset)), mapId);
            foreach (var (_, coords, range) in satellites)
            {
                if ((coords.Position - approach.Position).Length() > range
                    || !_interaction.InRangeUnobstructed(coords, approach, range, CollisionGroup.Impassable))
                    continue;

                covered++;
                break;
            }
        }

        return sampled == 0 ? 0f : (float) covered / sampled;
    }

    private bool TryFindHull(EntityUid gridUid, MapGridComponent grid, Vector2 center, Vector2 direction, float radius, out float distance)
    {
        for (distance = radius; distance > 0f; distance -= 0.5f)
        {
            if (_map.TryGetTileRef(gridUid, grid, center + (direction * distance), out var tile) && !tile.Tile.IsEmpty)
                return true;
        }

        return false;
    }

    #region Console

    private void OnConsoleOpened(Entity<MeteorShieldConsoleComponent> ent, ref BoundUIOpenedEvent args)
        => UpdateConsole(ent);

    private void OnConsoleToggle(Entity<MeteorShieldConsoleComponent> ent, ref MeteorShieldToggleMessage args)
    {
        if (!TryGetEntity(args.Satellite, out var uid)
            || !TryComp<MeteorShieldSatelliteComponent>(uid, out var satellite)
            || Transform(uid.Value).MapID != Transform(ent).MapID)
            return;

        TryToggle((uid.Value, satellite), args.Actor);
        UpdateConsole(ent);
    }

    private void UpdateConsole(Entity<MeteorShieldConsoleComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, MeteorShieldConsoleUiKey.Key))
            return;

        var consoleMap = Transform(ent).MapID;
        var state = new MeteorShieldConsoleUiState
        {
            Destroyed = _destroyed.GetValueOrDefault(consoleMap),
        };

        var center = _transform.GetWorldPosition(ent);
        if (_station.GetOwningStation(ent) is { } station)
        {
            state.Coverage = _coverage.GetValueOrDefault(station);
            if (_goals.TryGetActiveTarget(station, CoverageKey, out var target))
                state.GoalCoverage = target / 100f;

            if (_station.GetLargestGrid(station) is { } grid && TryComp<MapGridComponent>(grid, out var gridComp))
                center = _transform.GetWorldMatrix(grid).TransformBox(gridComp.LocalAABB).Center;
        }

        var query = EntityQueryEnumerator<MeteorShieldSatelliteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var satellite, out var xform))
        {
            if (xform.MapID != consoleMap)
                continue;

            state.Satellites.Add(new MeteorShieldSatelliteState
            {
                Entity = GetNetEntity(uid),
                Designation = Designation(satellite),
                Active = satellite.Active,
                InSpace = IsInSpace(uid),
                Distance = (_transform.GetWorldPosition(xform) - center).Length(),
            });
        }

        state.Satellites.Sort((a, b) => a.Designation.Length != b.Designation.Length
            ? a.Designation.Length.CompareTo(b.Designation.Length)
            : string.Compare(a.Designation, b.Designation, StringComparison.Ordinal));
        _ui.SetUiState(ent.Owner, MeteorShieldConsoleUiKey.Key, state);
    }

    #endregion
}
