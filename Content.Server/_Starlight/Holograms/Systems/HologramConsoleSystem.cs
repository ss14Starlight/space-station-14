using System.Linq;
using Content.Server._Starlight.Holograms.Components;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Holograms;
using Content.Shared._Starlight.Holograms.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Item;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Holograms.Systems;

public sealed class HologramConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly HologramSystem _hologram = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly PredictedBatterySystem _battery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleProjectHologramMessage>(OnProjectHologram);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleRecallMessage>(OnRecallHologram);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleEjectDiskMessage>(OnEjectDisk);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleToggleCarryMessage>(OnToggleCarry);
        SubscribeLocalEvent<HologramConsoleComponent, EntInsertedIntoContainerMessage>(OnDiskInserted);
        SubscribeLocalEvent<HologramConsoleComponent, EntRemovedFromContainerMessage>(OnDiskRemoved);
        SubscribeLocalEvent<HologramConsoleComponent, PowerCellSlotEmptyEvent>(OnBatteryEmpty);
        SubscribeLocalEvent<HologramConsoleComponent, PowerChangedEvent>(OnConsolePowerChanged);
        SubscribeLocalEvent<HologramServerComponent, ComponentRemove>(OnServerRemoved);
    }

    private void OnBatteryEmpty(EntityUid uid, HologramConsoleComponent component, ref PowerCellSlotEmptyEvent args)
    {
        // Recall all holograms when battery is empty
        if (!IsPortable(uid))
            return;

        foreach (var hologram in component.ActiveHolograms.Values)
        {
            if (Exists(hologram))
                _hologram.DoKillHologram(hologram);
        }
        component.ActiveHolograms.Clear();
        UpdateUserInterface(uid, component);
    }

    private void OnConsolePowerChanged(EntityUid uid, HologramConsoleComponent component, ref PowerChangedEvent args)
    {
        // Only handle stationary consoles losing power
        if (IsPortable(uid) || args.Powered)
            return;

        // Recall hologram when console loses power
        var server = component.LinkedServer;
        if (server != null && TryComp<HologramServerComponent>(server.Value, out var serverComp) && serverComp.LinkedHologram != null)
        {
            _hologram.DoKillHologram(serverComp.LinkedHologram.Value);
            serverComp.LinkedHologram = null;
        }
    }

    private void OnServerRemoved(EntityUid uid, HologramServerComponent component, ComponentRemove args)
    {
        // Recall hologram when server is deleted
        if (component.LinkedHologram != null)
        {
            _hologram.DoKillHologram(component.LinkedHologram.Value);
            component.LinkedHologram = null;
        }
    }

    public bool IsPortable(EntityUid uid) => HasComp<ItemComponent>(uid);
    public bool IsBatteryPowered(EntityUid uid) => HasComp<PowerCellSlotComponent>(uid);
    private void OnUIOpened(EntityUid uid, HologramConsoleComponent component, BoundUIOpenedEvent args) =>
        UpdateUserInterface(uid, component);

    private void OnDiskInserted(EntityUid uid, HologramConsoleComponent component, EntInsertedIntoContainerMessage args) =>
        UpdateUserInterface(uid, component);

    private void OnDiskRemoved(EntityUid uid, HologramConsoleComponent component, EntRemovedFromContainerMessage args) =>
        UpdateUserInterface(uid, component);

    private void UpdateUserInterface(EntityUid console, HologramConsoleComponent? component = null)
    {
        if (!Resolve(console, ref component))
            return;

        if (!_ui.HasUi(console, HologramConsoleUiKey.Key))
            return;

        var disks = new List<DiskInfo>();
        var activeCount = 0;

        // Find linked server (stationary mode only)
        var server = component.LinkedServer;
        if (!IsPortable(console) && (server == null || !TryComp<HologramServerComponent>(server, out var serverComp)))
        {
            // Try to find nearby server
            foreach (var nearby in _lookup.GetEntitiesInRange(Transform(console).Coordinates, component.SearchRange))
            {
                if (TryComp<HologramServerComponent>(nearby, out serverComp))
                {
                    server = nearby;
                    component.LinkedServer = server;
                    break;
                }
            }
        }

        // Check if console has a disk
        if (component.DiskSlot != null && 
            TryComp<ContainerManagerComponent>(console, out var containerManager) &&
            containerManager.Containers.TryGetValue(component.DiskSlot, out var container) &&
            container.ContainedEntities.Count > 0)
        {
            var disk = container.ContainedEntities[0];
            if (TryComp<HologramDiskComponent>(disk, out var diskComp))
            {
                // Check if disk has either a mind or a prototype
                if (diskComp.HoloMind != null || diskComp.HologramPrototype != null)
                {
                    string hologramName;
                    bool isActive;
                    
                    if (IsPortable(console))
                    {
                        // Portable mode: check if disk is in active holograms map
                        isActive = component.ActiveHolograms.ContainsKey(disk);
                        if (isActive) activeCount++;
                    }
                    else
                    {
                        // Stationary mode: check server's linked hologram
                        isActive = server != null && 
                                   TryComp<HologramServerComponent>(server.Value, out var srvComp) && 
                                   srvComp.LinkedHologram != null;
                    }
                    
                    if (diskComp.HoloMind != null && TryComp<MindComponent>(diskComp.HoloMind, out var mindComp))
                    {
                        hologramName = mindComp.CharacterName ?? "Unknown";
                    }
                    else if (diskComp.HologramPrototype != null)
                    {
                        hologramName = MetaData(disk).EntityName;
                    }
                    else
                    {
                        hologramName = "Unknown";
                    }

                    disks.Add(new DiskInfo(GetNetEntity(disk), hologramName, isActive));
                }
            }
        }

        // Get projector list with locations (stationary mode only)
        var projectors = new List<ProjectorInfo>();
        var projectorCoordinates = new Dictionary<NetEntity, NetCoordinates>();
        if (!IsPortable(console) && _station.GetOwningStation(console) is { } station)
        {
            // Query all projectors on the station (exclude portable ones)
            var query = EntityQueryEnumerator<HologramProjectorComponent, TransformComponent>();
            while (query.MoveNext(out var projector, out _, out var xform))
            {
                if (_station.GetOwningStation(projector, xform) != station)
                    continue;

                // Skip portable projectors (items/consoles that are also projectors)
                if (HasComp<ItemComponent>(projector))
                    continue;

                var name = MetaData(projector).EntityName;
                var location = GetProjectorLocation(projector, xform);

                var netEntity = GetNetEntity(projector);
                projectors.Add(new ProjectorInfo(netEntity, name, location));
                projectorCoordinates[netEntity] = GetNetCoordinates(xform.Coordinates);
            }
        }

        // Get active hologram
        NetEntity? activeHologram = null;
        if (!IsPortable(console) && server != null && 
            TryComp<HologramServerComponent>(server.Value, out var activeServerComp) && 
            activeServerComp.LinkedHologram != null)
        {
            activeHologram = GetNetEntity(activeServerComp.LinkedHologram.Value);
        }

        // Get battery info
        float? batteryPercent = null;
        if (IsBatteryPowered(console) && _powerCell.TryGetBatteryFromSlot(console, out var battery))
        {
            var charge = _battery.GetCharge(battery.Value.AsNullable());
            var maxCharge = battery.Value.Comp.MaxCharge;
            batteryPercent = maxCharge > 0 ? charge / maxCharge * 100f : 0f;
        }

        // Count actual disk slots from ItemSlots component
        var maxDiskSlots = 1; // Default to 1
        if (TryComp<ItemSlotsComponent>(console, out var itemSlotsComp))
        {
            // Count slots that have HoloDisk whitelist
            maxDiskSlots = itemSlotsComp.Slots.Values.Count(slot => 
                slot.Whitelist?.Tags?.Contains("HoloDisk") ?? false);
            if (maxDiskSlots == 0) maxDiskSlots = 1; // Fallback
        }

        var state = new HologramConsoleBoundUserInterfaceState(
            disks, 
            activeHologram, 
            projectors,
            projectorCoordinates,
            IsPortable(console),
            batteryPercent,
            component.AllowHologramCarry,
            activeCount,
            component.MaxActiveHolograms,
            maxDiskSlots);
            
        _ui.SetUiState(console, HologramConsoleUiKey.Key, state);
    }

    private string GetProjectorLocation(EntityUid projector, TransformComponent xform)
    {
        // Get grid name or area name if possible
        var coords = _transform.GetMapCoordinates(projector, xform);
        
        if (xform.GridUid != null)
        {
            var gridName = MetaData(xform.GridUid.Value).EntityName;
            // Try to get more specific location info
            var pos = xform.Coordinates;
            return $"{gridName} ({pos.X:F0}, {pos.Y:F0})";
        }

        return $"({coords.X:F0}, {coords.Y:F0})";
    }

    private void OnProjectHologram(EntityUid console, HologramConsoleComponent component, HologramConsoleProjectHologramMessage args)
    {
        // Get the specified disk
        var disk = GetEntity(args.DiskUid);
        if (!Exists(disk) || !TryComp<HologramDiskComponent>(disk, out var diskComp))
            return;

        // Verify disk is in console
        if (component.DiskSlot == null || 
            !TryComp<ContainerManagerComponent>(console, out var containerManager) ||
            !containerManager.Containers.TryGetValue(component.DiskSlot, out var container) ||
            !container.Contains(disk))
            return;

        // Check if we have either a mind or a prototype
        if (diskComp.HoloMind == null && diskComp.HologramPrototype == null)
            return;
        
        if (IsPortable(console))
        {
            if (component.ActiveHolograms.Count >= component.MaxActiveHolograms)
                return;

            // Check power
            if (IsBatteryPowered(console) && (!_powerCell.TryGetBatteryFromSlot(console, out var battery) || _battery.GetCharge(battery.Value.AsNullable()) <= 0))
                return;

            EntityUid? holo = null;
            var consoleXform = Transform(console);
            var spawnCoords = new EntityCoordinates(consoleXform.MapUid ?? EntityUid.Invalid, _transform.GetWorldPosition(consoleXform));

            // Spawn from prototype
            if (diskComp.HologramPrototype != null)
            {
                holo = Spawn(diskComp.HologramPrototype, spawnCoords);
            }
            // Spawn from mind
            else if (diskComp.HoloMind != null)
            {
                _hologram.TryGenerateHumanoidHologram(diskComp.HoloMind.Value, spawnCoords, out holo);
            }

            if (holo != null)
            {
                // Track the disk
                component.ActiveHolograms[disk] = holo.Value;
                
                // Set the console itself as projector and override so holograms don't jump to cameras
                if (TryComp<HologramProjectedComponent>(holo.Value, out var projectedComp))
                {
                    var netConsole = GetNetEntity(console);
                    projectedComp.CurProjector = netConsole;
                    projectedComp.ProjectorOverride = netConsole; // Lock to this portable console only
                }
            }
        }
        else
        {
            var server = component.LinkedServer;
            if (server == null || !TryComp<HologramServerComponent>(server, out var serverComp))
                return;

            var projector = GetEntity(args.ProjectorUid);
            if (!Exists(projector))
                return;

            // Kill existing hologram if any
            if (serverComp.LinkedHologram != null)
            {
                _hologram.TryKillHologram(serverComp.LinkedHologram.Value);
                serverComp.LinkedHologram = null;
            }

            // Spawn hologram at projector
            var projectorCoords = Transform(projector).Coordinates;
            EntityUid? holo = null;

            // Spawn from prototype
            if (diskComp.HologramPrototype != null)
            {
                holo = Spawn(diskComp.HologramPrototype, projectorCoords);
            }
            // Spawn from mind
            else if (diskComp.HoloMind != null)
            {
                _hologram.TryGenerateHumanoidHologram(diskComp.HoloMind.Value, projectorCoords, out holo);
            }

            if (holo != null)
            {
                serverComp.LinkedHologram = holo;
                
                // Set the projector as current for the hologram
                if (TryComp<HologramProjectedComponent>(holo.Value, out var projectedComp))
                {
                    projectedComp.CurProjector = GetNetEntity(projector);
                }
            }
        }

        UpdateUserInterface(console, component);
    }

    private void OnRecallHologram(EntityUid console, HologramConsoleComponent component, HologramConsoleRecallMessage args)
    {
        if (IsPortable(console))
        {
            foreach (var hologram in component.ActiveHolograms.Values)
            {
                if (Exists(hologram))
                    _hologram.DoKillHologram(hologram);
            }
            component.ActiveHolograms.Clear();
        }
        else
        {
            var server = component.LinkedServer;
            if (server == null || !TryComp<HologramServerComponent>(server, out var serverComp))
                return;

            if (serverComp.LinkedHologram != null)
            {
                _hologram.DoKillHologram(serverComp.LinkedHologram.Value);
                serverComp.LinkedHologram = null;
            }
        }

        UpdateUserInterface(console, component);
    }

    private void OnEjectDisk(EntityUid console, HologramConsoleComponent component, HologramConsoleEjectDiskMessage args)
    {
        var disk = GetEntity(args.DiskUid);
        if (!Exists(disk))
            return;

        // Verify disk is in console
        if (component.DiskSlot == null)
            return;

        // Recall hologram if disk is active
        if (IsPortable(console))
        {
            if (component.ActiveHolograms.TryGetValue(disk, out var hologram))
            {
                if (Exists(hologram))
                    _hologram.DoKillHologram(hologram);
                component.ActiveHolograms.Remove(disk);
            }
        }
        else
        {
            // For stationary consoles, check if this disk's hologram is currently active
            var server = component.LinkedServer;
            if (server != null && TryComp<HologramServerComponent>(server.Value, out var serverComp))
            {
                // Check if the active hologram belongs to this disk
                if (serverComp.LinkedHologram != null && TryComp<HologramDiskComponent>(disk, out var diskComp))
                {
                    // If disk has a mind, check if the hologram has that mind
                    bool shouldRecall = false;
                    if (diskComp.HoloMind != null && TryComp<MindContainerComponent>(serverComp.LinkedHologram.Value, out var mindContainer))
                    {
                        shouldRecall = mindContainer.Mind == diskComp.HoloMind;
                    }
                    
                    if (shouldRecall)
                    {
                        _hologram.DoKillHologram(serverComp.LinkedHologram.Value);
                        serverComp.LinkedHologram = null;
                    }
                }
            }
        }

        // Eject disk using item slots system
        _itemSlots.TryEject(console, component.DiskSlot, null, out var _);
        
        UpdateUserInterface(console, component);
    }

    private void OnToggleCarry(EntityUid console, HologramConsoleComponent component, HologramConsoleToggleCarryMessage args)
    {
        // Only works for portable mode (items)
        if (!IsPortable(console))
            return;

        component.AllowHologramCarry = args.AllowCarry;
        UpdateUserInterface(console, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Handle portable mode power drain and cleanup
        var query = EntityQueryEnumerator<HologramConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!IsPortable(uid))
                continue;

            var activeCount = component.ActiveHolograms.Count;
            
            if (activeCount > 0 && _powerCell.TryGetBatteryFromSlot(uid, out var battery))
            {
                var powerDraw = component.PowerDrawPerHologram * activeCount * frameTime;
                
                if (!_powerCell.TryUseCharge(uid, powerDraw))
                {
                    // Out of power - kill all holograms
                    foreach (var hologram in component.ActiveHolograms.Values)
                    {
                        if (Exists(hologram))
                            _hologram.DoKillHologram(hologram);
                    }
                    component.ActiveHolograms.Clear();
                }
            }

            // Clean up dead holograms from the dictionary
            var toRemove = component.ActiveHolograms
                .Where(kvp => !Exists(kvp.Key) || !Exists(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var disk in toRemove)
                component.ActiveHolograms.Remove(disk);
        }
    }
}
