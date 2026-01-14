using System.Linq;
using Content.Server._Starlight.Holograms.Components;
using Content.Server.Power.Components;
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
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<HologramConsoleComponent, BoundUIClosedEvent>(OnUIClosed);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleProjectHologramMessage>(OnProjectHologram);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleRecallMessage>(OnRecallHologram);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleToggleCarryMessage>(OnToggleCarry);
        SubscribeLocalEvent<HologramConsoleComponent, PowerCellSlotEmptyEvent>(OnBatteryEmpty);
        SubscribeLocalEvent<HologramConsoleComponent, PowerChangedEvent>(OnConsolePowerChanged);
        SubscribeLocalEvent<HologramConsoleComponent, EntInsertedIntoContainerMessage>(OnBladeInserted);
        SubscribeLocalEvent<HologramConsoleComponent, EntRemovedFromContainerMessage>(OnBladeRemoved);
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
    
    private void OnUIOpened(EntityUid uid, HologramConsoleComponent component, BoundUIOpenedEvent args)
    {
        // Scan for server on same grid when UI opens (stationary mode)
        if (!IsPortable(uid) && (component.LinkedServer == null || !TryComp<HologramServerComponent>(component.LinkedServer, out _)))
        {
            // Try to find server on the same grid
            var xform = Transform(uid);
            if (xform.GridUid is { } grid)
            {
                var servers = new HashSet<Entity<HologramServerComponent>>();
                _lookup.GetGridEntities(grid, servers);
                
                if (servers.FirstOrDefault() is { } server)
                {
                    component.LinkedServer = server;
                }
            }
        }
        
        UpdateUserInterface(uid, component);
        UpdateBriefcaseAppearance(uid, component);
    }

    private void OnUIClosed(EntityUid uid, HologramConsoleComponent component, BoundUIClosedEvent args) =>
        UpdateBriefcaseAppearance(uid, component);

    private void OnBladeInserted(EntityUid uid, HologramConsoleComponent component, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != "blade_server_slot")
            return;
        
        UpdateBriefcaseAppearance(uid, component);
        UpdateUserInterface(uid, component);
    }

    private void OnBladeRemoved(EntityUid uid, HologramConsoleComponent component, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != "blade_server_slot")
            return;
        
        UpdateBriefcaseAppearance(uid, component);
        UpdateUserInterface(uid, component);
    }

    private void UpdateBriefcaseAppearance(EntityUid uid, HologramConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component, logMissing: false))
            return;

        if (!IsPortable(uid))
            return;

        if (!HasComp<AppearanceComponent>(uid))
            return;

        // Check if UI is open
        var uiOpen = _ui.IsUiOpen(uid, HologramConsoleUiKey.Key);
        
        // Check if there are active holograms
        var hasActive = component.ActiveHolograms.Count > 0;

        // Determine state
        HologramBriefcaseState state;
        if (!uiOpen)
            state = HologramBriefcaseState.Closed;
        else if (hasActive)
            state = HologramBriefcaseState.Active;
        else
            state = HologramBriefcaseState.Open;

        // Check if blade is inserted
        var hasBlade = _itemSlots.GetItemOrNull(uid, "blade_server_slot") != null;

        // Blade should only be visible when briefcase is open (not closed) AND blade is inserted
        var showBlade = hasBlade && uiOpen;

        _appearance.SetData(uid, HologramBriefcaseVisuals.State, state);
        _appearance.SetData(uid, HologramBriefcaseVisuals.HasBlade, showBlade);
    }

    private void UpdateUserInterface(EntityUid console, HologramConsoleComponent? component = null)
    {
        if (!Resolve(console, ref component))
            return;

        if (!_ui.HasUi(console, HologramConsoleUiKey.Key))
            return;

        var bladeServerList = new List<BladeServerInfo>();
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

        // Scan for powered hologram blade servers within range
        // Blade servers with both brain and body chips populated will appear in the list
        var entitiesInRange = _lookup.GetEntitiesInRange(Transform(console).Coordinates, component.BladeServerScanRange);
        var bladeServers = new HashSet<EntityUid>();
        
        // Collect blade servers - both standalone and those in racks
        foreach (var entity in entitiesInRange)
        {
            // Direct blade servers (only if not in a container)
            if (HasComp<HologramBladeServerComponent>(entity))
            {
                // Check if this blade server is contained in something (e.g., a rack)
                if (!TryComp(entity, out TransformComponent? xform) || xform.ParentUid == EntityUid.Invalid || !HasComp<ItemSlotsComponent>(xform.ParentUid))
                {
                    // Standalone blade server - add it
                    bladeServers.Add(entity);
                }
            }
            // Blade servers inside racks
            else if (TryComp<Content.Shared._Starlight.BladeServer.BladeServerRackComponent>(entity, out var rack))
            {
                // Get all blade servers from the rack's item slots
                if (TryComp<ItemSlotsComponent>(entity, out var rackSlots))
                {
                    foreach (var slot in rackSlots.Slots.Values)
                    {
                        if (slot.Item != null && HasComp<HologramBladeServerComponent>(slot.Item.Value))
                        {
                            bladeServers.Add(slot.Item.Value);
                        }
                    }
                }
            }
        }
        
        foreach (var bladeServerUid in bladeServers)
        {
            if (!TryComp<HologramBladeServerComponent>(bladeServerUid, out var bladeServer))
                continue;

            // Check if the blade server itself is powered, OR if its containing rack is powered
            bool isPowered = false;
            
            // Check if blade server has direct power (standalone)
            if (TryComp<ApcPowerReceiverComponent>(bladeServerUid, out var powerReceiver) && powerReceiver.Powered)
            {
                isPowered = true;
            }
            // Check if blade server is in a powered rack
            else if (TryComp(bladeServerUid, out TransformComponent? xform) && 
                     xform.ParentUid != EntityUid.Invalid &&
                     TryComp<ApcPowerReceiverComponent>(xform.ParentUid, out var rackPower) && 
                     rackPower.Powered)
            {
                isPowered = true;
            }
            
            if (!isPowered)
                continue;

            // Get ItemSlots to access chips
            if (!TryComp<ItemSlotsComponent>(bladeServerUid, out var itemSlots))
                continue;

            // Get brain chip using ItemSlotsSystem
            if (!_itemSlots.TryGetSlot(bladeServerUid, bladeServer.BrainChipSlot, out var brainSlot, itemSlots) || brainSlot.Item == null)
                continue;

            // Get body chip using ItemSlotsSystem
            if (!_itemSlots.TryGetSlot(bladeServerUid, bladeServer.BodyChipSlot, out var bodySlot, itemSlots) || bodySlot.Item == null)
                continue;

            // Both chips present - extract data
            var brainChip = brainSlot.Item.Value;
            var bodyChip = bodySlot.Item.Value;

            if (!TryComp<HologramBrainChipComponent>(brainChip, out var brainComp))
                continue;

            if (!TryComp<HologramBodyChipComponent>(bodyChip, out var bodyComp))
                continue;

            // Check if either has valid data
            if (brainComp.HoloMind == null && bodyComp.HologramPrototype == null)
                continue;

            string hologramName;
            bool isActive;

            if (IsPortable(console))
            {
                // Portable mode: check if blade server is in active holograms map
                isActive = component.ActiveHolograms.ContainsKey(bladeServerUid);
                if (isActive) activeCount++;
            }
            else
            {
                // Stationary mode: check server's linked hologram
                isActive = server != null && 
                           TryComp<HologramServerComponent>(server.Value, out var srvComp) && 
                           srvComp.LinkedHologram != null;
            }

            // Get name from mind or body chip
            if (brainComp.HoloMind != null && TryComp<MindComponent>(brainComp.HoloMind, out var mindComp))
            {
                hologramName = mindComp.CharacterName ?? "Unknown";
            }
            else if (bodyComp.HologramName != null)
            {
                hologramName = bodyComp.HologramName;
            }
            else if (bodyComp.HologramPrototype != null)
            {
                hologramName = MetaData(bodyChip).EntityName;
            }
            else
            {
                hologramName = "Unknown";
            }

            // Add blade server to list (using blade server UID as identifier)
            bladeServerList.Add(new BladeServerInfo(GetNetEntity(bladeServerUid), hologramName, isActive));
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

        // Always show the blade server panel if ShowBladeServerPanel is enabled
        var showBladeServerPanel = component.ShowBladeServerPanel;
        
        // Check if linked to hologram server machine (for stationary mode)
        var hasServer = IsPortable(console) || component.LinkedServer != null;
        
        // Always show map container if ShowMap is enabled (client will show "NO SERVER CONNECTED" when hasServer is false)
        var showMap = component.ShowMap;

        var state = new HologramConsoleBoundUserInterfaceState(
            bladeServerList, 
            activeHologram, 
            projectors,
            projectorCoordinates,
            IsPortable(console),
            batteryPercent,
            component.AllowHologramCarry,
            activeCount,
            component.MaxActiveHolograms,
            bladeServerList.Count,
            showMap,
            component.ShowProjectButton,
            component.ShowRecallButton,
            showBladeServerPanel,
            hasServer);
            
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
        // Get the specified blade server (args.BladeServerUid refers to blade server)
        var bladeServer = GetEntity(args.BladeServerUid);
        if (!Exists(bladeServer) || !TryComp<HologramBladeServerComponent>(bladeServer, out var bladeComp))
            return;

        // Get ItemSlots to access chips
        if (!TryComp<ItemSlotsComponent>(bladeServer, out var itemSlots))
            return;

        // Get brain chip using ItemSlotsSystem
        if (!_itemSlots.TryGetSlot(bladeServer, bladeComp.BrainChipSlot, out var brainSlot, itemSlots) || brainSlot.Item == null)
            return;

        // Get body chip using ItemSlotsSystem
        if (!_itemSlots.TryGetSlot(bladeServer, bladeComp.BodyChipSlot, out var bodySlot, itemSlots) || bodySlot.Item == null)
            return;

        var brainChip = brainSlot.Item.Value;
        var bodyChip = bodySlot.Item.Value;

        if (!TryComp<HologramBrainChipComponent>(brainChip, out var brainChipComp))
            return;

        if (!TryComp<HologramBodyChipComponent>(bodyChip, out var bodyChipComp))
            return;

        // Check if we have either a mind or a prototype
        if (brainChipComp.HoloMind == null && bodyChipComp.HologramPrototype == null)
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
            if (bodyChipComp.HologramPrototype != null)
            {
                holo = Spawn(bodyChipComp.HologramPrototype, spawnCoords);
            }
            // Spawn from mind
            else if (brainChipComp.HoloMind != null)
            {
                _hologram.TryGenerateHumanoidHologram(brainChipComp.HoloMind.Value, spawnCoords, out holo);
            }

            if (holo != null)
            {
                // Track the blade server
                component.ActiveHolograms[bladeServer] = holo.Value;
                
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
            if (bodyChipComp.HologramPrototype != null)
            {
                holo = Spawn(bodyChipComp.HologramPrototype, projectorCoords);
            }
            // Spawn from mind
            else if (brainChipComp.HoloMind != null)
            {
                _hologram.TryGenerateHumanoidHologram(brainChipComp.HoloMind.Value, projectorCoords, out holo);
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
        UpdateBriefcaseAppearance(console, component);
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
        UpdateBriefcaseAppearance(console, component);
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
            
            foreach (var bladeServer in toRemove)
                component.ActiveHolograms.Remove(bladeServer);
        }
    }
}
