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
    /// Distance from the Terraformer where the barrier ring should spawn.
    /// If set to 0 or below, Radius is used instead.
    /// </summary>
    [DataField]
    public float BarrierRadius = 0f;

    /// <summary>
    /// How often the Terraformer rebuilds its barrier ring.
    /// This allows overlapping Terraformer fields to merge.
    /// </summary>
    [DataField]
    public float BarrierRefreshCooldown = 2f;

    [DataField]
    public float BarrierRefreshAccumulator = 0f;

    /// <summary>
    /// Forces this Terraformer to rebuild its barrier ring on its next update.
    /// Used when another Terraformer turns off or is deleted.
    /// </summary>
    [DataField]
    public bool ForceBarrierRefresh = false;

    /// <summary>
    /// Runtime list of barrier entities spawned by this Terraformer.
    /// </summary>
    public List<EntityUid> SpawnedBarriers = new();
}