using Content.Server._Starlight.Silicons.Bots;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Systems;
using Content.Shared._Starlight.Silicons.Bots;

namespace Content.Server._Starlight.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Steers a MULE through the waypoints of its planned route, a short leg at a time.
/// </summary>
public sealed partial class MuleDriveOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private MuleSystem _mule = default!;
    private NPCSteeringSystem _steering = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _mule = sysManager.GetEntitySystem<MuleSystem>();
        _steering = sysManager.GetEntitySystem<NPCSteeringSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_entManager.TryGetComponent<MuleComponent>(owner, out var mule))
            return HTNOperatorStatus.Failed;

        if (mule.Waypoints.Count == 0)
            return HTNOperatorStatus.Finished;

        var target = mule.Waypoints[0];
        if (!_entManager.TryGetComponent<NPCSteeringComponent>(owner, out var steering) || steering.Coordinates != target)
        {
            steering = _steering.Register(owner, target);
            steering.Range = _mule.WaypointRange((owner, mule), target);
            // Register keeps the status from the last point, which would count this one as reached.
            steering.Status = SteeringStatus.Moving;
            return HTNOperatorStatus.Continuing;
        }

        switch (steering.Status)
        {
            case SteeringStatus.InRange:
                mule.Waypoints.RemoveAt(0);
                return mule.Waypoints.Count == 0 ? HTNOperatorStatus.Finished : HTNOperatorStatus.Continuing;
            case SteeringStatus.NoPath:
                // Something changed on the way, so plan the route again.
                mule.Waypoints.Clear();
                return HTNOperatorStatus.Failed;
            default:
                return HTNOperatorStatus.Continuing;
        }
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);
        _steering.Unregister(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
    }
}
