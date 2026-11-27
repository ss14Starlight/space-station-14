using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Finds the nearest reachable entity tagged XenoHealer and pathfinds to it.
/// Writes the healer and its coordinates to the blackboard; follow with a MoveToOperator.
/// </summary>
public sealed partial class PathToXenoHealerOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private PathfindingSystem _pathfinding = default!;

    [DataField("tag")]
    public ProtoId<TagPrototype> Tag = "XenoHealer";

    [DataField("targetKey")]
    public string TargetKey = "HealerTarget";

    [DataField("targetCoordinates")]
    public string TargetCoordinates = "HealerCoordinates";

    [DataField("pathfindKey")]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    [DataField("rangeKey")]
    public string RangeKey = "MovementRange";

    /// <summary>How many of the nearest healers to try pathfinding to.</summary>
    [DataField("candidates")]
    public int Candidates = 3;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // Where the Xeno will be at this point in the plan (the victim, if a MoveTo ran earlier)
        if (!blackboard.TryGetValue<EntityCoordinates>(NPCBlackboard.OwnerCoordinates, out var start, _entManager))
            return (false, null);

        var range = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);

        // Collect tagged healers, nearest first
        var found = new List<(EntityUid Uid, EntityCoordinates Coords, float Distance)>();
        var query = _entManager.EntityQueryEnumerator<TagComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var tags, out var xform))
        {
            if (uid == owner)
                continue;

            if (!tags.Tags.AsReadOnly().Contains(Tag))
                continue;

            if (!start.TryDistance(_entManager, xform.Coordinates, out var distance))
                continue; // different map

            found.Add((uid, xform.Coordinates, distance));
        }

        // First one we can actually path to wins
        foreach (var (uid, coords, _) in found.OrderBy(x => x.Distance).Take(Candidates))
        {
            var path = await _pathfinding.GetPath(
                owner,
                start,
                coords,
                range,
                cancelToken,
                _pathfinding.GetFlags(blackboard));

            if (path.Result != PathResult.Path)
                continue;

            return (true, new Dictionary<string, object>()
            {
                { TargetKey, uid },
                { TargetCoordinates, coords },
                { PathfindKey, path }
            });
        }

        return (false, null);
    }
}
