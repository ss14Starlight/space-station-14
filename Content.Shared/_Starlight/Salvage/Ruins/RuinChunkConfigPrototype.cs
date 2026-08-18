using System.Collections.Immutable;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Salvage.Ruins;

/// <summary>
/// Configuration for procedural ruin chunk generation (flood-fill size, damage RNG, tile costs).
/// </summary>
[Prototype]
public sealed partial class RuinChunkConfigPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Cost budget per flood-fill stage. Higher values produce larger chunks.
    /// </summary>
    [DataField]
    public int FloodFillPoints = 25;

    /// <summary>
    /// Number of flood-fill stages. Each stage expands from the previous frontier for irregular shapes.
    /// </summary>
    [DataField]
    public int FloodFillStages = 7;

    /// <summary>
    /// Chance that a wall entity is not spawned (destroyed).
    /// </summary>
    [DataField]
    public float WallDestroyChance = 0.30f;

    /// <summary>
    /// Chance that a window is damaged after spawn.
    /// </summary>
    [DataField]
    public float WindowDamageChance = 0.10f;

    /// <summary>
    /// Chance that a floor tile is replaced with lattice. Lattice is never damaged further.
    /// </summary>
    [DataField]
    public float FloorToLatticeChance = 0.15f;

    /// <summary>
    /// When false, salvage mob spawners are omitted from the wreck biome.
    /// </summary>
    [DataField]
    public bool SpawnMobs = true;

    /// <summary>
    /// Path cost for wall entities / wall tiles.
    /// </summary>
    [DataField]
    public int WallCost = 20;

    /// <summary>
    /// Path cost for window entities by substring match on prototype ID (longest match wins).
    /// </summary>
    [DataField]
    public Dictionary<string, int> WindowCosts = new()
    {
        ["firelock"] = 10,
        ["windoorsecure"] = 10,
        ["windoor"] = 4,
        ["reinforceddirectional"] = 10,
        ["directional"] = 10,
        ["reinforceddiagonal"] = 10,
        ["diagonal"] = 10,
        ["reinforced"] = 10,
    };

    /// <summary>
    /// Default path cost for windows when no WindowCosts pattern matches.
    /// </summary>
    [DataField]
    public int DefaultWindowCost = 4;

    /// <summary>
    /// Path cost for tile definitions by substring match on tile ID (longest match wins).
    /// </summary>
    [DataField]
    public Dictionary<string, int> TileCosts = new()
    {
        ["directionalglass"] = 8,
        ["reinforcedglass"] = 12,
        ["glass"] = 8,
        ["grille"] = 4,
    };

    /// <summary>
    /// Default path cost for floor tiles when no TileCosts pattern matches.
    /// </summary>
    [DataField]
    public int DefaultTileCost = 1;

    /// <summary>
    /// Path cost treated as impassable space for flood-fill.
    /// </summary>
    [DataField]
    public int SpaceCost = 9999;

    /// <summary>
    /// Tile definition IDs treated as wall tiles for cost map building.
    /// </summary>
    [DataField]
    public ImmutableList<string> WallTileIds = ImmutableList.CreateRange(new[]
    {
        "WallSolid",
        "WallReinforced",
        "WallReinforcedRust",
        "WallSolidRust",
        "WallSolidDiagonal",
        "WallReinforcedDiagonal",
    });

    /// <summary>
    /// Entity prototype IDs treated as walls when parsing ruin source maps.
    /// </summary>
    [DataField]
    public ImmutableList<EntProtoId> WallPrototypes = ImmutableList.CreateRange(new EntProtoId[]
    {
        "WallSolid",
        "WallReinforced",
        "WallReinforcedRust",
        "WallSolidRust",
        "WallSolidDiagonal",
        "WallReinforcedDiagonal",
        "WallShuttle",
        "WallShuttleDiagonal",
        "WallPlastitanium",
        "WallPlastitaniumDiagonal",
        "WallMiningDiagonal",
        "WallWood",
    });

    /// <summary>
    /// Entity prototype IDs treated as windows/windoors/firelocks when parsing ruin source maps.
    /// Locked Windoor*/Firelock* variants are also matched by prefix when these bases are listed.
    /// </summary>
    [DataField]
    public ImmutableList<EntProtoId> WindowPrototypes = ImmutableList.CreateRange(new EntProtoId[]
    {
        "Window",
        "WindowDirectional",
        "ReinforcedWindow",
        "WindowReinforcedDirectional",
        "WindowDiagonal",
        "ReinforcedWindowDiagonal",
        "TintedWindow",
        "WindowFrostedDirectional",
        "ShuttleWindow",
        "ShuttleWindowDiagonal",
        "PlastitaniumWindow",
        "PlastitaniumWindowDiagonal",
        "ReinforcedPlasmaWindow",
        "ReinforcedPlasmaWindowDiagonal",
        "PlasmaReinforcedWindowDirectional",
        "ReinforcedUraniumWindow",
        "ReinforcedUraniumWindowDiagonal",
        "UraniumReinforcedWindowDirectional",
        "Windoor",
        "WindoorSecure",
        "WindoorPlasma",
        "WindoorSecurePlasma",
        "WindoorClockwork",
        "Firelock",
        "FirelockGlass",
        "FirelockEdge",
        "FirelockFrame",
        "WindoorAssembly",
        "WindoorAssemblySecure",
        "WindoorAssemblyPlasma",
        "WindoorAssemblySecurePlasma",
    });
}
