using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Server._Starlight.Salvage.Ruins;
using Content.Server._Starlight.StationEvents.Components;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared._Starlight.Salvage.Ruins;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.StationEvents.Events;

public sealed partial class WreckSwarmSystem : StationEventSystem<WreckSwarmComponent>
{
    #region Dependencies

    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private RuinGeneratorSystem _ruinGenerator = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    #endregion

    #region Fields

    private readonly List<RuinMapPrototype> _ruinMaps = [];
    private List<Entity<MapGridComponent>> _intersectingGrids = [];
    private readonly List<GridLaunchSnapshot> _gridSnapshots = [];

    #endregion

    #region Methods

    protected override void ActiveTick(EntityUid uid, WreckSwarmComponent component, GameRuleComponent gameRule, float frameTime)
    {
        if (!TryComp<StationEventComponent>(uid, out var stationEvent) ||
            stationEvent.TargetStation is not { } station ||
            !TryComp<StationDataComponent>(station, out var stationData) ||
            stationData.Grids.Count == 0)
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        var wreckMap = _mapSystem.CreateMap();
        var wreckMapXform = Transform(wreckMap);

        if (!TrySpawnWreck(component, wreckMapXform.MapID) ||
            !TryCollectWreckGrids(wreckMap, out var footprint) ||
            !TryPlanLaunch((station, stationData), component, footprint, out var plan) ||
            !_mapSystem.TryGetMap(plan.MapId, out var spawnMapUid))
        {
            _mapSystem.DeleteMap(wreckMapXform.MapID);
            ForceEndSelf(uid, gameRule);
            return;
        }

        AttachLooseTempMapEntities(wreckMap, footprint);
        ApplyLaunch(spawnMapUid.Value, plan);
        _mapSystem.DeleteMap(wreckMapXform.MapID);

        if (component.Announcement is { } locId)
            Announce(stationEvent, Loc.GetString(locId), false, null, component.AnnouncementSound);

        ForceEndSelf(uid, gameRule);
    }

    private bool TrySpawnWreck(WreckSwarmComponent component, MapId tempMapId)
    {
        // Admin/debug override: load a fixed grid path instead of generating a ruin chunk.
        if (component.FixedGrid is { } fixedGrid)
            return _loader.TryLoadGrid(tempMapId, fixedGrid, out _);

        var ruinMaps = GetRuinMaps();
        if (ruinMaps.Count == 0)
        {
            Sawmill.Warning("Wreck swarm found no ruin map prototypes; ending without spawn.");
            return false;
        }

        // Prefer a preloaded map when any exist; GenerateRuin still parses on demand if the pick is cold.
        var cachedMaps = new List<RuinMapPrototype>();
        foreach (var map in ruinMaps)
        {
            if (_ruinGenerator.IsMapCached(map.MapPath))
                cachedMaps.Add(map);
        }

        var ruinMap = RobustRandom.Pick(cachedMaps.Count > 0 ? cachedMaps : ruinMaps);
        var config = ResolveChunkConfig(component);
        var seed = RobustRandom.Next();

        var result = _ruinGenerator.GenerateRuin(ruinMap.MapPath, seed, config);
        if (result == null)
            return false;

        return _ruinGenerator.SpawnRuinGrid(tempMapId, result, seed) != null;
    }

    private bool TryCollectWreckGrids(EntityUid wreckMap, [NotNullWhen(true)] out WreckFootprint? footprint)
    {
        footprint = null;
        var children = new List<WreckGridInfo>();
        var mapXform = Transform(wreckMap);
        var enumerator = mapXform.ChildEnumerator;
        Box2? combined = null;

        while (enumerator.MoveNext(out var child))
        {
            if (!TryComp<MapGridComponent>(child, out var grid))
                continue;

            if (!_ruinGenerator.TryPrepareWreckGrid(child))
                continue;

            var xform = Transform(child);
            var info = new WreckGridInfo(child, xform.LocalPosition, xform.LocalRotation, grid.LocalAABB);
            children.Add(info);

            var childBox = Matrix3Helpers.CreateTransform(info.LocalPosition, info.LocalRotation)
                .TransformBox(info.LocalAABB);
            combined = combined == null ? childBox : combined.Value.Union(childBox);
        }

        if (children.Count == 0 || combined == null)
            return false;

        var bounds = combined.Value;
        footprint = new WreckFootprint(
            children,
            bounds.Center,
            (bounds.TopRight - bounds.Center).Length());
        return true;
    }

    private List<RuinMapPrototype> GetRuinMaps()
    {
        _ruinMaps.Clear();
        _ruinMaps.AddRange(_proto.EnumeratePrototypes<RuinMapPrototype>());
        _ruinMaps.Sort((x, y) => string.Compare(x.ID, y.ID, StringComparison.Ordinal));
        return _ruinMaps;
    }

    private RuinChunkConfigPrototype? ResolveChunkConfig(WreckSwarmComponent component)
    {
        var configId = component.ChunkConfig ?? new ProtoId<RuinChunkConfigPrototype>("Medium");
        _proto.TryIndex(configId, out RuinChunkConfigPrototype? config);
        return config;
    }

    #endregion
}
