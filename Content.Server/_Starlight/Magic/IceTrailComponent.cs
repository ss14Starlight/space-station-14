// Ice trail component for projectiles that leave ice behind as they fly
namespace Content.Server._Starlight.Magic;

/// <summary>
/// Component for projectiles that spawn IceCrust debris while traveling.
/// Creates a trail of frozen debris behind the projectile as it flies through the air.
/// </summary>
[RegisterComponent]
public sealed partial class IceTrailComponent : Component
{
    /// <summary>
    /// How often to spawn IceCrust debris (in seconds).
    /// Default is 0.05 seconds for a more continuous trail.
    /// </summary>
    [DataField("spawnInterval")]
    public float SpawnInterval = 0.05f;

    /// <summary>
    /// Time accumulator for tracking when to spawn next entity.
    /// </summary>
    public float TimeAccumulator = 0f;

    /// <summary>
    /// Probability (0.0 to 1.0) of spawning ice instead of snow.
    /// </summary>
    [DataField("iceChance")]
    public float IceChance = 0.55f;

    /// <summary>
    /// The entity ID for ice crust debris (IceCrust).
    /// </summary>
    [DataField("iceEntityId")]
    public string IceEntityId = "IceCrust";

    /// <summary>
    /// The tile ID for snow floor (FloorSnow).
    /// </summary>
    [DataField("snowTileId")]
    public string SnowTileId = "FloorSnow";
}
