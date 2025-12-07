using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Starlight.Traits.Components;
using Robust.Shared.Containers;
using System.Linq;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Server.Body.Components;
using Content.Shared.Damage.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Traits;

/// <summary>
/// Handles organ replacement for trait-based adaptations.
/// Replaces organs (like lungs) when traits are applied, and provides
/// necessary equipment for breathing adaptation traits.
/// </summary>
public sealed class TraitOrganReplacementSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TraitOrganReplacementComponent, ComponentStartup>(OnStartup);
    }

    /// <summary>
    /// Handles organ replacement when the component is added to an entity.
    /// Finds the appropriate body part, removes existing organs, and inserts the new one.
    /// Also handles special cases like giving emergency nitrogen gear or removing poison regen.
    /// </summary>
    private void OnStartup(EntityUid uid, TraitOrganReplacementComponent component, ComponentStartup args)
    {
        // Find the torso part (where lungs typically are)
        if (!TryComp<BodyComponent>(uid, out var body))
            return;

        EntityUid? torsoUid = null;
        BodyPartComponent? torsoPart = null;

        // Find the torso body part
        foreach (var (partId, part) in _bodySystem.GetBodyChildren(uid, body))
        {
            if (part.PartType == BodyPartType.Torso)
            {
                torsoUid = partId;
                torsoPart = part;
                break;
            }
        }

        if (torsoUid == null || torsoPart == null)
            return;

        // Check if the organ slot exists using the body system
        if (!_bodySystem.CanInsertOrgan(torsoUid.Value, component.Slot, torsoPart))
            return;

        // Get the container for this organ slot
        var containerId = SharedBodySystem.GetOrganContainerId(component.Slot);
        if (!_containerSystem.TryGetContainer(torsoUid.Value, containerId, out var container))
            return;

        // Remove existing organ if any
        var containedEntities = container.ContainedEntities.ToList();
        
        // Check what type of lungs they currently have (if dealing with lung slot)
        string? currentLungAlert = null;
        if (component.Slot == "lungs")
        {
            foreach (var existingOrgan in containedEntities)
            {
                if (TryComp<LungComponent>(existingOrgan, out var existingLung))
                {
                    currentLungAlert = existingLung.Alert;
                    break;
                }
            }
        }
        
        // Check if the new organ has the same lung type as existing
        var newOrganIsSameType = false;
        if (currentLungAlert != null && TryGetLungAlertFromPrototype(component.Organ, out var newLungAlert))
        {
            newOrganIsSameType = currentLungAlert == newLungAlert;
        }
        
        // If trying to replace with the same organ type, just give equipment and return
        if (newOrganIsSameType)
        {
            GiveEquipment(uid, component);
            return;
        }
        
        foreach (var existingOrgan in containedEntities)
        {
            if (TryComp<OrganComponent>(existingOrgan, out var organComp))
            {
                _bodySystem.RemoveOrgan(existingOrgan, organComp);
                QueueDel(existingOrgan);
            }
        }

        // Spawn and insert the new organ
        var newOrgan = Spawn(component.Organ, Transform(uid).Coordinates);
        if (TryComp<OrganComponent>(newOrgan, out var newOrganComp))
        {
            _bodySystem.InsertOrgan(torsoUid.Value, newOrgan, component.Slot, torsoPart, newOrganComp);
            
            // Give equipment if configured
            GiveEquipment(uid, component);
            
            // Remove poison regen if configured
            if (component.RemovePoisonRegen)
            {
                RemovePoisonRegen(uid);
            }
        }
        else
        {
            // If spawning failed or it's not an organ, delete it
            QueueDel(newOrgan);
        }
    }

    /// <summary>
    /// Tries to get the lung alert type from an organ prototype.
    /// </summary>
    private bool TryGetLungAlertFromPrototype(EntProtoId organId, out string? alert)
    {
        alert = null;
        
        var tempOrgan = Spawn(organId, MapCoordinates.Nullspace);
        var hasAlert = TryComp<LungComponent>(tempOrgan, out var lung);
        if (hasAlert && lung != null)
            alert = lung.Alert;
            
        QueueDel(tempOrgan);
        return hasAlert;
    }

    /// <summary>
    /// Gives equipment to an entity based on the component configuration.
    /// </summary>
    private void GiveEquipment(EntityUid uid, TraitOrganReplacementComponent component)
    {
        // Spawn hand item if configured
        if (component.HandItem != null && TryComp<HandsComponent>(uid, out var hands))
        {
            var handItem = Spawn(component.HandItem.Value, Transform(uid).Coordinates);
            _handsSystem.TryPickup(uid, handItem, checkActionBlocker: false, handsComp: hands);
        }

        // Equip items to inventory slots if configured
        foreach (var (slot, itemProto) in component.Equipment)
        {
            var item = Spawn(itemProto, Transform(uid).Coordinates);
            _inventorySystem.TryEquip(uid, item, slot, true, force: true);
        }
    }

    /// <summary>
    /// Removes the PassiveDamage component that provides poison regeneration.
    /// This is called when nitrogen breathers (like Vox) switch to oxygen breathing,
    /// removing their natural poison resistance as a balancing measure.
    /// </summary>
    private void RemovePoisonRegen(EntityUid uid)
    {
        // Remove the PassiveDamage component that provides poison regeneration
        if (TryComp<PassiveDamageComponent>(uid, out var passiveDamage))
        {
            // Check if it has poison regen (negative poison damage)
            if (passiveDamage.Damage.DamageDict.TryGetValue("Poison", out var poisonDamage) && poisonDamage < 0)
            {
                RemComp<PassiveDamageComponent>(uid);
            }
        }
    }
}
