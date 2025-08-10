using Robust.Shared.Containers;

using Content.Shared._Starlight.Mech.Components;

namespace Content.Shared._Starlight.Mech.Systems;

public abstract partial class SharedMechEquipmentSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MechActiveEquipmentComponent, ComponentStartup>(OnActiveEquipmentStartup);
    }

    private void OnActiveEquipmentStartup(EntityUid uid, MechActiveEquipmentComponent component, ComponentStartup args)
    {
        component.ProvidedContainer = _container.EnsureContainer<Container>(uid, component.ProvidedContainerId);
    }
}
