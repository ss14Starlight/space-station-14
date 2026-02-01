namespace Content.Server.Maps;

/// <summary>
/// Manages which station map will be used for the next round.
/// </summary>
public interface IGameMapManager
{
    void Initialize();

    /// <summary>
    /// Returns all maps eligible to be played right now.
    /// </summary>
    /// <returns>enumerator of map prototypes</returns>
    IEnumerable<GameMapPrototype> CurrentlyEligibleMaps();

    /// <summary>
    /// Returns all maps that can be voted for.
    /// </summary>
    /// <returns>enumerator of map prototypes</returns>
    IEnumerable<GameMapPrototype> AllVotableMaps();

    /// <summary>
    /// Returns all maps.
    /// </summary>
    /// <returns>enumerator of map prototypes</returns>
    IEnumerable<GameMapPrototype> AllMaps();

    /// <summary>
    /// Gets the currently selected map
    /// </summary>
    /// <returns>selected map</returns>
    List<GameMapPrototype?> GetSelectedMaps();

    string GetMapString();

    /// <summary>
    /// Clears the selected map, if any
    /// </summary>
    void ClearSelectedMaps();

    /// <summary>
    /// Attempts to select the given map, checking eligibility criteria
    /// </summary>
    /// <param name="gameMap">map prototype</param>
    /// <returns>success or failure</returns>
    bool TrySelectMapIfEligible(string gameMap);
    bool TrySelectMapsIfEligible(List<string> gameMaps);

    int GetStationCount();

    /// <summary>
    /// Select the given map regardless of eligibility
    /// </summary>
    /// <param name="gameMap">map prototype</param>
    /// <returns>success or failure</returns>

    void SelectMaps(List<string> gameMaps);

    /// <summary>
    /// Selects a random map eligible map
    /// </summary>

    void SelectMapsRandom();

    /// <summary>
    /// Selects the map at the front of the rotation queue
    /// </summary>
    /// <returns>selected map</returns>
    void SelectMapsFromRotationQueue(bool markAsPlayed = false);

    /// <summary>
    /// Selects the map by following rules set in the config
    /// </summary>
    public void SelectMapsByConfigRules();

    /// <summary>
    /// Checks if the given map exists
    /// </summary>
    /// <param name="gameMap">name of the map</param>
    /// <returns>existence</returns>
    bool CheckMapExists(string gameMap);
}
