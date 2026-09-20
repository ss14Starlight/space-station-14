using Content.Shared.Wieldable.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Wieldable;

public abstract partial class SharedWieldableSystem
{
    /// <summary>
    /// Forces the wielded state without requiring the item to be held in hands.
    /// Used by effects that animate an item without a wielder (e.g. a revenant misfiring a gun).
    /// </summary>
    public void ForceWielded(Entity<WieldableComponent?> ent, bool wielded)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.Wielded == wielded)
            return;

        SetWielded((ent, ent.Comp), wielded);
    }
}
