using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared._Starlight.Xenobiology;

namespace Content.Server._Starlight.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class SlimeEatOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    private SlimeSystem _slime = default!;

    /// <summary>
    /// Target entity to eat.
    /// </summary>
    [DataField("targetKey", required: true)]
    public string TargetKey = string.Empty;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _slime = sysManager.GetEntitySystem<SlimeSystem>();
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);
        blackboard.Remove<EntityUid>(TargetKey);
        blackboard.Remove<string>(SlimePickNearbyEdibleOperator.TargetDamageTypeKey);
        blackboard.Remove<FixedPoint2>(SlimePickNearbyEdibleOperator.TargetDamageThresholdKey);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entMan) || _entMan.Deleted(target))
            return HTNOperatorStatus.Failed;
        
        if (!blackboard.TryGetValue<string>(SlimePickNearbyEdibleOperator.TargetDamageTypeKey, out var targetDamageType, _entMan) ||
            !blackboard.TryGetValue<FixedPoint2>(SlimePickNearbyEdibleOperator.TargetDamageThresholdKey, out var targetDamageThreshold, _entMan))
            return HTNOperatorStatus.Failed;
        
        // No cannibalism of other slimes
        if (_entMan.HasComponent<SlimeComponent>(target))
            return HTNOperatorStatus.Failed;

        if (!_entMan.TryGetComponent<SlimeComponent>(owner, out var slime))
            return HTNOperatorStatus.Failed;

        if (!_entMan.TryGetComponent<DamageableComponent>(target, out var damage))
            return HTNOperatorStatus.Failed;
        
        // Don't target entities that aren't mobs
        if (!_entMan.HasComponent<MobStateComponent>(target))
            return HTNOperatorStatus.Failed;
        
        if (!(damage.TotalDamage < targetDamageThreshold))
            return HTNOperatorStatus.Failed;

        if (!_slime.TryEat((owner, slime), target))
            return HTNOperatorStatus.Failed;

        return HTNOperatorStatus.Finished;
    }
}