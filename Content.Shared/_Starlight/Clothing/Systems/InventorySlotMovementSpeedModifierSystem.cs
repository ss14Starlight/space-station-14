using System.Linq;
using Content.Shared._Starlight.Clothing.Components;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;

namespace Content.Shared._Starlight.Clothing.Systems;

public sealed partial class InventorySlotMovementSpeedModifierSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventorySlotMovementSpeedModifierComponent, ClothingDidEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<InventorySlotMovementSpeedModifierComponent, ClothingDidUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<InventorySlotMovementSpeedModifierComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
    }

    private void OnEquipped(Entity<InventorySlotMovementSpeedModifierComponent> ent,
        ref ClothingDidEquippedEvent args) =>
        _speed.RefreshMovementSpeedModifiers(ent);

    private void OnUnequipped(Entity<InventorySlotMovementSpeedModifierComponent> ent,
        ref ClothingDidUnequippedEvent args) =>
        _speed.RefreshMovementSpeedModifiers(ent);

    private void OnRefreshMovementSpeedModifiers(Entity<InventorySlotMovementSpeedModifierComponent> ent,
        ref RefreshMovementSpeedModifiersEvent args)
    {
        var (uid, comp) = ent;
        if (!TryComp<InventoryComponent>(uid, out var inventory))
            return;
        if (!_inventory.TryGetContainerSlotEnumerator((uid, inventory), out var slots))
            return;

        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is null)
                continue;
            var slotEnt = slot.ContainedEntity.Value;

            foreach (var data in from data in comp.SlotData
                     let query = _inventory.InSlotWithAnyFlags(slotEnt, data.AffectedFlags)
                     where query != data.Inverted
                     select data)
                args.ModifySpeed(data.SpeedMod);
        }
    }
}
