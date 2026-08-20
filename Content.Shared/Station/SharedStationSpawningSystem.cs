using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Collections;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
#region Starlight
using Content.Shared.Preferences;
using Content.Shared._Starlight.Roles;
using Content.Shared.Containers.ItemSlots;
#endregion

namespace Content.Shared.Station;

public abstract partial class SharedStationSpawningSystem : EntitySystem
{
    [Dependency] protected IPrototypeManager PrototypeManager = default!;
    [Dependency] protected ISharedAdminLogManager _adminLogger = default!; // Starlight
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] protected InventorySystem InventorySystem = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedTransformSystem _xformSystem = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!; // Starlight

    private EntityQuery<HandsComponent> _handsQuery;
    private EntityQuery<InventoryComponent> _inventoryQuery;
    private EntityQuery<StorageComponent> _storageQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<ItemSlotsComponent> _itemSlotsQuery; // Starlight

    public override void Initialize()
    {
        base.Initialize();
        _handsQuery = GetEntityQuery<HandsComponent>();
        _inventoryQuery = GetEntityQuery<InventoryComponent>();
        _storageQuery = GetEntityQuery<StorageComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _itemSlotsQuery = GetEntityQuery<ItemSlotsComponent>(); // Starlight
    }

    /// <summary>
    ///     Equips the data from a `RoleLoadout` onto an entity.
    /// </summary>
    public void EquipRoleLoadout(EntityUid entity, RoleLoadout loadout, RoleLoadoutPrototype roleProto, HumanoidCharacterProfile? profile = null) => EquipRoleLoadout(entity, loadout, roleProto, profile, null); // Starlight edit

    internal void EquipRoleLoadout(EntityUid entity, RoleLoadout loadout, RoleLoadoutPrototype roleProto, HumanoidCharacterProfile? profile, PriorityStorageEquipContext? priorityContext) // Starlight
    {

        // Starlight Start
        // Store loadout and profile on entity
        var appliedLoadout = EnsureComp<AppliedRoleLoadoutComponent>(entity);
        appliedLoadout.Loadout = loadout;
        appliedLoadout.Profile = profile;


        if (StarlightEquipRoleLoadout(entity, loadout, [], roleProto, priorityContext)) // Starlight
        {
            EquipRoleName(entity, loadout, roleProto);
            return;
        }
        // Starlight end
        // Order loadout selections by the order they appear on the prototype.
        foreach (var group in loadout.SelectedLoadouts.OrderBy(x => roleProto.Groups.FindIndex(e => e == x.Key)))
        {
            foreach (var items in group.Value)
            {
                if (!PrototypeManager.TryIndex(items.Prototype, out var loadoutProto))
                {
                    Log.Error($"Unable to find loadout prototype for {items.Prototype}");
                    continue;
                }

                EquipStartingGear(entity, loadoutProto, raiseEvent: false, priorityContext: priorityContext); // Starlight
            }
        }

        EquipRoleName(entity, loadout, roleProto);
    }

    /// <summary>
    /// Applies the role's name as applicable to the entity.
    /// </summary>
    public void EquipRoleName(EntityUid entity, RoleLoadout loadout, RoleLoadoutPrototype roleProto)
    {
        string? name = null;

        if (roleProto.CanCustomizeName)
        {
            name = loadout.EntityName;
        }

        if (string.IsNullOrEmpty(name) && PrototypeManager.Resolve(roleProto.NameDataset, out var nameData))
        {
            name = Loc.GetString(_random.Pick(nameData.Values));
        }

        if (!string.IsNullOrEmpty(name))
        {
            _metadata.SetEntityName(entity, name);
        }
    }

    public void EquipStartingGear(EntityUid entity, LoadoutPrototype loadout, bool raiseEvent = true) => EquipStartingGear(entity, loadout, raiseEvent, null); // Starlight

    internal void EquipStartingGear(EntityUid entity, LoadoutPrototype loadout, bool raiseEvent, PriorityStorageEquipContext? priorityContext) // Starlight
    {
        EquipStartingGear(entity, loadout.StartingGear, raiseEvent, priorityContext); // Starlight
        EquipStartingGear(entity, (IEquipmentLoadout) loadout, raiseEvent, priorityContext); // Starlight
    }

    /// <summary>
    /// <see cref="EquipStartingGear(Robust.Shared.GameObjects.EntityUid,System.Nullable{Robust.Shared.Prototypes.ProtoId{Content.Shared.Roles.StartingGearPrototype}},bool)"/>
    /// </summary>
    public void EquipStartingGear(EntityUid entity, ProtoId<StartingGearPrototype>? startingGear, bool raiseEvent = true) => EquipStartingGear(entity, startingGear, raiseEvent, null); // Starlight

    internal void EquipStartingGear(EntityUid entity, ProtoId<StartingGearPrototype>? startingGear, bool raiseEvent, PriorityStorageEquipContext? priorityContext) // Starlight
    {
        PrototypeManager.Resolve(startingGear, out var gearProto);
        EquipStartingGear(entity, gearProto, raiseEvent, priorityContext); // Starlight
    }

    /// <summary>
    /// <see cref="EquipStartingGear(Robust.Shared.GameObjects.EntityUid,System.Nullable{Robust.Shared.Prototypes.ProtoId{Content.Shared.Roles.StartingGearPrototype}},bool)"/>
    /// </summary>
    public void EquipStartingGear(EntityUid entity, StartingGearPrototype? startingGear, bool raiseEvent = true) => EquipStartingGear(entity, startingGear, raiseEvent, null); // Starlight

    internal void EquipStartingGear(EntityUid entity, StartingGearPrototype? startingGear, bool raiseEvent, PriorityStorageEquipContext? priorityContext) => EquipStartingGear(entity, (IEquipmentLoadout?)startingGear, raiseEvent, priorityContext); // Starlight

    /// <summary>
    /// Equips starting gear onto the given entity.
    /// </summary>
    /// <param name="entity">Entity to load out.</param>
    /// <param name="startingGear">Starting gear to use.</param>
    /// <param name="raiseEvent">Should we raise the event for equipped. Set to false if you will call this manually</param>
    public void EquipStartingGear(EntityUid entity, IEquipmentLoadout? startingGear, bool raiseEvent = true) => EquipStartingGear(entity, startingGear, raiseEvent, null); // Starlight, so many "use expression body for method" commments...

    internal void EquipStartingGear(EntityUid entity, IEquipmentLoadout? startingGear, bool raiseEvent,  PriorityStorageEquipContext? priorityContext) // Starlight
    {
        if (startingGear == null)
            return;

        var xform = _xformQuery.GetComponent(entity);

        if (InventorySystem.TryGetSlots(entity, out var slotDefinitions))
        {
            var gearLeftToBeIssued = startingGear.Equipment.ToDictionary(); // Starlight
            foreach (var slot in slotDefinitions)
            {
                var equipmentStr = startingGear.GetGear(slot.Name);
                if (!string.IsNullOrEmpty(equipmentStr))
                {
                    var equipmentEntity = Spawn(equipmentStr, xform.Coordinates);
                    InventorySystem.TryEquip(entity, equipmentEntity, slot.Name, silent: true, force: true);
                    gearLeftToBeIssued.Remove(slot.Name); // Starlight
                }
            }
            // Starlight Start
            // If the equipping entity doesn't have enough slots to fit the designated gear, still spawn it but place at their feet.
            foreach (var item in gearLeftToBeIssued)
            {
                Spawn(item.Value, xform.Coordinates);
            }
            // Starlight End
        }

        if (_handsQuery.TryComp(entity, out var handsComponent))
        {
            var inhand = startingGear.Inhand;
            var coords = xform.Coordinates;
            foreach (var prototype in inhand)
            {
                var inhandEntity = Spawn(prototype, coords);

                if (_handsSystem.TryGetEmptyHand((entity, handsComponent), out var emptyHand))
                {
                    if (_handsSystem.TryPickup(entity, inhandEntity, emptyHand, checkActionBlocker: false, handsComp: handsComponent)) // Starlight
                        priorityContext?.IssuedGear.Add(inhandEntity); // Starlight
                }
            }
        }

        if (startingGear.Storage.Count > 0)
        {
            #region Starlight
            //var coords = _xformSystem.GetMapCoordinates(entity);
            _inventoryQuery.TryComp(entity, out var inventoryComp);

            /*foreach (var (slotName, entProtos) in startingGear.Storage)
            {
                if (entProtos == null || entProtos.Count == 0)
                    continue;

                if (inventoryComp != null &&
                    InventorySystem.TryGetSlotEntity(entity, slotName, out var slotEnt, inventoryComponent: inventoryComp) &&
                    _storageQuery.TryComp(slotEnt, out var storage))
                {

                    foreach (var entProto in entProtos)
                    {
                        var spawnedEntity = Spawn(entProto, coords);

                        _storage.Insert(slotEnt.Value, spawnedEntity, out _, storageComp: storage, playSound: false);
                    }
                }
                else if (inventoryComp != null &&
                    InventorySystem.TryGetSlotEntity(entity, slotName, out var slotEnt2, inventoryComponent: inventoryComp) &&
                    _itemSlotsQuery.TryComp(slotEnt2, out var itemSlots))
                {

                    foreach (var entProto in entProtos)
                    {
                        var spawnedEntity = Spawn(entProto, coords);
                        // Because we need an Entity<ItemSlotsComponent?>
                        Entity<ItemSlotsComponent?> typed = (slotEnt2.Value, itemSlots);
                        InsertIntoItemSlots(typed, spawnedEntity);
                    }
                }
            }*/
            EquipStorageGear(entity, startingGear, inventoryComp, priorityContext);
            #endregion
        }

        if (raiseEvent)
        {
            var ev = new StartingGearEquippedEvent(entity);
            RaiseLocalEvent(entity, ref ev);
        }
    }

    /// <summary>
    ///     Gets all the gear for a given slot when passed a loadout.
    /// </summary>
    /// <param name="loadout">The loadout to look through.</param>
    /// <param name="slot">The slot that you want the clothing for.</param>
    /// <returns>
    ///     If there is a value for the given slot, it will return the proto id for that slot.
    ///     If nothing was found, will return null
    /// </returns>
    public string? GetGearForSlot(RoleLoadout? loadout, string slot)
    {
        if (loadout == null)
            return null;

        foreach (var group in loadout.SelectedLoadouts)
        {
            foreach (var items in group.Value)
            {
                if (!PrototypeManager.Resolve(items.Prototype, out var loadoutPrototype))
                    return null;

                var gear = ((IEquipmentLoadout) loadoutPrototype).GetGear(slot);
                if (gear != string.Empty)
                    return gear;
            }
        }

        return null;
    }

    // Starlight start
    /// <summary>
    /// A variant on the role loadout equip process that tries to be more deliberate about equipping
    /// characters in the correct order to satisfy requirements (e.g. bags before contents).
    /// </summary>
    /// <param name="entity">The entity being equipped</param>
    /// <param name="loadout">The loadout being equipped to the entity</param>
    /// <param name="otherStartingGear">Other starting gear not listed in the role loadout</param>
    /// <param name="roleProto">The base definition for the role</param>
    /// <returns>true on success, false on failure</returns>
    public bool StarlightEquipRoleLoadout(EntityUid entity, RoleLoadout loadout, IEnumerable<IEquipmentLoadout> otherStartingGear, RoleLoadoutPrototype roleProto) => StarlightEquipRoleLoadout(entity, loadout, otherStartingGear, roleProto, null); // Starlight

    internal bool StarlightEquipRoleLoadout(EntityUid entity, RoleLoadout loadout, IEnumerable<IEquipmentLoadout> otherStartingGear, RoleLoadoutPrototype roleProto, PriorityStorageEquipContext? priorityContext) // Starlight
    {
        List<IEquipmentLoadout> allStartingGear = new();

        // Order loadout selections by the order they appear on the prototype.
        // We're going to process the loadout entries in this order in each of the three passes.
        foreach (var group in loadout.SelectedLoadouts.OrderBy(x => roleProto.Groups.FindIndex(e => e == x.Key)))
        {
            foreach (var items in group.Value)
            {
                if (!PrototypeManager.TryIndex(items.Prototype, out var loadoutProto))
                {
                    Log.Error($"Unable to find loadout prototype for {items.Prototype}");
                    continue;
                }

                if (loadoutProto.StartingGear is not null) {
                    PrototypeManager.Resolve(loadoutProto.StartingGear, out var gearProto);
                    if (gearProto is IEquipmentLoadout equipmentProto) {
                        allStartingGear.Add(equipmentProto);
                    }
                }
                allStartingGear.Add(loadoutProto);
            }
        }

        allStartingGear.AddRange(otherStartingGear);
        var gearRemainingToBeIssued = allStartingGear.ToList();

        var xform = _xformQuery.GetComponent(entity);
        var coords = xform.Coordinates;

        // Do three passes:
        // 1. Add any equipment
        // 2. Insert items into hands
        // 3. Insert items into storages
        // This avoids issues where the normal code may process a loadoutprototype that adds an equipment
        // with storage after a loadoutprototype that tries to use that storage.

        if (InventorySystem.TryGetSlots(entity, out var slotDefinitions))
        {
            foreach (var startingGear in allStartingGear) {
                var equipmentRemaining = startingGear.Equipment.ToList();
                foreach (var slot in slotDefinitions)
                {
                    var equipmentStr = startingGear.GetGear(slot.Name);
                    if (!string.IsNullOrEmpty(equipmentStr))
                    {
                        if (slot.Name == "back" && slot.Whitelist?.Tags?.Contains("CorgiWearable") == true)
                            equipmentStr = "ClothingBagPet";
                        var equipmentEntity = Spawn(equipmentStr, xform.Coordinates);
                        InventorySystem.TryEquip(entity, equipmentEntity, slot.Name, silent: true, force: true);
                    }
                    equipmentRemaining.Remove(equipmentRemaining.FirstOrDefault(a => a.Key == slot.Name));
                }
                foreach (var equipment in equipmentRemaining)
                {
                    var equipmentEntity = Spawn(equipment.Value, xform.Coordinates);
                }
            }
        }

        if (_handsQuery.TryComp(entity, out var handsComponent))
        {
            foreach (var startingGear in allStartingGear) {
                var inhand = startingGear.Inhand;
                foreach (var prototype in inhand)
                {
                    var inhandEntity = Spawn(prototype, coords);

                    if (_handsSystem.TryGetEmptyHand((entity, handsComponent), out var emptyHand))
                    {
                        if (_handsSystem.TryPickup(entity, inhandEntity, emptyHand, checkActionBlocker: false, handsComp: handsComponent)) // Starlight
                            priorityContext?.IssuedGear.Add(inhandEntity); // Starlight
                    }
                }
            }
        }

        _inventoryQuery.TryComp(entity, out var inventoryComp);

        foreach (var startingGear in allStartingGear)
        {
            #region Starlight
            EquipStorageGear(entity, startingGear, inventoryComp, priorityContext);
        }
        return true;
    }
    #endregion

    #region Starlight
    // Pretty much had to rewrite all of this to get it to to work with antags rolling later without dropping their stuff on the ground.
    private void EquipStorageGear(EntityUid entity,
        IEquipmentLoadout startingGear,
        InventoryComponent? inventoryComp,
        PriorityStorageEquipContext? priorityContext)
    {
        var coords = _xformSystem.GetMapCoordinates(entity);

        foreach (var (slotName, entProtos) in startingGear.Storage)
        {
            if (entProtos == null || entProtos.Count == 0)
                continue;

            var prioritize = priorityContext != null && slotName == "back";
            EntityUid? slotEntity = null;
            if (inventoryComp != null)
                InventorySystem.TryGetSlotEntity(entity, slotName, out slotEntity, inventoryComponent: inventoryComp);

            if (slotEntity != null && _storageQuery.TryComp(slotEntity, out var storage))
            {
                foreach (var entProto in entProtos)
                {
                    var spawnedEntity = Spawn(entProto, coords);

                    if (prioritize)
                    {
                        if (!TryInsertPriorityStorageGear(entity,
                                (slotEntity.Value, storage),
                                spawnedEntity,
                                priorityContext!))
                        {
                            TryPlacePriorityGearInHands(entity,
                                spawnedEntity,
                                slotName,
                                priorityContext!,
                                failedStorage: slotEntity.Value);
                        }
                    }
                    else
                    {
                        _storage.Insert(slotEntity.Value,
                            spawnedEntity,
                            out _,
                            storageComp: storage,
                            playSound: false);
                    }
                }
            }
            else if (!prioritize && slotEntity != null && _itemSlotsQuery.TryComp(slotEntity, out var itemSlots))
            {
                foreach (var entProto in entProtos)
                {
                    var spawnedEntity = Spawn(entProto, coords);
                    // Because we need an Entity<ItemSlotsComponent?>
                    Entity<ItemSlotsComponent?> typed = (slotEntity.Value, itemSlots);
                    InsertIntoItemSlots(typed, spawnedEntity);
                }
            }
            else if (prioritize)
            {
                foreach (var entProto in entProtos)
                {
                    var spawnedEntity = Spawn(entProto, coords);
                    TryPlacePriorityGearInHands(entity, spawnedEntity, slotName, priorityContext!);
                }
            }
        }
    }

    private bool TryInsertPriorityStorageGear(EntityUid entity,
        Entity<StorageComponent> storage,
        EntityUid gear,
        PriorityStorageEquipContext priorityContext)
    {
        // Check whether this storage can accept the gear regardless of its current capacity.
        if (!_storage.CanInsert(storage,
                gear,
                out _,
                storage.Comp,
                ignoreStacks: true,
                ignoreLocation: true))
        {
            return false;
        }

        if (TryInsertEntireStorageItem(storage, gear, priorityContext))
            return true;

        // Displace pre-existing contents one at a time, retrying after each removal.
        // Gear issued during this same loadout is protected from being displaced by later gear.
        // If placement never succeeds, restore every displaced item to its original grid location.
        var displacedItems = new List<(EntityUid Item, ItemStorageLocation Location)>();
        foreach (var storedItem in storage.Comp.Container.ContainedEntities.ToArray())
        {
            if (priorityContext.IssuedGear.Contains(storedItem) ||
                !storage.Comp.StoredItems.TryGetValue(storedItem, out var location))
            {
                continue;
            }

            _xformSystem.DropNextTo(storedItem, entity);
            displacedItems.Add((storedItem, location));

            if (!TryInsertEntireStorageItem(storage, gear, priorityContext))
                continue;

            foreach (var (item, _) in displacedItems)
            {
                _adminLogger.Add(LogType.AntagSelection,
                    LogImpact.Low,
                    $"{ToPrettyString(entity):target} had {ToPrettyString(item):item} dropped from {ToPrettyString(storage):storage} to make room for antagonist gear {ToPrettyString(gear):item}");
            }

            return true;
        }

        foreach (var (item, location) in displacedItems)
        {
            if (_storage.InsertAt(storage.AsNullable(),
                    item,
                    location,
                    out _,
                    playSound: false,
                    stackAutomatically: false))
            {
                continue;
            }

            _adminLogger.Add(LogType.AntagSelection,
                LogImpact.Low,
                $"{ToPrettyString(entity):target} had {ToPrettyString(item):item} left on the ground after it could not be restored to {ToPrettyString(storage):storage} following a failed attempt to make room for antagonist gear {ToPrettyString(gear):item}");
        }

        return false;
    }

    private bool TryInsertEntireStorageItem(Entity<StorageComponent> storage,
        EntityUid gear,
        PriorityStorageEquipContext priorityContext)
    {
        // Require enough grid space for the entire item and disable automatic stacking.
        // Otherwise, a partial stack merge could report success while leaving the remainder on the floor.
        if (!_storage.CanInsert(storage, gear, out _, storage.Comp, ignoreStacks: true) ||
            !_storage.Insert(storage,
                gear,
                out _,
                storageComp: storage.Comp,
                playSound: false,
                stackAutomatically: false))
        {
            return false;
        }

        priorityContext.IssuedGear.Add(gear);
        return true;
    }

    private bool TryPlacePriorityGearInHands(EntityUid entity,
        EntityUid gear,
        string slotName,
        PriorityStorageEquipContext priorityContext,
        EntityUid? failedStorage = null)
    {
        var storageFailure = failedStorage == null
            ? $"their {slotName} slot had no storage"
            : $"it could not fit in {ToPrettyString(failedStorage.Value):storage}";

        if (_handsQuery.TryComp(entity, out var hands))
        {
            if (_handsSystem.TryPickupAnyHand(entity,
                    gear,
                    checkActionBlocker: false,
                    animate: false,
                    handsComp: hands))
            {
                priorityContext.IssuedGear.Add(gear);
                _adminLogger.Add(LogType.AntagSelection,
                    LogImpact.Low,
                    $"{ToPrettyString(entity):target} had antagonist gear {ToPrettyString(gear):item} placed in a hand because {storageFailure}");
                return true;
            }

            foreach (var hand in _handsSystem.EnumerateHands((entity, hands)))
            {
                if (!_handsSystem.TryGetHeldItem((entity, hands), hand, out var heldItem) ||
                    priorityContext.IssuedGear.Contains(heldItem.Value))
                {
                    continue;
                }

                if (!_handsSystem.TryForcePickup((entity, hands),
                        gear,
                        hand,
                        checkActionBlocker: false,
                        animate: false,
                        handsComp: hands))
                {
                    continue;
                }

                priorityContext.IssuedGear.Add(gear);
                _adminLogger.Add(LogType.AntagSelection,
                    LogImpact.Low,
                    $"{ToPrettyString(entity):target} had {ToPrettyString(heldItem):item} dropped from a hand so antagonist gear {ToPrettyString(gear):item} could be held because {storageFailure}");
                return true;
            }
        }

        _adminLogger.Add(LogType.AntagSelection,
            LogImpact.Low,
            $"{ToPrettyString(entity):target} had antagonist gear {ToPrettyString(gear):item} left on the ground because {storageFailure} and no hand could hold it");
        return false;
    }
    #endregion

    private void InsertIntoItemSlots(Entity<ItemSlotsComponent?> typed, EntityUid entity) {
        bool foundEmpty = _itemSlots.TryInsertEmpty(typed, entity, null, excludeUserAudio: true, suppressSound: true);

        if (!foundEmpty)
        {
            // Since we're not filling in an empty slot, try to stack
            bool foundSlot = _itemSlots.TryGetAvailableSlot(typed, entity, null, out var writeSlot, emptyOnly: false, allowSwap: false);
            if (foundSlot)
            {
                _itemSlots.TryInsert(typed, writeSlot!, entity, null, excludeUserAudio: true, suppressSound: true);
            }
            else
            {
                // We can't stack - go for a swap instead, and we'll delete the removed item
                foundSlot = _itemSlots.TryGetAvailableSlot(typed, entity, null, out var writeSlotSwap, emptyOnly: false, allowSwap: true);
                if (foundSlot)
                {
                    var xform = _xformQuery.GetComponent(entity);
                    // If we don't specify that we're ejecting it to invalid coordinates, then
                    // when demo entities are loaded for the profile view in testing we'll try
                    // to eject into a nonexistent map coordinate space, which fails the test.
                    var gotDeletable = _itemSlots.TryEject(typed, writeSlotSwap!, null, out var removedItem, excludeUserAudio: true, xform.Coordinates, suppressSound: true);
                    if (gotDeletable)
                    {
                        QueueDel(removedItem);
                    }
                    _itemSlots.TryInsert(typed, writeSlotSwap!, entity, null, excludeUserAudio: true, suppressSound: true);
                }
            }
        }
    }
    // Starlight end
}

#region Starlight
/// A context object that tracks which gear has been issued to a character during the loadout process.
internal sealed class PriorityStorageEquipContext
{
    public readonly HashSet<EntityUid> IssuedGear = [];
}
#endregion
