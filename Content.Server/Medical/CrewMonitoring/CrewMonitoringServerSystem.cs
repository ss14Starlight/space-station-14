using Content.Server.DeviceNetwork.Systems;
using Content.Server.Medical.SuitSensors;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.Medical.SuitSensor;
using Robust.Shared.Timing;
using Content.Shared.DeviceNetwork.Components;

namespace Content.Server.Medical.CrewMonitoring;

public sealed class CrewMonitoringServerSystem : EntitySystem
{
    [Dependency] private readonly SuitSensorSystem _sensors = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetworkSystem = default!;
    [Dependency] private readonly SingletonDeviceNetServerSystem _singletonServerSystem = default!;

    private const float UpdateRate = 3f;
    private float _updateDiff;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrewMonitoringServerComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CrewMonitoringServerComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
        SubscribeLocalEvent<CrewMonitoringServerComponent, DeviceNetServerDisconnectedEvent>(OnDisconnected);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // check update rate
        _updateDiff += frameTime;
        if (_updateDiff < UpdateRate)
            return;
        _updateDiff -= UpdateRate;

        var servers = EntityQueryEnumerator<CrewMonitoringServerComponent>();

        while (servers.MoveNext(out var id, out var server))
        {
            if (!_singletonServerSystem.IsActiveServer(id))
                continue;

            UpdateTimeout(id);
            BroadcastSensorStatus(id, server);
            UpdatePager(id, server); // Starlight
        }
    }

    /// <summary>
    /// Adds or updates a sensor status entry if the received package is a sensor status update
    /// </summary>
    private void OnPacketReceived(EntityUid uid, CrewMonitoringServerComponent component, DeviceNetworkPacketEvent args)
    {
        var sensorStatus = _sensors.PacketToSuitSensor(args.Data);
        if (sensorStatus == null)
            return;

        sensorStatus.Timestamp = _gameTiming.CurTime;
        component.SensorStatus[args.SenderAddress] = sensorStatus;
    }

    /// <summary>
    /// Clears the servers sensor status list
    /// </summary>
    private void OnRemove(EntityUid uid, CrewMonitoringServerComponent component, ComponentRemove args)
    {
        //component.SensorStatus.Clear(); // Starlight: Don't instantly wipe sensor list, let it time out instead.
        component.PagingStatus.Clear(); // Starlight
    }

    /// <summary>
    /// Drop the sensor status if it hasn't been updated for to long
    /// </summary>
    private void UpdateTimeout(EntityUid uid, CrewMonitoringServerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        foreach (var (address, sensor) in component.SensorStatus)
        {
            var dif = _gameTiming.CurTime - sensor.Timestamp;
            if (dif.Seconds > component.SensorTimeout)
                component.SensorStatus.Remove(address);
        }
    }
    
    /// <summary>
    /// STARLIGHT: Perform all pager-related update logic.
    /// </summary>
    private void UpdatePager(EntityUid uid, CrewMonitoringServerComponent component, DeviceNetworkComponent? device = null)
    {
        var seen = new HashSet<string>();
        
        if (!Resolve(uid, ref device))
            return;
        
        // Update paging status for all currently seen sensors.
        foreach (var (address, sensor) in component.SensorStatus)
        {
            var critOrDead = !sensor.IsAlive || (sensor.DamagePercentage != null && sensor.DamagePercentage >= 0.5);
            seen.Add(address);

            // If person isn't crit or dead, discard status if it exists and ignore.
            if (!critOrDead)
            {
                component.PagingStatus.Remove(address);
                continue;
            }
            
            // We know the sensor is eligible. Begin tracking if not already.
            component.PagingStatus.TryGetValue(address, out var pagingStatus);
            if (pagingStatus == null)
                component.PagingStatus[address] = pagingStatus = new SuitSensorPagingStatus(_gameTiming.CurTime);
            
            // Since we are in the loop of *seen* sensors, update last seen.
            pagingStatus.LastSeen = _gameTiming.CurTime;
            
            // Emit packet only when we haven't already and when enough time has passed.
            if (pagingStatus.Paged || pagingStatus.FirstSeen + component.PagingPageAfterDuration > _gameTiming.CurTime) continue;
            pagingStatus.Paged = true;
            
            // Broadcast a paging trigger to all listeners. Consoles themselves determine if they activate or not.
            _deviceNetworkSystem.QueuePacket(uid, null, new NetworkPayload()
            {
                [DeviceNetworkConstants.Command] = DeviceNetworkConstants.CmdUpdatedState,
                [SuitSensorConstants.NET_PAGING_SINCE] = pagingStatus.FirstSeen,
                [SuitSensorConstants.NET_JOB_DEPARTMENTS] = sensor.JobDepartments,
            }, device: device);
        }
        
        // Check for pagers we've tracked but not seen.
        foreach (var (address, pagingTracker) in component.PagingStatus)
        {
            if (seen.Contains(address))
                continue;
            
            // Forget the sensor if it was missing for too long.
            if (pagingTracker.LastSeen + component.PagingForgetAfterDuration <= _gameTiming.CurTime)
                component.PagingStatus.Remove(address);
        }
    }

    /// <summary>
    /// Broadcasts the status of all connected sensors
    /// </summary>
    private void BroadcastSensorStatus(EntityUid uid, CrewMonitoringServerComponent? serverComponent = null, DeviceNetworkComponent? device = null)
    {
        if (!Resolve(uid, ref serverComponent, ref device))
            return;

        var payload = new NetworkPayload()
        {
            [DeviceNetworkConstants.Command] = DeviceNetworkConstants.CmdUpdatedState,
            [SuitSensorConstants.NET_STATUS_COLLECTION] = serverComponent.SensorStatus
        };

        _deviceNetworkSystem.QueuePacket(uid, null, payload, device: device);
    }

    /// <summary>
    /// Clears sensor data on disconnect
    /// </summary>
    private void OnDisconnected(EntityUid uid, CrewMonitoringServerComponent component, ref DeviceNetServerDisconnectedEvent _)
    {
        component.SensorStatus.Clear();
        component.PagingStatus.Clear(); // Starlight
    }
}
