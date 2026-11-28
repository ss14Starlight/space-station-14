using Content.Server._Starlight.StationGoals;
using Content.Shared._Starlight.StationGoals;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.BluespaceArtillery;

public sealed partial class BluespaceArtillerySystem
{
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private StationGoalSystem _goals = default!;

    public static readonly ProtoId<StationGoalPrototype> TestGoal = "BluespaceArtillery";
    public const string TestSiteDestroyedKey = "BluespaceArtilleryTestSiteDestroyed";

    private const string TestSitePrototype = "BluespaceArtilleryTestSite";
    private const string TestSiteFloor = "Plating";
    private const string TestSiteWall = "WallReinforced";

    private const int TestSiteRadius = 4;

    private static readonly TimeSpan TestSiteDeleteDelay = TimeSpan.FromSeconds(10);

    private readonly Dictionary<EntityUid, TimeSpan> _rangesToDelete = new();

    [SubscribeLocalEvent]
    private void OnGoalActivated(ref StationGoalActivatedEvent ev)
    {
        if (ev.Goal == TestGoal)
            SpawnTestSite(ev.Station);
    }

    [SubscribeLocalEvent]
    private void OnGoalFailed(ref StationGoalFailedEvent ev)
    {
        if (ev.Goal != TestGoal)
            return;

        var query = EntityQueryEnumerator<BluespaceArtilleryTestSiteComponent, TransformComponent>();
        while (query.MoveNext(out _, out var site, out var xform))
        {
            if (site.Station == ev.Station && xform.MapUid is { } map)
                QueueDel(map);
        }
    }

    /// <summary>
    /// Spawns test site for BSA's station goal.
    /// </summary>
    public EntityUid SpawnTestSite(EntityUid station)
    {
        var map = _map.CreateMap(out var mapId);
        _metaData.SetEntityName(map, Loc.GetString("bsa-test-site-map-name"));

        var grid = _map.CreateGridEntity(mapId);
        _metaData.SetEntityName(grid, Loc.GetString("bsa-test-site-grid-name"));

        var floor = new Tile(_tileDefs[TestSiteFloor].TileId);
        var tiles = new List<(Vector2i, Tile)>();
        for (var x = -TestSiteRadius; x <= TestSiteRadius; x++)
        {
            for (var y = -TestSiteRadius; y <= TestSiteRadius; y++)
                tiles.Add((new Vector2i(x, y), floor));
        }

        _map.SetTiles(grid, tiles);

        for (var x = -TestSiteRadius; x <= TestSiteRadius; x++)
        {
            for (var y = -TestSiteRadius; y <= TestSiteRadius; y++)
            {
                if (Math.Abs(x) == TestSiteRadius || Math.Abs(y) == TestSiteRadius)
                    SpawnAtPosition(TestSiteWall, _map.GridTileToLocal(grid, grid.Comp, new Vector2i(x, y)));
            }
        }

        var target = SpawnAtPosition(TestSitePrototype, _map.GridTileToLocal(grid, grid.Comp, Vector2i.Zero));
        EnsureComp<BluespaceArtilleryTestSiteComponent>(target).Station = station;
        return target;
    }

    private void OnTestSiteHit(Entity<BluespaceArtilleryTestSiteComponent> site)
    {
        if (site.Comp.Hit || Transform(site).MapUid is not { } map)
            return;

        site.Comp.Hit = true;
        _rangesToDelete[map] = _timing.CurTime + TestSiteDeleteDelay;
        _goals.ReportProgress(site.Comp.Station, TestSiteDestroyedKey);
    }

    private void UpdateTestSites(TimeSpan now)
    {
        if (_rangesToDelete.Count == 0)
            return;

        foreach (var (map, deleteAt) in _rangesToDelete)
        {
            if (now < deleteAt)
                continue;

            _rangesToDelete.Remove(map);
            QueueDel(map);
        }
    }
}
