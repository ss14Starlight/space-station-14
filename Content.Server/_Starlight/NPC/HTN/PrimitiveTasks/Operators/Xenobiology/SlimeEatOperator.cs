using Content.Server._Starlight.Xenobiology;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._Starlight.Xenobiology;

namespace Content.Server._Starlight.NPC.HTN.PrimitiveTasks.Operators.Xenobiology;

public sealed partial class SlimeEatOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    private SlimeSystem _slimeSystem = default!;
    private SlimeBrainSystem _slimeBrainSystem = default!;

    /// <summary>
    /// Target entity to eat.
    /// </summary>
    [DataField("targetKey", required: true)]
    public string TargetKey = string.Empty;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _slimeSystem = sysManager.GetEntitySystem<SlimeSystem>();
        _slimeBrainSystem = sysManager.GetEntitySystem<SlimeBrainSystem>();
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        blackboard.Remove<EntityUid>(TargetKey);
        base.TaskShutdown(blackboard, status);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entMan.TryGetComponent<SlimeComponent>(owner, out var slime))
            return HTNOperatorStatus.Failed;

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entMan) || _entMan.Deleted(target))
            return HTNOperatorStatus.Failed;

        if (!_slimeBrainSystem.IsEdibleBySlimeTest(target))
            return HTNOperatorStatus.Failed;

        if (!_slimeSystem.TryEat((owner, slime), target))
            return HTNOperatorStatus.Failed;

        _slimeBrainSystem.SlimeSuccessfulEat(owner);

        return HTNOperatorStatus.Finished;
    }
}
