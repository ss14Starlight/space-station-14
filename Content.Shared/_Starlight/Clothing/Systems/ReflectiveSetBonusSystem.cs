using Content.Shared._Starlight.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Tag;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.GameObjects;

namespace Content.Shared._Starlight.Clothing.Systems;

/// <summary>
/// Manages the reflective armor set bonus - grants 100% reflection when both vest and helmet are equipped.
/// This system grants 100% reflection to the vest and helmet only when both are equipped.
/// Uses component tags to detect matching items and listens to global equip/unequip events.
/// </summary>
public sealed class ReflectiveSetBonusSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<ReflectiveSetBonusComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<DidEquipEvent>(OnDidEquip);
        SubscribeLocalEvent<DidUnequipEvent>(OnDidUnequip);
    }

    private void OnComponentStartup(EntityUid uid, ReflectiveSetBonusComponent component, ComponentStartup args)
    {
        // Store the original reflection probability from the prototype when component initializes
        if (TryComp<ReflectComponent>(uid, out var reflect))
        {
            component.OriginalReflectProb = reflect.ReflectProb;
        }
    }

    private void OnDidEquip(DidEquipEvent args)
    {
        // Check if the equipped item is part of the reflective set
        if (HasComp<ReflectiveSetBonusComponent>(args.Equipment))
        {
            CheckAllReflectiveSets(args.Equipee);
        }
    }

    private void OnDidUnequip(DidUnequipEvent args)
    {
        // Restore original reflection probability for unequipped item (only if it has the component)
        if (TryComp<ReflectiveSetBonusComponent>(args.Equipment, out var bonus) && 
            TryComp<ReflectComponent>(args.Equipment, out var reflect))
        {
            reflect.ReflectProb = bonus.OriginalReflectProb;
            Dirty(args.Equipment, reflect);
            
            // Update remaining equipped items
            CheckAllReflectiveSets(args.Equipee);
        }
    }

    /// <summary>
    /// Checks all equipped items for the set bonus and applies correct reflection probability.
    /// </summary>
    private void CheckAllReflectiveSets(EntityUid wearer)
    {
        if (!TryComp<InventoryComponent>(wearer, out var inventory))
            return;

        // Check if wearer has both reflective vest and reflective helmet
        var hasVest = false;
        var hasHelmet = false;
        EntityUid? vestEntity = null;
        EntityUid? helmetEntity = null;

        // Check all equipped items
        if (_inventory.TryGetContainerSlotEnumerator(wearer, out var enumerator))
        {
            while (enumerator.MoveNext(out var slot))
            {
                if (slot.ContainedEntity == null)
                    continue;

                var item = slot.ContainedEntity.Value;

                if (!TryComp<ReflectiveSetBonusComponent>(item, out var bonus))
                    continue;

                if (bonus.VestTag != null && _tag.HasTag(item, bonus.VestTag.Value))
                {
                    hasVest = true;
                    vestEntity = item;
                }

                if (bonus.HelmetTag != null && _tag.HasTag(item, bonus.HelmetTag.Value))
                {
                    hasHelmet = true;
                    helmetEntity = item;
                }
            }
        }

        // Apply set bonus if both pieces are equipped
        if (hasVest && hasHelmet && vestEntity.HasValue && helmetEntity.HasValue)
        {
            // Set both items to 100% reflection when full set is worn
            if (TryComp<ReflectComponent>(vestEntity.Value, out var vestReflect))
            {
                vestReflect.ReflectProb = 1.0f;
                Dirty(vestEntity.Value, vestReflect);
            }
            if (TryComp<ReflectComponent>(helmetEntity.Value, out var helmetReflect))
            {
                helmetReflect.ReflectProb = 1.0f;
                Dirty(helmetEntity.Value, helmetReflect);
            }
        }
        else
        {
            // Restore original reflection values when set is incomplete
            if (vestEntity.HasValue && 
                TryComp<ReflectiveSetBonusComponent>(vestEntity.Value, out var vestBonus) &&
                TryComp<ReflectComponent>(vestEntity.Value, out var vestReflect))
            {
                vestReflect.ReflectProb = vestBonus.OriginalReflectProb;
                Dirty(vestEntity.Value, vestReflect);
            }
            
            if (helmetEntity.HasValue && 
                TryComp<ReflectiveSetBonusComponent>(helmetEntity.Value, out var helmetBonus) &&
                TryComp<ReflectComponent>(helmetEntity.Value, out var helmetReflect))
            {
                helmetReflect.ReflectProb = helmetBonus.OriginalReflectProb;
                Dirty(helmetEntity.Value, helmetReflect);
            }
        }
    }
}
