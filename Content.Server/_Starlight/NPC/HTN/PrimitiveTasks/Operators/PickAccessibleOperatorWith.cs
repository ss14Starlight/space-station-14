using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Pathfinding;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Picks a nearby accessible coordinate on a tile that contains a specific entity prototype
/// (e.g. a spider web). The target entity must not block movement.
/// </summary>
public sealed partial class PickWebTileOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IComponentFactory _factory = default!;

    private PathfindingSystem _pathfinding = default!;
    private EntityLookupSystem _lookup = default!;

    /// <summary>
    /// Blackboard key that holds the max search range (float).
    /// </summary>
    [DataField("rangeKey", required: true)]
    public string RangeKey = string.Empty;

    /// <summary>
    /// Blackboard key where the chosen <see cref="EntityCoordinates"/> will be stored.
    /// </summary>
    [DataField("targetCoordinates")]
    public string TargetCoordinates = "TargetCoordinates";

    /// <summary>
    /// Where the pathfinding result will be stored (removed after execution).
    /// </summary>
    [DataField("pathfindKey")]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    /// <summary>
    /// Prototype ID of the entity that must be present on the destination tile.
    /// Example: "SpiderWeb"
    /// </summary>
    [DataField("tileEntityPrototype")]
    public string TileEntityPrototype = string.Empty;

    [DataField("tileEntityComponent")]
    public string TileEntityComponent = string.Empty;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
        _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
    }

    /// <inheritdoc/>
    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        blackboard.TryGetValue<float>(RangeKey, out var maxRange, _entManager);
        if (maxRange == 0f)
            maxRange = 7f;

        var ownerXform = _entManager.GetComponent<TransformComponent>(owner);

        var candidates = new List<EntityCoordinates>();

        var nearby = _lookup.GetEntitiesInRange(ownerXform.Coordinates, maxRange);
        foreach (var uid in nearby)
        {
            if (uid == owner)
                continue;

            if (!_entManager.TryGetComponent<MetaDataComponent>(uid, out var meta))
                continue;

            if (TileEntityPrototype != string.Empty && meta.EntityPrototype?.ID != TileEntityPrototype)
                continue;

            if (TileEntityComponent != string.Empty && (!_factory.TryGetRegistration(TileEntityComponent, out var registration) || !_entManager.HasComponent(uid, registration)))
                continue;

            if (!_entManager.TryGetComponent<TransformComponent>(uid, out var xform))
                continue;

            candidates.Add(xform.Coordinates);
        }

        if (candidates.Count == 0)
            return (false, null);

        _random.Shuffle(candidates);

        foreach (var coord in candidates)
        {
            if (cancelToken.IsCancellationRequested)
                break;

            var path = await _pathfinding.GetPath(
                owner,
                ownerXform.Coordinates,
                coord,
                0f,
                cancelToken,
                flags: _pathfinding.GetFlags(blackboard));

            if (path.Result != PathResult.Path)
                continue;

            return (true, new Dictionary<string, object>
            {
                { TargetCoordinates, coord },
                { PathfindKey, path },
            });
        }

        return (false, null);
    }
}
