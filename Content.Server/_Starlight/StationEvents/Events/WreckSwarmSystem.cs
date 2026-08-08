using System.Numerics;
using Content.Server._Starlight.Salvage.Ruins;
using Content.Server._Starlight.StationEvents.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared._Starlight.Salvage.Ruins;
using Content.Shared.GameTicking.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.StationEvents.Events;

public sealed partial class WreckSwarmSystem : StationEventSystem<WreckSwarmComponent>
{
    #region Dependencies

    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private RuinGeneratorSystem _ruinGenerator = default!;

    #endregion

    #region Fields

    private readonly List<RuinMapPrototype> _ruinMaps = [];

    #endregion

    #region Methods

    protected override void ActiveTick(EntityUid uid, WreckSwarmComponent component, GameRuleComponent gameRule, float frameTime)
    {
        if (_station.GetStations().Count == 0)
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        if (stationEvent.TargetStation is null)
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        if (_station.GetLargestGrid(stationEvent.TargetStation.Value) is not { } grid)
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        var mapId = Transform(grid).MapID;
        var playableArea = _physics.GetWorldAABB(grid);

        var minimumDistance = (playableArea.TopRight - playableArea.Center).Length() + 50f;
        var maximumDistance = minimumDistance + 100f;

        var center = playableArea.Center;

        var angle = RobustRandom.NextAngle();
        var spawnAngle = RobustRandom.NextAngle();

        var offset = angle.RotateVec(new Vector2(((maximumDistance - minimumDistance) * RobustRandom.NextFloat()) + minimumDistance, 0));

        var spawnPosition = new MapCoordinates(center + offset, mapId);

        var wreckMap = _mapSystem.CreateMap();
        var wreckMapXform = Transform(wreckMap);

        if (!TrySpawnWreck(component, wreckMapXform.MapID) ||
            wreckMapXform.ChildCount == 0 ||
            !_mapSystem.TryGetMap(spawnPosition.MapId, out var spawnUid))
        {
            _mapSystem.DeleteMap(wreckMapXform.MapID);
            ForceEndSelf(uid, gameRule);
            return;
        }

        var mapChildren = wreckMapXform.ChildEnumerator;

        while (mapChildren.MoveNext(out var mapChild))
        {
            var wreckXForm = Comp<TransformComponent>(mapChild);
            var localPos = wreckXForm.LocalPosition;

            _transform.SetParent(mapChild, wreckXForm, spawnUid.Value);
            _transform.SetWorldPositionRotation(mapChild, spawnPosition.Position + localPos, spawnAngle, wreckXForm);

            // Fail soft if physics is missing rather than throwing mid-event.
            if (!TryComp<PhysicsComponent>(mapChild, out var physics))
                continue;

            _physics.SetLinearVelocity(mapChild, -offset.Normalized() * component.Velocity, body: physics);
        }

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
            return false;

        var ruinMap = RobustRandom.Pick(ruinMaps);
        var config = ResolveChunkConfig(component);
        var seed = RobustRandom.Next();

        var result = _ruinGenerator.GenerateRuin(ruinMap.MapPath, seed, config);
        if (result == null)
            return false;

        return _ruinGenerator.SpawnRuinGrid(tempMapId, result, seed) != null;
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
