using Content.Server._Starlight.Silicons.Bots;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._Starlight.Silicons.Bots;

namespace Content.Server._Starlight.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Unloads or loads a MULE where it's arrived, then sends it home or lets it wait.
/// </summary>
public sealed partial class MuleArriveOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private MuleSystem _mule = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _mule = sysManager.GetEntitySystem<MuleSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_entManager.TryGetComponent<MuleComponent>(owner, out var mule))
            return HTNOperatorStatus.Failed;

        _mule.Arrive((owner, mule));
        return HTNOperatorStatus.Finished;
    }
}
