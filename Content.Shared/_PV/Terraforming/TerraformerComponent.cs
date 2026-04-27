using Robust.Shared.GameStates;

namespace Content.Shared._PV.Terraforming;

[RegisterComponent, NetworkedComponent]
public sealed partial class TerraformerComponent : Component
{
    /// <summary>
    /// Radius around the machine that can be terraformed.
    /// </summary>
    [DataField]
    public float Radius = 5f;

    /// <summary>
    /// Current fuel stored in the machine.
    /// For the first test, this can be set directly in YAML.
    /// Later this should be filled with Biomass.
    /// </summary>
    [DataField]
    public float Fuel = 100f;

    /// <summary>
    /// Fuel consumed per second while active.
    /// </summary>
    [DataField]
    public float FuelPerSecond = 1f;

    /// <summary>
    /// Seconds between tile conversions.
    /// </summary>
    [DataField]
    public float TileConvertCooldown = 2f;

    /// <summary>
    /// Internal timer.
    /// </summary>
    [DataField]
    public float Accumulator = 0f;

    /// <summary>
    /// Whether the machine is currently running.
    /// For the first test, this can be true by default.
    /// </summary>
    [DataField]
    public bool Active = true;

    /// <summary>
    /// Tile prototype IDs that this machine is allowed to convert.
    /// Example: FloorAsteroidSand
    /// </summary>
    [DataField]
    public List<string> SourceTiles = new();

    /// <summary>
    /// Tile prototype ID to convert into.
    /// Example: FloorGrass
    /// </summary>
    [DataField]
    public string TargetTile = "FloorGrass";
}