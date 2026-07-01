namespace Content.Server._Starlight.UXN.Devices;

public abstract partial class ComponentUxnDevice<T> : UXNDevice where T : IComponent
{
    [Dependency] protected IEntitySystemManager _entSysMan = default!;
    public ComponentUxnDevice() => IoCManager.InjectDependencies(this);
    /// <summary>
    /// this should always be lowercase
    /// </summary>
    public virtual string Id => typeof(T).Name[..^"Component".Length].ToLowerInvariant();

    protected Entity<T> Entity;

    public void Setup(EntityUid euid, T comp)
    {
        Entity = new Entity<T>(euid, comp);
        SetupCore(euid, comp);
    }

    protected abstract void SetupCore(EntityUid euid, T comp);
}
