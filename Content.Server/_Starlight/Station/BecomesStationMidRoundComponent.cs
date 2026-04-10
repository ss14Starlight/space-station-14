using Content.Server._Starlight.Commands;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Station;

/// <summary>
/// Similar to BecomesStation, but causes it to initialize on grid/map spawn instead of solely on gamemap load.
/// </summary>
[RegisterComponent]
[Access(typeof(GameTicker), typeof(StationSystem), typeof(ShuttleSystem), typeof(StationInitCommand))]
public sealed partial class BecomesStationMidRoundComponent : Component
{
    /// <summary>
    /// Set this to true if you are mapping.
    /// False by default as it will avoid an initialization attempt when adding to a grid on an already initialized map.
    /// </summary>
    [DataField] public bool Initialize;
    /// <summary>
    /// Initialized ID of station
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)] public string? InitializedId = null;
    /// <summary>
    /// ID of station
    /// </summary>
    [DataField(required: true)] public string? Id = null;
    /// <summary>
    /// Prototypes to use when constructing station
    /// </summary>
    [DataField] public List<EntProtoId> BaseStationProtos = [];
    /// <summary>
    /// Always allow FTLing to this station
    /// </summary>
    [DataField] public bool AllowFTLDestination;
    /// <summary>
    /// Whether to use an emergency shuttle
    /// </summary>
    [DataField] public bool UseEmergencyShuttle;
    /// <summary>
    /// Whether to add armories or not
    /// </summary>
    [DataField] public bool UseArmories;
    /// <summary>
    /// Whether to add an arrivals shuttle or not
    /// </summary>
    [DataField] public bool UseArrivals;
    /// <summary>
    /// Prevents dungeons from spawning
    /// </summary>
    [DataField] public bool AllowDungeonSpawn;
    /// <summary>
    /// Allows the cargo ferry to spawn
    /// </summary>
    [DataField] public bool AllowCargoShuttle;
    /// <summary>
    /// Whitelisted gridspawns
    /// </summary>
    [DataField] public HashSet<string> AllowedGridSpawns = [];
    /// <summary>
    /// Overrides the emergency shuttle grid
    /// </summary>
    [DataField] public string? EmergencyShuttleOverridePath = null;
    /// <summary>
    /// Jobs available on init
    /// </summary>
    [DataField] public Dictionary<ProtoId<JobPrototype>, int> AvailableJobs = [];
    /// <summary>
    /// Allows events to target this station
    /// </summary>
    [DataField] public bool AllowEvents;

    [DataField] public EntProtoId DefaultBaseStationPrototype = "StandardNanotrasenStation";
}
