using System.Threading;
using System.Threading.Tasks;
using Content.Server._Starlight.Xenobiology;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared._Starlight.Xenobiology;
using Content.Shared.Interaction;

namespace Content.Server._Starlight.NPC.HTN.PrimitiveTasks.Operators.Xenobiology;

public sealed partial class SlimeTargetKnownEdibleTargetOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private SlimeBrainSystem _slimeBrainSystem = default!;
    private EntityLookupSystem _lookup = default!;
    private PathfindingSystem _pathfinding = default!;

    /// <summary>
    /// Target entity to eat.
    /// </summary>
    [DataField("targetKey", required: true)]
    public string TargetKey = string.Empty;

    /// <summary>
    /// Target entitycoordinates to move to.
    /// </summary>
    [DataField("targetMoveKey", required: true)]
    public string TargetMoveKey = string.Empty;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _slimeBrainSystem = sysManager.GetEntitySystem<SlimeBrainSystem>();
        _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<SlimeComponent>(owner, out var slime))
            return (false, null);

        foreach (var entity in _lookup.GetEntitiesInRange(owner, _slimeBrainSystem.FoodSearchRange))
        {
            if (!_slimeBrainSystem.AcquireTargetFoods().Contains(entity)) continue;

            var pathRange = SharedInteractionSystem.InteractionRange - 1f;
            var path = await _pathfinding.GetPath(owner, entity, pathRange, cancelToken);

            if (path.Result == PathResult.NoPath)
                continue;

            return (true, new Dictionary<string, object>()
            {
                {TargetKey, entity},
                {TargetMoveKey, _entManager.GetComponent<TransformComponent>(entity).Coordinates},
                {NPCBlackboard.PathfindKey, path},
            });
        }

        return (false, null);
    }
}
