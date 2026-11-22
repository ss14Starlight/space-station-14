using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Interactions;

/// <summary>
/// Releases whatever the NPC is currently pulling.
/// </summary>
public sealed partial class StopPullingOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private PullingSystem _pulling = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pulling = sysManager.GetEntitySystem<PullingSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (_pulling.GetPulling(owner) is { } pulled &&
            _entManager.TryGetComponent<PullableComponent>(pulled, out var pullable))
        {
            _pulling.TryStopPull(pulled, pullable, owner);
        }

        return HTNOperatorStatus.Finished;
    }
}