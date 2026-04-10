using Content.Shared.Medical.SuitSensor;
using Robust.Shared.Map;

namespace Content.Server.Medical.CrewMonitoring;

[RegisterComponent]
[Access(typeof(CrewMonitoringServerSystem))]
public sealed partial class CrewMonitoringServerComponent : Component
{

    /// <summary>
    ///     List of all currently connected sensors to this server.
    /// </summary>
    public readonly Dictionary<string, SuitSensorStatus> SensorStatus = new();

    /// <summary>
    ///     After what time sensor consider to be lost.
    /// </summary>
    [DataField("sensorTimeout"), ViewVariables(VVAccess.ReadWrite)]
    public float SensorTimeout = 10f;

    /// <summary>
    ///     STARLIGHT: Paging statuses per sensor (key is sender address).
    /// </summary>
    public readonly Dictionary<string, SuitSensorPagingStatus> PagingStatus = new();

    /// <summary>
    ///     STARLIGHT: How long to wait since sensor was first seen
    /// </summary>
    public TimeSpan PagingPageAfterDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     STARLIGHT: How long to not see a sensor before forgetting it.
    /// </summary>
    public TimeSpan PagingForgetAfterDuration = TimeSpan.FromSeconds(8);

}
