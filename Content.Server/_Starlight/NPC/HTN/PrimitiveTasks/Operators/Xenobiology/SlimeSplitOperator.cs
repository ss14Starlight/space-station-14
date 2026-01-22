using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._Starlight.Xenobiology;

namespace Content.Server._Starlight.NPC.HTN.PrimitiveTasks.Operators.Xenobiology;

public sealed partial class SlimeSplitOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    private SlimeSystem _slimeSystem = default!;
    
    /// <summary>
    /// The amount of slimes to split into
    /// </summary>
    [DataField("splitAmount", required: true)]
    public int SplitAmount = 0!;
    
    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _slimeSystem = sysManager.GetEntitySystem<SlimeSystem>();
    }
    
    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        
        if (!_entMan.TryGetComponent<SlimeComponent>(owner, out var slime))
            return HTNOperatorStatus.Failed;

        if (!_slimeSystem.QueueSlimeSplit((owner, slime), SplitAmount))
            return HTNOperatorStatus.Failed;

        return HTNOperatorStatus.Finished;
    }
}