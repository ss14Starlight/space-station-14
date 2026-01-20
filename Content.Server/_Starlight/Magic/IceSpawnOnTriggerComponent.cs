// Starlight: Ice Storm spell component
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Magic;

/// <summary>
/// Starlight: Component for projectiles that spawn IceCrust debris in a circular pattern when they hit something.
/// Specifically checks if the target is a humanoid mob before creating the ice circle effect.
/// </summary>
[RegisterComponent]
public sealed partial class IceSpawnOnTriggerComponent : Component
{
    /// <summary>
    /// Starlight: The radius of the ice circle to spawn when hitting a humanoid (in tiles).
    /// Default is 2 tiles for a smaller area.
    /// </summary>
    [DataField("radius")]
    public float Radius = 2f;

    /// <summary>
    /// Starlight: Only spawn the ice circle if the projectile hits a humanoid mob.
    /// If false, will spawn ice on any trigger event.
    /// </summary>
    [DataField("requireHumanoid")]
    public bool RequireHumanoid = true;

    /// <summary>
    /// Starlight: Probability (0.0 to 1.0) of spawning ice instead of snow.
    /// </summary>
    [DataField("iceChance")]
    public float IceChance = 0.60f;

    /// <summary>
    /// Starlight: The entity ID for ice crust debris (IceCrust).
    /// </summary>
    [DataField("iceEntityId")]
    public string IceEntityId = "IceCrust";

    /// <summary>
    /// Starlight: The tile ID for snow floor (FloorSnow).
    /// </summary>
    [DataField("snowTileId")]
    public string SnowTileId = "FloorSnow";
}
