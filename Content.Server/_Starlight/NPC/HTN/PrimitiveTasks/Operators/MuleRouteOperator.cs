using System.Threading;
using System.Threading.Tasks;
using Content.Server._Starlight.Silicons.Bots;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._Starlight.Silicons.Bots;

namespace Content.Server._Starlight.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Plans a MULE's route to its destination.
/// </summary>
public sealed partial class MuleRouteOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private MuleSystem _mule = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _mule = sysManager.GetEntitySystem<MuleSystem>();
    }

    public override Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_entManager.TryGetComponent<MuleComponent>(owner, out var mule))
            return Task.FromResult<(bool, Dictionary<string, object>?)>((false, null));

        // No way there yet: it waits and tries again rather than giving up.
        if (!_mule.PlanRoute((owner, mule)))
            return Task.FromResult<(bool, Dictionary<string, object>?)>((false, null));

        return Task.FromResult<(bool, Dictionary<string, object>?)>((true, null));
    }
}
