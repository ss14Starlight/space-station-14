using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Robust.Shared.Containers;

namespace Content.Shared._FarHorizons.Silicons.IPC;

public abstract partial class SharedIPCSystem
{
    protected virtual void SetupBrain() =>
        SubscribeLocalEvent<IPCBrainHolderComponent, ComponentStartup>(OnStartup);

    private void OnStartup(Entity<IPCBrainHolderComponent> ent, ref ComponentStartup args) =>
        ent.Comp.BrainContainerSlot = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.BrainContainerSlotID);
}
