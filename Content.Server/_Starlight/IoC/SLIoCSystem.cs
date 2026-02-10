using Content.Shared._Starlight.IoC;

namespace Content.Server._Starlight.IoC;

[Obsolete]
public sealed class SLIoCSystem : SharedSLIoCSystem
{
    [Dependency] private readonly IDependencyCollection _dependency = default!;

    public override void ServerInitIoC()
    {
        // TODO STARLIGHT delete this when SharedVendingMachineSystem.OnVendingGetState is fixed
#if DEBUG
        if (IoCManager.Instance != null)
            return;

        IoCManager.InitThread(_dependency);
#endif
    }
}
