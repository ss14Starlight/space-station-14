using Content.Shared.Movement.Pulling.Components;

namespace Content.Server._Starlight.NPC.HTN.Preconditions;

/// <summary>
/// True unless another HTN NPC is currently pulling something.
/// </summary>
public sealed partial class NoOtherPullerPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var query = _entManager.EntityQueryEnumerator<ActivePullerComponent, HTNComponent>();

        while (query.MoveNext(out var uid, out _, out _))
        {
            if (uid != owner)
                return false;
        }

        return true;
    }
}
