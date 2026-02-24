using Content.Shared.Medical.SuitSensor;
using Robust.Shared.Map; // Starlight
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.CrewMonitoring;

[Serializable, NetSerializable]
public enum CrewMonitoringUIKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class CrewMonitoringState : BoundUserInterfaceState
{
    public TimeSpan Timestamp; // Starlight: When this state was transmitted.
    public TimeSpan LastUpdate; // Starlight: The last time the console got an update from the monitoring server.
    public List<SuitSensorStatus> Sensors;

    public CrewMonitoringState(TimeSpan timestamp, TimeSpan lastUpdate, List<SuitSensorStatus> sensors) // Starlight
    {
        Timestamp = timestamp; // Starlight
        LastUpdate = lastUpdate; // Starlight
        Sensors = sensors;
    }
}
// Starlight-start
[Serializable, NetSerializable]
public sealed partial class CrewMonitoringWarpRequestMessage : BoundUserInterfaceMessage
{
    public NetCoordinates Coordinates;

    public CrewMonitoringWarpRequestMessage(NetCoordinates coordinates)
    {
        Coordinates = coordinates;
    }
}
// Starlight-end
