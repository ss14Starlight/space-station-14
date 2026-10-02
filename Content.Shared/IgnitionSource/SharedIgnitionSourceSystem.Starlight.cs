namespace Content.Shared.IgnitionSource;

public abstract partial class SharedIgnitionSourceSystem : EntitySystem
{
    /// <summary>
    /// Allows the server to maintain an active set without polling every ignition source.
    /// </summary>
    protected virtual void OnIgnitionStateChanged(Entity<IgnitionSourceComponent> ent)
    {
    }
}
