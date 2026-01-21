using System.Threading;
using System.Threading.Tasks;
using Content.Server._Starlight.Xenobiology;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared._Starlight.Xenobiology;
using Content.Shared.Interaction;
using Content.Shared.Tag;

namespace Content.Server._Starlight.NPC.HTN.PrimitiveTasks.Operators.Xenobiology;

public sealed partial class SlimeLocateFeedingSpotOperator : HTNOperator
{
    /*
     * This locates a feeding spot for the slime to go to and directs them there.
     * Should be used when there are no nearby food sources.
     */
    
    [Dependency] private readonly IEntityManager _entManager = default!;

    private SlimeBrainSystem _slimeBrainSystem = default!;
    private PathfindingSystem _pathfinding = default!;
    
    /// <summary>
    /// Target entitycoordinates to move to.
    /// </summary>
    [DataField("targetMoveKey", required: true)]
    public string TargetMoveKey = string.Empty;
    
    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _slimeBrainSystem = sysManager.GetEntitySystem<SlimeBrainSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
    }
    
    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        
        if (!_entManager.TryGetComponent<SlimeComponent>(owner, out var slime))
            return (false, null);
        
        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var slimeTransform))
            return (false, null);

        foreach (var spot in _slimeBrainSystem.AcquireFeedingSpots())
        {
            var pathRange = SharedInteractionSystem.InteractionRange - 1f;
            var path = await _pathfinding.GetPath(owner, slimeTransform.Coordinates, spot, pathRange, cancelToken);

            if (path.Result == PathResult.NoPath)
                continue;

            return (true, new Dictionary<string, object>()
            {
                {TargetMoveKey, slimeTransform.Coordinates},
                {NPCBlackboard.PathfindKey, path},
            });
        }
        
        return (false, null);
    }
}