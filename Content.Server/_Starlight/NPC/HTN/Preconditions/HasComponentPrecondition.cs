using Robust.Shared.Prototypes;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.HTN;

namespace Content.Server._Starlight.NPC.HTN.Preconditions;

/// <summary>
/// Does the owner have all of the given components. Use invert to require that it has none of them.
/// </summary>
public sealed partial class HasComponentPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    [DataField("components", required: true)]
    public ComponentRegistry Components = new();

    [DataField]
    public bool Invert;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var hasAll = true;

        foreach (var entry in Components.Values)
        {
            if (!_entManager.HasComponent(owner, entry.Component.GetType()))
            {
                hasAll = false;
                break;
            }
        }

        return hasAll ^ Invert;
    }
}
