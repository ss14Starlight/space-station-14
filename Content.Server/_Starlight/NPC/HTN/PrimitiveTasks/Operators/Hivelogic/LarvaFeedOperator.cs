using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server._Starlight.XenoHive;
using Content.Server._Starlight.XenoHive.Components;

namespace Content.Server._Starlight.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Starts feeding on the entity in the target key and waits for it to finish.
/// </summary>
public sealed partial class LarvaFeedOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private LarvaFeedSystem _feed = default!;

    [DataField("targetKey", required: true)]
    public string TargetKey = string.Empty;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _feed = sysManager.GetEntitySystem<LarvaFeedSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) ||
            !_feed.CanFeed(owner, target))
            return (false, null);

        return (true, null);
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            _feed.TryStartFeed(owner, target);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // A completed feed deletes the feeder, so this only sees "still feeding" or "stopped".
        return _entManager.TryGetComponent<LarvaFeedComponent>(owner, out var comp) && comp.Target != null
            ? HTNOperatorStatus.Continuing
            : HTNOperatorStatus.Failed;
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);
        _feed.CancelFeed(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
    }
}
