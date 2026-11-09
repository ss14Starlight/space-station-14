using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.FireDragon;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class AncientFlameProjectileComponent : Component
{
    /// <summary>
    /// Ground fire prototype spawned along the projectile trail and at the impact point.
    /// </summary>
    [DataField] public EntProtoId GroundFirePrototype = "FireDragonGroundFire";

    /// <summary>
    /// Time between samples of the projectile's path.
    /// </summary>
    [DataField] public TimeSpan TrailInterval = TimeSpan.FromSeconds(0.1);

    /// <summary>
    /// Maximum distance in meters between interpolated trail samples.
    /// </summary>
    [DataField] public float TrailSampleSpacing = 0.3f;

    /// <summary>
    /// Maximum number of interpolated samples processed in one trail update.
    /// </summary>
    [DataField] public int MaxTrailSamples = 128;

    /// <summary>
    /// Radius in meters of the ground fire created on impact.
    /// </summary>
    [DataField] public float ImpactFireRadius = 2.5f;

    /// <summary>
    /// Next time the projectile's trail is sampled.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextTrail;
    public MapCoordinates? LastPosition;
    public HashSet<(EntityUid Grid, Vector2i Tile)> BurnedTiles = [];
    public bool Impacted;
}
