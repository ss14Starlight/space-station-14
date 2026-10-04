using Content.Shared.Hands.Components;

namespace Content.Shared.Hands.EntitySystems;

public abstract partial class SharedHandsSystem
{
    public bool TryCycleActiveHand(Entity<HandsComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false)
            || ent.Comp.ActiveHandId is not { } active
            || ent.Comp.Hands.Count < 2)
            return false;

        var currentIndex = ent.Comp.SortedHands.IndexOf(active);
        if (currentIndex < 0)
            return false;

        var nextIndex = (currentIndex + 1) % ent.Comp.SortedHands.Count;
        return TrySetActiveHand(ent, ent.Comp.SortedHands[nextIndex]);
    }

    public bool TryCycleActiveHandReverse(Entity<HandsComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false)
            || ent.Comp.ActiveHandId is not { } active
            || ent.Comp.Hands.Count < 2)
            return false;

        var currentIndex = ent.Comp.SortedHands.IndexOf(active);
        if (currentIndex < 0)
            return false;

        var previousIndex = (currentIndex - 1 + ent.Comp.SortedHands.Count) % ent.Comp.SortedHands.Count;
        return TrySetActiveHand(ent, ent.Comp.SortedHands[previousIndex]);
    }
}
