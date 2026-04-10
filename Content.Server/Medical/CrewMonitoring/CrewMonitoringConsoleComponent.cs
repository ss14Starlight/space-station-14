using Content.Shared.Medical.SuitSensor;
using Robust.Shared.Audio; // Starlight

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
    ///     STARLIGHT: Whether paging is enabled.
    /// </summary>
    [DataField]
    public bool PagingEnabled = true;

    /// <summary>
    ///     STARLIGHT: The sound to play when paged.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier PagingSound = new SoundPathSpecifier("/Audio/_Starlight/Effects/crew_monitor_alert.ogg");

    /// <summary>
    ///     STARLIGHT: Paging sound audio parameters.
    /// </summary>
    [DataField] public AudioParams PagingSoundParams = AudioParams.Default
        .WithVolume(-2f)
        .WithMaxDistance(5);

    /// <summary>
    ///     STARLIGHT: Whether the paging sound should play local to the entity that contains the component. Used for AI.
    /// </summary>
    [DataField]
    public bool PagingSoundLocal = false;

    /// <summary>
    ///     STARLIGHT: When the last paging trigger was received.
    /// </summary>
    [DataField]
    public TimeSpan LastPagingTriggerReceivedAt = TimeSpan.Zero;

    /// <summary>
    ///     STARLIGHT: The delay between paging visuals getting enabled and timing out if the UI isn't interacted with.
    /// </summary>
    [DataField]
    public TimeSpan PagingVisualsTimeoutDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     STARLIGHT: When the paging visuals timeout.
    /// </summary>
    [DataField]
    public TimeSpan PagingVisualsTimeoutAt = TimeSpan.Zero;

    /// <summary>
    ///     After what time sensor consider to be lost.
    /// </summary>
    [DataField("sensorTimeout"), ViewVariables(VVAccess.ReadWrite)]
    public float SensorTimeout = 10f;
}
