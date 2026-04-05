using Content.Shared.PAI;

namespace Content.Shared.Interaction;

public abstract partial class SharedInteractionSystem
{
    private bool IsSlottedPAI(EntityUid actor, EntityUid target)
    {
        return HasComp<PAIComponent>(actor) &&
               _containerSystem.TryGetContainingContainer(actor, out var container) &&
               container != null &&
               container.Owner == target &&
               container.ID == "pai_slot";
    }
}
