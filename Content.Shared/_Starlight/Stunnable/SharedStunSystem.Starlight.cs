using Robust.Shared.GameObjects;

// ReSharper disable CheckNamespace
namespace Content.Shared.Stunnable;

public abstract partial class SharedStunSystem
{
    public void ToggleKnockdown(EntityUid entity)
    {
        if (!Exists(entity))
            return;

        ToggleKnockdown((entity, null, null));
    }
}
