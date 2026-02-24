using System; // Starlight
using System.Linq;
using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Systems;
using Content.Shared.PowerCell;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Pinpointer;
using Content.Server.Silicons.StationAi;
using Robust.Server.GameObjects;
using Robust.Shared.Log; // Starlight
using Content.Shared.Silicons.StationAi; // Starlight
using Robust.Shared.Map; // Starlight
using Robust.Shared.Timing; // Starlight

namespace Content.Server.Medical.CrewMonitoring;

public sealed class CrewMonitoringConsoleSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly StationAiSystem _stationAiSystem = default!; // Starlight
    [Dependency] private readonly IGameTiming _gameTiming = default!; // Starlight

    private readonly ISawmill _sawmill = Logger.GetSawmill("crewmonitoring"); // Starlight

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, CrewMonitoringWarpRequestMessage>(OnWarpRequest); // Starlight
    }

    /// <summary>
    ///     STARLIGHT: Periodically update the UI, even if there is no crew monitoring server transmitting.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        
        // Periodically update the UI, per console.
        var consoles = EntityQueryEnumerator<CrewMonitoringConsoleComponent>();
        while (consoles.MoveNext(out var id, out var console))
        {
            if (console.LastInterfaceUpdate + console.InterfaceUpdateRate > _gameTiming.CurTime)
                return;
            
            UpdateUserInterface(id, console);
        }
    }

    private void OnRemove(EntityUid uid, CrewMonitoringConsoleComponent component, ComponentRemove args)
    {
        component.ConnectedSensors.Clear();
    }

    private void OnPacketReceived(EntityUid uid, CrewMonitoringConsoleComponent component, DeviceNetworkPacketEvent args)
    {
        var payload = args.Data;

        // Check command
        if (!payload.TryGetValue(DeviceNetworkConstants.Command, out string? command))
            return;

        if (command != DeviceNetworkConstants.CmdUpdatedState)
            return;

        if (!payload.TryGetValue(SuitSensorConstants.NET_STATUS_COLLECTION, out Dictionary<string, SuitSensorStatus>? sensorStatus))
            return;

        component.ConnectedSensors = sensorStatus;
        component.LastSensorDataReceivedAt = _gameTiming.CurTime; // Starlight
        UpdateUserInterface(uid, component);
    }

    private void OnUIOpened(EntityUid uid, CrewMonitoringConsoleComponent component, BoundUIOpenedEvent args)
    {
        if (!_cell.TryUseActivatableCharge(uid))
            return;

        UpdateUserInterface(uid, component);
    }

    private void UpdateUserInterface(EntityUid uid, CrewMonitoringConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;
        component.LastInterfaceUpdate = _gameTiming.CurTime; // Starlight

        if (!_uiSystem.IsUiOpen(uid, CrewMonitoringUIKey.Key))
            return;

        // The grid must have a NavMapComponent to visualize the map in the UI
        var xform = Transform(uid);

        if (xform.GridUid != null)
            EnsureComp<NavMapComponent>(xform.GridUid.Value);

        // Update all sensors info
        var allSensors = component.ConnectedSensors.Values.ToList();
        _uiSystem.SetUiState(uid, CrewMonitoringUIKey.Key, new CrewMonitoringState(_gameTiming.CurTime, component.LastSensorDataReceivedAt, allSensors)); // Starlight: Add two timestamps
    }
    // Starlight-start
    private void OnWarpRequest(EntityUid uid, CrewMonitoringConsoleComponent component, ref CrewMonitoringWarpRequestMessage args)
    {
        if (args.Actor is not { Valid: true } actor)
        {
            _sawmill.Warning($"Received crew monitor warp request with no valid actor for console {uid}.");
            return;
        }

        if (!HasComp<StationAiHeldComponent>(actor))
        {
            _sawmill.Warning($"Entity {Name(actor)} ({actor}) attempted to warp via crew monitor {uid} without StationAiHeldComponent.");
            return;
        }

        EntityCoordinates coordinates;
        try
        {
            coordinates = GetCoordinates(args.Coordinates);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to convert network coordinates {args.Coordinates} for crew monitor warp request from {Name(actor)} ({actor}).", e);
            return;
        }

        if (!_stationAiSystem.TryWarpEyeToCoordinates(actor, coordinates))
        {
            _sawmill.Debug($"Crew monitor warp request from {Name(actor)} ({actor}) to {coordinates} was rejected.");
        }
    }
    // Starlight-end
}
