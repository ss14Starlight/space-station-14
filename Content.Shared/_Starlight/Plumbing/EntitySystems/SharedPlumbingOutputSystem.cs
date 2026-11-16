using Content.Shared._Starlight.Plumbing.Components;

// ReSharper disable CheckNamespace
namespace Content.Shared.Plumbing.EntitySystems;

public abstract partial class SharedPlumbingOutputSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // this re-broadcasts this as a standard event because both ChemMasterSystem and ReagentDispenserSystem need to handle
        // and there can only be one handler for a given component's AfterAutoHandleStateEvent
        SubscribeLocalEvent<PlumbingOutletComponent, AfterAutoHandleStateEvent>(OnPlumbingOutputState);
    }

    private void OnPlumbingOutputState(Entity<PlumbingOutletComponent> ent, ref AfterAutoHandleStateEvent ev)
    {
        PlumbingOutputAutoStateEvent ev2 = new(ent.Comp.Enabled);
        RaiseLocalEvent(ent.Owner, ref ev2);
    }
}
