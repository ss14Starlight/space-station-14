using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Content.Shared.Nutrition.Components;

namespace Content.Server._Starlight.NPC.HTN.Preconditions;

/// <summary>
/// A copy of HungryPrecondition.cs with the condition flipped, because it's a sealed class.
/// </summary>
public sealed partial class NotHungryPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public HungerThreshold MinHungerState = HungerThreshold.Starving;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<EntityUid>(NPCBlackboard.Owner, out var owner, _entManager))
        {
            return false;
        }

        return _entManager.TryGetComponent<HungerComponent>(owner, out var hunger) ? hunger.CurrentThreshold > MinHungerState : false;
    }
}
