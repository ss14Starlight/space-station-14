using Content.Shared.Atmos;
using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._PV.Terraforming;

[RegisterComponent, NetworkedComponent]
public sealed partial class TerraformerComponent : Component
{
    [DataField]
    public float Radius = 5f;

    [DataField]
    public float Fuel = 0f;

    [DataField]
    public float MaxFuel = 100f;

    [DataField]
    public float FuelPerSecond = 1f;

    [DataField]
    public float FuelPerBiomass = 10f;

    [DataField]
    public float TileConvertCooldown = 2f;

    /// <summary>
    /// Research points awarded to the linked research server for every successfully converted tile.
    /// </summary>
    [DataField]
    public int SciencePointsPerTile = 5;

    [DataField]
    public int TilesTerraformed = 0;

    [DataField]
    public float Accumulator = 0f;

    [DataField]
    public bool Active = true;

    [DataField]
    public ProtoId<StackPrototype> BiomassStack = "Biomass";

    [DataField]
    public List<string> SourceTiles = new();

    [DataField]
    public string TargetTile = "FloorGrass";

    [DataField]
    public float AtmosCooldown = 1f;

    [DataField]
    public float AtmosAccumulator = 0f;

    [DataField]
    public float TargetPressure = Atmospherics.OneAtmosphere;

    [DataField]
    public float MaxPressure = 110f;

    [DataField]
    public float GasMolesPerTile = 0.25f;

    [DataField]
    public bool ScrubGases = true;

    [DataField]
    public float ScrubCooldown = 1f;

    [DataField]
    public float ScrubAccumulator = 0f;

    [DataField]
    public float ScrubMolesPerTile = 1f;

    [DataField]
    public float TargetScrubMoles = 0f;

    [DataField]
    public List<Gas> ScrubbedGases = new()
    {
        Gas.CarbonDioxide
    };

    [DataField]
    public bool CreateBarriers = true;

    [DataField]
    public string BarrierPrototype = "TerraformerAtmosBarrier";

    /// <summary>
    /// Distance from the Terraformer where the barrier outline should spawn.
    /// If set to 0 or below, Radius is used instead.
    /// </summary>
    [DataField]
    public float BarrierRadius = 0f;

    /// <summary>
    /// How often the Terraformer network checks and repairs the shared barrier outline.
    /// This does not delete and respawn the entire outline anymore; it only removes invalid barriers and spawns missing ones.
    /// </summary>
    [DataField]
    public float BarrierRefreshCooldown = 6f;

    [DataField]
    public float BarrierRefreshAccumulator = 0f;

    /// <summary>
    /// Forces this Terraformer's grid to repair its shared barrier outline on the next update.
    /// Used when another Terraformer turns off or is deleted.
    /// </summary>
    [DataField]
    public bool ForceBarrierRefresh = false;

    /// <summary>
    /// Runtime list of barrier entities spawned for this Terraformer's current barrier network.
    /// The repaired barrier system stores the shared grid outline on one Terraformer in the network.
    /// </summary>
    public List<EntityUid> SpawnedBarriers = new();

    [DataField]
    public bool SpawnTrees = true;

    /// <summary>
    /// Maximum amount of trees this Terraformer may spawn over its lifetime.
    /// </summary>
    [DataField]
    public int MaxSpawnedTrees = 4;

    [DataField]
    public int SpawnedTrees = 0;

    [DataField]
    public float TreeSpawnAccumulator = 0f;

    [DataField]
    public float TreeSpawnCooldown = 20f;

    [DataField]
    public float TreeSpawnChance = 0.25f;

    /// <summary>
    /// Normal tree prototype. FloraTree randomly chooses one of the normal tree sprites.
    /// </summary>
    [DataField]
    public string TreePrototype = "FloraTree";

    /// <summary>
    /// Tile prototype IDs that may receive spawned trees.
    /// Keep this to grass tiles so trees do not appear on paths or other converted floor types.
    /// </summary>
    [DataField]
    public List<string> TreeSpawnTiles = new()
    {
        "FloorGrass"
    };
}
