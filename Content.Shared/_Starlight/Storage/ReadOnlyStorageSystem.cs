using Content.Shared.Storage;
using Robust.Shared.Containers;

namespace Content.Shared._Starlight.Storage;

// blocks player inserts into read only storage. only the storage container is guarded, and forced
// inserts skip the attempt event, so code can still place items inside
public sealed class ReadOnlyStorageSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReadOnlyStorageComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
    }

    private void OnInsertAttempt(Entity<ReadOnlyStorageComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.Container.ID != StorageComponent.ContainerId)
            return;

        args.Cancel();
    }
}
