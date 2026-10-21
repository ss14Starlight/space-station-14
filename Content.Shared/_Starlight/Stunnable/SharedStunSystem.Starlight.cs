using Robust.Shared.GameObjects;

// ReSharper disable CheckNamespace
namespace Content.Shared.Stunnable;

public abstract partial class SharedStunSystem
{
    /// <summary>
    /// Toggles the knockdown state of an entity if it exists.
    /// </summary>
    /// <remarks>
    /// Does nothing if the entity does not exist; otherwise, delegates to the tuple overload with its components
    /// unresolved.
    /// </remarks>
    /// <param name="entity">The entity whose knockdown state to toggle.</param>
    public void ToggleKnockdown(EntityUid entity)
    {
        if (!Exists(entity))
            return;

        ToggleKnockdown((entity, null, null));
    }
}
