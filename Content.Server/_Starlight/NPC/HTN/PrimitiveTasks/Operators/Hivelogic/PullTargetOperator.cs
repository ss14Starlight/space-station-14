using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Movement.Pulling.Systems;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Interactions;

public sealed partial class PullTargetOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private PullingSystem _pulling = default!;

    [DataField("targetKey", required: true)]
    public string TargetKey = string.Empty;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pulling = sysManager.GetEntitySystem<PullingSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) ||
            !_pulling.CanPull(owner, target))
            return (false, null);

        return (true, null);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            return HTNOperatorStatus.Failed;

        return _pulling.TryStartPull(owner, target)
            ? HTNOperatorStatus.Finished
            : HTNOperatorStatus.Failed;
    }
}