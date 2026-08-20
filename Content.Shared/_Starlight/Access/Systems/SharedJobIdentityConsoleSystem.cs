using Content.Shared._Starlight.Access.Components;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;

namespace Content.Shared._Starlight.Access.Systems;

[UsedImplicitly]
public abstract partial class SharedJobIdentityConsoleSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlotsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JobIdentityConsoleComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<JobIdentityConsoleComponent, ComponentRemove>(OnComponentRemove);
    }

    private void OnComponentInit(EntityUid uid, JobIdentityConsoleComponent component, ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(uid, JobIdentityConsoleComponent.PrivilegedIdCardSlotId, component.PrivilegedIdSlot);
        _itemSlotsSystem.AddItemSlot(uid, JobIdentityConsoleComponent.TargetIdCardSlotId, component.TargetIdSlot);
    }

    private void OnComponentRemove(EntityUid uid, JobIdentityConsoleComponent component, ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, component.PrivilegedIdSlot);
        _itemSlotsSystem.RemoveItemSlot(uid, component.TargetIdSlot);
    }
}
