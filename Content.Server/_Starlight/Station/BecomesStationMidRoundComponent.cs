using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Station;

/// <summary>
/// Similar to BecomesStation, but causes it to initialize on grid/map spawn instead of solely on gamemap load.
/// </summary>
[RegisterComponent]
[Access(typeof(GameTicker), typeof(StationSystem))]
public sealed partial class BecomesStationMidRoundComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)] public string? InitializedId = null;
    [DataField(required: true)] public string? Id = null;
    [DataField] public List<EntProtoId> BaseStationProtos = default!; // will combine all components from specified station protos
    [DataField] public bool AllowFTLDestination; // whether you can inherently FTL to the station or not.
    [DataField] public bool UseEmergencyShuttle; // false will ignore any emergency shuttle related settings. | Prevents adding emergency shuttle comp
    [DataField] public bool UseArmories; // whether to spawn armories or not.
    [DataField] public bool UseArrivals; // whether to add to arrivals rotation or not.
    [DataField] public bool AllowDungeonSpawn; // allow a new dungeon to spawn or not | Prevents spawning dungeon regardless of gridspawn
    [DataField] public bool AllowCargoShuttle; // allows the cargo shuttle "ferry" to spawn or not.
    [DataField] public string[] AllowedGridSpawns; // whitelisted gridspawn keys. Only affects GridSpawn groups, not DungeonSpawns.
    [DataField] public string? EmergencyShuttleOverridePath = null;
    [DataField] public Dictionary<ProtoId<JobPrototype>, int>? AvailableJobs = null; // null = no jobs
}