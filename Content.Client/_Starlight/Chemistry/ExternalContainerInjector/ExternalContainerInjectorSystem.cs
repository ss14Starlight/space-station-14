using Content.Client.Items;
using Content.Shared._Starlight.Chemistry.ExternalContainerInjector;
using Robust.Client.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Chemistry.ExternalContainerInjector;

public sealed class ExternalContainerInjectorSystem : SharedExternalContainerInjectorSystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<ExternalContainerInjectorComponent>(ent => new ExternalContainerInjectorStatusControl(ent));

        // Subscribe to container events to update status controls when vials are inserted/removed
        SubscribeLocalEvent<ExternalContainerInjectorComponent, EntInsertedIntoContainerMessage>(OnVialInserted);
        SubscribeLocalEvent<ExternalContainerInjectorComponent, EntRemovedFromContainerMessage>(OnVialRemoved);
    }

    private void OnVialInserted(Entity<ExternalContainerInjectorComponent> entity,
        ref EntInsertedIntoContainerMessage args)
    {
        // Check if this is the vial slot
        if (args.Container.ID != entity.Comp.VialSlotId)
            return;

        // Force update the status control
        Dirty(entity);
    }

    private void OnVialRemoved(Entity<ExternalContainerInjectorComponent> entity,
        ref EntRemovedFromContainerMessage args)
    {
        // Check if this is the vial slot
        if (args.Container.ID != entity.Comp.VialSlotId)
            return;

        // Force update the status control
        Dirty(entity);
    }
}