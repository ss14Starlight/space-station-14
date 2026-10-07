using Content.Shared.Stacks;
using Content.Shared.Tools.Components;

namespace Content.Shared.Tools.Systems;

public sealed partial class ToolRefinableSystem
{
    [Dependency] private SharedStackSystem _stackSystem = default!;

    private bool TrySplitRefinementStack(Entity<ToolRefinableComponent> ent, out EntityUid source)
    {
        source = ent.Owner;
        if (!ent.Comp.RefineOneFromStack || !TryComp<StackComponent>(ent, out var stack))
            return true;

        if (stack.Count == 1 && !stack.Unlimited)
            return true;

        if (_stackSystem.Split((ent, stack), 1, Transform(ent).Coordinates) is not { } split)
            return false;

        source = split;
        return true;
    }
}
