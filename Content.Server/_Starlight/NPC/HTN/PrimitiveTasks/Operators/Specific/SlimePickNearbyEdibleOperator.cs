using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared._Starlight.Xenobiology;

namespace Content.Server._Starlight.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class SlimePickNearbyEdibleOperator : HTNOperator
{
    // Before anyone asks, yes this is a slapdash copy of PickNearbyInjectableOperator.cs
    // What do you want from me? I got places to go and food to eat, I'm not going to needlessly reinvent the wheel here
    
    [Dependency] private readonly IEntityManager _entManager = default!;
    private EntityLookupSystem _lookup = default!;
    private SlimeSystem _slime = default!;
    private PathfindingSystem _pathfinding = default!;
    
    /// <summary>
    /// The slime's search range for eating.
    /// </summary>
    [DataField("range", required: true)]
    public float Range = 0;
    
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
    
    /// <summary>
    /// The type of damage to check against when determining if the target has enough non-damage.
    /// </summary>
    [DataField("targetDamageType", required: true)]
    public string TargetDamageType = string.Empty;
    
    /// <summary>
    /// The amount of damage below which the slime will consider the target to be edible.
    /// </summary>
    [DataField("targetDamageThreshold", required: true)]
    public FixedPoint2 TargetDamageThreshold = 0;
    
    public const string TargetDamageTypeKey = "TargetDamageType";
    public const string TargetDamageThresholdKey = "TargetDamageThreshold";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
        _slime = sysManager.GetEntitySystem<SlimeSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        
        if (!_entManager.TryGetComponent<SlimeComponent>(owner, out var slime))
            return (false, null);
        
        var damageQuery = _entManager.GetEntityQuery<DamageableComponent>();
        var slimeQuery = _entManager.GetEntityQuery<SlimeComponent>();
        var mobStateQuery = _entManager.GetEntityQuery<MobStateComponent>();

        foreach (var entity in _lookup.GetEntitiesInRange(owner, Range))
        {
            // Don't cannibalize other slimes
            if (slimeQuery.HasComponent(entity))
                continue;
            
            if (damageQuery.TryGetComponent(entity, out var damage))
            {
                // Don't target entities that aren't mobs
                if (!mobStateQuery.HasComponent(entity))
                    continue;
                
                // Only target entities that are not damaged enough
                if (!(damage.TotalDamage < TargetDamageThreshold))
                    continue;
                
                var pathRange = SharedInteractionSystem.InteractionRange - 1f;
                var path = await _pathfinding.GetPath(owner, entity, pathRange, cancelToken);

                if (path.Result == PathResult.NoPath)
                    continue;

                return (true, new Dictionary<string, object>()
                {
                    {TargetKey, entity},
                    {TargetMoveKey, _entManager.GetComponent<TransformComponent>(entity).Coordinates},
                    {NPCBlackboard.PathfindKey, path},
                    {TargetDamageTypeKey, TargetDamageType},
                    {TargetDamageThresholdKey, TargetDamageThreshold},
                });
            }
        }
        
        return (false, null);
    }
}