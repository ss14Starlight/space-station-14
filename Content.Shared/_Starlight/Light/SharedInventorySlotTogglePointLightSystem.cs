using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;

namespace Content.Shared._Starlight.Light;

public sealed partial class SharedInventorySlotTogglePointLightSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPointLightSystem _light = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventorySlotTogglePointLightComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<InventorySlotTogglePointLightComponent, ClothingDidEquippedEvent>(OnSlotEquipped);
        SubscribeLocalEvent<InventorySlotTogglePointLightComponent, ClothingDidUnequippedEvent>(OnSlotUnequipped);
    }

    private void OnComponentStartup(Entity<InventorySlotTogglePointLightComponent> ent, ref ComponentStartup args) =>
        ToggleLight(ent, ent);

    private void OnSlotEquipped(Entity<InventorySlotTogglePointLightComponent> ent,
        ref ClothingDidEquippedEvent args) =>
        ToggleLight(ent, ent, args.Clothing, true);

    private void OnSlotUnequipped(Entity<InventorySlotTogglePointLightComponent> ent,
        ref ClothingDidUnequippedEvent args) => ToggleLight(ent, ent, args.Clothing, false);

    private void ToggleLight(EntityUid uid, InventorySlotTogglePointLightComponent comp,
        ClothingComponent clothing, bool equipped)
    {
        if (!TryComp<InventoryComponent>(uid, out var inventory)) return;
        if (!_light.TryGetLight(uid, out var light)) return;
        if (equipped)
        {
            if ((comp.OnSlots & clothing.InSlotFlag) != SlotFlags.NONE)
                _light.SetEnabled(uid, true, light);
            else if ((comp.OffSlots & clothing.InSlotFlag) != SlotFlags.NONE)
                _light.SetEnabled(uid, false, light);
            return;
        }

        ToggleLight(uid, comp, inventory, light);
    }

    private void ToggleLight(EntityUid uid, InventorySlotTogglePointLightComponent comp, InventoryComponent? inventory = null, SharedPointLightComponent? light = null)
    {
        if (!Resolve(uid, ref inventory))
            return;

        // Doing it like this because any direct checks with SharedPointLightComponent seem to break.
        if (light is null && !_light.TryGetLight(uid, out light))
            return;

        if (HasItemInSlots(uid, inventory, comp.OnSlots))
            _light.SetEnabled(uid, true, light);
        else if (HasItemInSlots(uid, inventory, comp.OffSlots))
            _light.SetEnabled(uid, false, light);
        else _light.SetEnabled(uid, comp.DefaultState, light);
    }

    private bool HasItemInSlots(EntityUid uid, InventoryComponent inventory, SlotFlags querySlots)
    {
        if (!_inventory.TryGetContainerSlotEnumerator((uid, inventory), out var slots))
            return false;

        // Seemingly no way to get slot defs matched with slot containers, so need to enumerate and get InSlotFlag.
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is null) continue;
            if (_inventory.InSlotWithAnyFlags(slot.ContainedEntity.Value, querySlots)) return true;
        }

        return false;
    }
}
