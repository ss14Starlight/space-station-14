using Content.Shared.Body.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Medical.Limbs;
public abstract partial class SharedLimbSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WithAttachedBodyPartsComponent, MapInitEvent>(OnWithAttachedBodyPartsMapInit);

    }

    private void OnWithAttachedBodyPartsMapInit(Entity<WithAttachedBodyPartsComponent> ent, ref MapInitEvent args)
    {
        foreach (var partProtoId in ent.Comp.Parts)
        {
            if (!_prototypes.TryIndex(partProtoId.Value, out var prototype))
                continue;
            var slotId = SharedBodySystem.GetPartSlotContainerId(partProtoId.Key);
            _containers.EnsureContainer<ContainerSlot>(ent, slotId);
            _ = SpawnInContainerOrDrop(prototype.ID, ent, slotId);
        }
    }
}
