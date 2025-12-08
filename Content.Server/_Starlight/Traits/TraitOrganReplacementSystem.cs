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
using Content.Shared.Clothing.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Roles;
using Robust.Shared.Log;
using Content.Shared.Damage;
using Content.Shared.Body.Systems;
using Robust.Shared.Timing;

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
    [Dependency] private readonly GasTankSystem _gasTankSystem = default!;
    [Dependency] private readonly SharedInternalsSystem _sharedInternalsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TraitOrganReplacementComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TraitOrganReplacementComponent, MapInitEvent>(OnMapInit);
    }

    /// <summary>
    /// Handles organ replacement when the entity is map initialized.
    /// This ensures the body is fully set up before we try to modify it.
    /// </summary>
    private void OnMapInit(EntityUid uid, TraitOrganReplacementComponent component, MapInitEvent args)
    {
        PerformOrganReplacement(uid, component);
    }

    /// <summary>
    /// Handles organ replacement when the component is added to an entity.
    /// Finds the appropriate body part, removes existing organs, and inserts the new one.
    /// Also handles special cases like giving emergency nitrogen gear or removing poison regen.
    /// </summary>
    private void OnStartup(EntityUid uid, TraitOrganReplacementComponent component, ComponentStartup args)
    {
        PerformOrganReplacement(uid, component);
    }

    private void PerformOrganReplacement(EntityUid uid, TraitOrganReplacementComponent component)
    {
        // Prevent double execution
        if (component.HasBeenApplied)
            return;

        // Find the torso part (where lungs typically are)
        if (!TryComp<BodyComponent>(uid, out var body))
        {
            Log.Warning($"TraitOrganReplacement: No body component found for {ToPrettyString(uid)}");
            return;
        }

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
        {
            Log.Warning($"TraitOrganReplacement: No torso found for {ToPrettyString(uid)}");
            return;
        }

        // Check if the organ slot exists using the body system
        var canInsertOrgan = _bodySystem.CanInsertOrgan(torsoUid.Value, component.Slot, torsoPart);
        
        // If slot doesn't exist (like Shadekin with no lungs), create it
        if (!canInsertOrgan && component.Slot == "lungs")
        {
            if (!_bodySystem.TryCreateOrganSlot(torsoUid.Value, component.Slot, out _, torsoPart))
            {
                Log.Error($"TraitOrganReplacement: Failed to create lungs slot for {ToPrettyString(uid)}");
                return;
            }
            
            Dirty(torsoUid.Value, torsoPart);
        }

        // Get the container for this organ slot (create if it doesn't exist)
        var containerId = SharedBodySystem.GetOrganContainerId(component.Slot);
        if (!_containerSystem.TryGetContainer(torsoUid.Value, containerId, out var container))
        {
            // If container doesn't exist, ensure it's created
            container = _containerSystem.EnsureContainer<ContainerSlot>(torsoUid.Value, containerId);
        }

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
            
            // Still remove poison regen if configured, even if not replacing organs
            if (component.RemovePoisonRegen)
            {
                RemovePoisonRegen(uid);
            }
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
            var inserted = _bodySystem.InsertOrgan(torsoUid.Value, newOrgan, component.Slot, torsoPart, newOrganComp);
            
            if (inserted)
            {
                // Mark as applied to prevent double execution (don't dirty - this is a server-only component)
                component.HasBeenApplied = true;
            }
            else
            {
                Log.Error($"TraitOrganReplacement: Failed to insert organ {ToPrettyString(newOrgan)} into slot {component.Slot} for {ToPrettyString(uid)}");
            }
            
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
            Log.Error($"TraitOrganReplacement: Spawned entity {ToPrettyString(newOrgan)} is not an organ, deleting");
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
        // Handle special nitrogen tank spawning logic
        if (component.SpawnNitrogenTank)
        {
            SpawnNitrogenTankEquipment(uid);
        }
        else
        {
            // Spawn hand item if configured
            if (component.HandItem != null && TryComp<HandsComponent>(uid, out var hands))
            {
                var handItem = Spawn(component.HandItem.Value, Transform(uid).Coordinates);
                _handsSystem.TryPickup(uid, handItem, checkActionBlocker: false, handsComp: hands);
            }
        }

        // Equip items to inventory slots if configured
        foreach (var (slot, itemProto) in component.Equipment)
        {
            var item = Spawn(itemProto, Transform(uid).Coordinates);
            _inventorySystem.TryEquip(uid, item, slot, true, force: true);
        }
        
        // Delay the breath tool and tank connection to ensure equipment is fully initialized
        // This is necessary because GotEquippedEvent doesn't fire reliably during character spawn
        if (component.SpawnNitrogenTank || component.Equipment.ContainsKey("mask"))
        {
            Timer.Spawn(TimeSpan.FromMilliseconds(100), () =>
            {
                if (!Exists(uid))
                    return;
                    
                // Connect breath tool if we equipped a mask
                if (component.Equipment.TryGetValue("mask", out var maskProto) && 
                    _inventorySystem.TryGetSlotEntity(uid, "mask", out var maskEntity) &&
                    TryComp<BreathToolComponent>(maskEntity, out var breathTool) && 
                    TryComp<InternalsComponent>(uid, out var internalsComp))
                {
                    breathTool.ConnectedInternalsEntity = uid;
                    _sharedInternalsSystem.ConnectBreathTool((uid, internalsComp), maskEntity.Value);
                    Log.Debug($"TraitOrganReplacement: Manually connected breath tool {maskEntity.Value} to {uid}. BreathTools count: {internalsComp.BreathTools.Count}");
                }
                
                // Connect nitrogen tank for nitrogen breathers
                if (component.SpawnNitrogenTank && TryComp<InternalsComponent>(uid, out var internals))
                {
                    Log.Debug($"TraitOrganReplacement: Attempting to connect nitrogen tank for {uid}. BreathTools count: {internals.BreathTools.Count}");
                    
                    if (_inventorySystem.TryGetSlotEntity(uid, "suitstorage", out var tankEntity) &&
                        TryComp<GasTankComponent>(tankEntity, out var gasTank))
                    {
                        Log.Debug($"TraitOrganReplacement: Found tank {tankEntity.Value} in suitstorage, connecting to internals");
                        
                        var success = _gasTankSystem.ConnectToInternals((tankEntity.Value, gasTank), user: uid);
                        Log.Debug($"TraitOrganReplacement: ConnectToInternals returned {success}. GasTankEntity: {internals.GasTankEntity}");
                    }
                    else
                    {
                        Log.Warning($"TraitOrganReplacement: Could not find nitrogen tank in suitstorage slot for {uid}");
                    }
                }
            });
        }
    }

    /// <summary>
    /// Spawns a large nitrogen tank on the entity's back and optionally a tank harness if no armor is equipped.
    /// Enables the tank immediately so the entity can breathe.
    /// </summary>
    private void SpawnNitrogenTankEquipment(EntityUid uid)
    {
        // Check if entity has armor equipped in the outerClothing slot
        var hasArmor = _inventorySystem.TryGetSlotEntity(uid, "outerClothing", out var armorEntity) &&
                       TryComp<ClothingComponent>(armorEntity, out _);

        // If no armor, equip tank harness
        if (!hasArmor)
        {
            var tankHarness = Spawn("ClothingOuterVestTank", Transform(uid).Coordinates);
            _inventorySystem.TryEquip(uid, tankHarness, "outerClothing", true, force: true);
        }

        // Remove any existing item in the suitstorage slot to make room for the nitrogen tank
        if (_inventorySystem.TryGetSlotEntity(uid, "suitstorage", out var existingItem))
        {
            _inventorySystem.TryUnequip(uid, "suitstorage", true, force: true);
        }

        // Spawn large nitrogen tank
        var nitrogenTank = Spawn("NitrogenTankFilled", Transform(uid).Coordinates);
        
        // Equip it to the suitstorage slot (should succeed since we cleared it)
        _inventorySystem.TryEquip(uid, nitrogenTank, "suitstorage", true, force: true);
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
