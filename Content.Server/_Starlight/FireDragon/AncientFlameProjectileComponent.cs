using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.FireDragon;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class AncientFlameProjectileComponent : Component
{
    [DataField] public EntProtoId GroundFirePrototype = "FireDragonGroundFire";
    [DataField] public TimeSpan TrailInterval = TimeSpan.FromSeconds(0.1);
    [DataField] public float TrailSampleSpacing = 0.3f;
    [DataField] public int MaxTrailSamples = 128;
    [DataField] public float ImpactFireRadius = 2.5f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextTrail;
    public MapCoordinates? LastPosition;
    public HashSet<(EntityUid Grid, Vector2i Tile)> BurnedTiles = [];
    public bool Impacted;
}
