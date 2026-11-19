using Content.Server._Starlight.Silicons.Bots;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Content.Shared._Starlight.Silicons.Bots;

namespace Content.Server._Starlight.NPC.HTN.Preconditions;

/// <summary>
/// Met while a MULE is switched on and has been sent somewhere.
/// </summary>
public sealed partial class MuleRunningPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        return _entManager.TryGetComponent<MuleComponent>(owner, out var mule) && MuleSystem.IsRunning(mule);
    }
}
