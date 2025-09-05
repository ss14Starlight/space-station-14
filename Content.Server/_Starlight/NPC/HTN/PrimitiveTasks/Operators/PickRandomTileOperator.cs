using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.NPC.HTN.PrimitiveTasks.Operators;

public sealed partial class PickRandomTileOperator : HTNOperator
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    private TurfSystem _turf = default!;
    private SharedMapSystem _map = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _turf = sysManager.GetEntitySystem<TurfSystem>();
        _map = sysManager.GetEntitySystem<SharedMapSystem>();
    }

    [DataField]
    public string TargetKey = "TargetTile";

    [DataField]
    public HashSet<ProtoId<ContentTileDefinition>> Tiles = new();

    [DataField]
    public bool Invert = false;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var ownerPos = _entityManager.GetComponent<TransformComponent>(owner);
        if (!_entityManager.TryGetComponent<MapGridComponent>(ownerPos.GridUid, out var mapGrid))
            return (false, null);
        var tileIds = Tiles.Select(item => item.Id).ToHashSet();
        var pos = ownerPos.LocalPosition;
        var aabb = new Box2(pos.X - 3,
            pos.Y - 3,
            pos.X + 3,
            pos.Y + 3
        );
        bool IsValidTile(TileRef tile) => tileIds.Contains(_turf.GetContentTileDefinition(tile).ID) ^ Invert;
        var tileEnumerator = _map.GetLocalTilesEnumerator(ownerPos.GridUid.Value, mapGrid, aabb, true, IsValidTile);

        List<(Vector2, float)> coordinates = new();
        while (tileEnumerator.MoveNext(out var tile))
        {
            var tileCenter = tile.GridIndices + mapGrid.TileSizeHalfVector;
            var direction = tileCenter - pos;
            var len = direction.CompareLengthTo(pos);
            if (len < 3)
            {
                coordinates.Add((tileCenter, len));
            }
        }
        coordinates.Sort(delegate ((Vector2, float) left, (Vector2, float) right)
        {
            if (left.Item2 < right.Item2)
                return -1;
            return left.Item2 > right.Item2 ? 1 : 0;
        });

        return (true, new Dictionary<string, object>()
        {
            {TargetKey, new EntityCoordinates(owner,coordinates[0].Item1)}
        });
    }

}
