namespace Content.Server._Starlight.Salvage.VGRoid;

/// <summary>
/// Marks the generated round-start VGRoid grid and carries the expected placement range
/// used by the self-heal system.
/// </summary>
[RegisterComponent]
public sealed partial class VGRoidSpawnMarkerComponent : Component
{
    /// <summary>
    /// Populated from the owning DungeonSpawnGroup's minimumDistance when the grid is spawned.
    /// </summary>
    [DataField]
    public float MinimumEdgeDistance;

    /// <summary>
    /// Populated from the owning DungeonSpawnGroup's maximumDistance when the grid is spawned.
    /// </summary>
    [DataField]
    public float MaximumEdgeDistance;

    [DataField]
    public float DistanceTolerance = 16f;
}
