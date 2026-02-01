namespace Content.Shared._Starlight.IoC;

// TODO STARLIGHT delete this when SharedVendingMachineSystem.OnVendingGetState is fixed
[Obsolete]
public abstract class SharedSLIoCSystem : EntitySystem
{
    [Obsolete]
    public virtual void ServerInitIoC()
    {
    }
}
