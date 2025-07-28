
using System.Linq;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Robust.Shared.Containers;

namespace Content.Shared._Starlight.Implants;

public abstract partial class DeimplantOnStorageEmptySystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedImplanterSystem _implanter = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DeimplantOnStorageEmptyComponent, EntGotRemovedFromContainerMessage>(OnItemRemoved);
    }

    public void OnItemRemoved(Entity<DeimplantOnStorageEmptyComponent> ent, ref EntGotRemovedFromContainerMessage ev)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
            return;
        if (container.ContainedEntities.Count != 0)
            return;
        if (!_container.TryGetContainingContainer((ent, Transform(ent), MetaData(ent)), out var contained))
            return;

        _container.RemoveEntity(contained.Owner, ent, force: true);
    }
}
