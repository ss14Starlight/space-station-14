using Content.Shared.Charges.Components;
using JetBrains.Annotations;

// ReSharper disable CheckNamespace
namespace Content.Shared.Charges.Systems;

public abstract partial class SharedChargesSystem
{
    /// <summary>
    /// Gets the maximum charges of an entity, or -1 if it has no <see cref="LimitedChargesComponent"/>.
    /// </summary>
    [Pure]
    public int GetMaxCharges(Entity<LimitedChargesComponent?> entity)
    {
        if (!Resolve(entity.Owner, ref entity.Comp, false))
            return -1;

        return entity.Comp.MaxCharges;
    }
}
