using Content.Shared.Medical.SuitSensor;

namespace Content.Server.Medical.CrewMonitoring;

[RegisterComponent]
[AutoGenerateComponentPause] // Starlight
[Access(typeof(CrewMonitoringConsoleSystem))]
public sealed partial class CrewMonitoringConsoleComponent : Component
{
    /// <summary>
    ///     List of all currently connected sensors to this console.
    /// </summary>
    public Dictionary<string, SuitSensorStatus> ConnectedSensors = new();

    /// <summary>
    ///     STARLIGHT: The rate at which this console transmits UI updates to clients.
    /// </summary>
    [DataField]
    public TimeSpan InterfaceUpdateRate = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     STARLIGHT: The time at which this console last transmitted a UI update.
    /// </summary>
    [AutoPausedField]
    public TimeSpan LastInterfaceUpdate = TimeSpan.Zero;
    
    /// <summary>
    ///     STARLIGHT: When the last update was received. Used to determine if the server is online.
    /// </summary>
    [DataField]
    public TimeSpan LastSensorDataReceivedAt = TimeSpan.Zero;

    /// <summary>
    ///     After what time sensor consider to be lost.
    /// </summary>
    [DataField("sensorTimeout"), ViewVariables(VVAccess.ReadWrite)]
    public float SensorTimeout = 10f;
}
