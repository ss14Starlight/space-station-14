using Robust.Shared.GameStates;

namespace Content.Shared.Station.Components;

/// <summary>
/// Stores core information about a station, namely its config and associated grids.
/// All station entities will have this component.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedStationSystem))]
public sealed partial class StationDataComponent : Component
{
    /// <summary>
    /// The game map prototype, if any, associated with this station.
    /// </summary>
    [DataField]
    public StationConfig? StationConfig;

    /// <summary>
    /// List of all grids this station is part of.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Grids = new();

    //Starlight begin
    /// <summary>
    /// All grids that are INITIALLY part of the station go here, as in anything that had BecomesStationComponent on it.
    /// This is used to track if a player is on the "main station" or not.
    /// </summary>
    [ViewVariables] public HashSet<EntityUid> MainGrids = [];
    //Starlight end
}
