using Robust.Shared.GameObjects;

namespace Content.Shared._Starlight.UXN.Devices;

public abstract class ComponentUxnDevice<T> : UXNDevice where T : IComponent
{
    protected Entity<T> Entity;

    public void Setup(EntityUid euid, T comp)
    {
        Entity = new Entity<T>(euid, (T)comp);
        SetupCore(euid, comp);
    }

    protected abstract void SetupCore(EntityUid euid, T comp);
}