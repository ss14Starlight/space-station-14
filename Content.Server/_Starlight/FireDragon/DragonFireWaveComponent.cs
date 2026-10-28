using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.FireDragon;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class DragonFireWaveComponent : Component
{
    /// <summary>
    /// Ground fire prototype spawned as the wave advances.
    /// </summary>
    [DataField] public EntProtoId GroundFirePrototype = "FireDragonGroundFire";

    /// <summary>
    /// Distance in tiles advanced by each firebreath update.
    /// </summary>
    [DataField] public float BreathStepDistance = 0.25f;

    /// <summary>
    /// Optional lifetime override for flames spawned by this wave.
    /// </summary>
    [DataField] public TimeSpan? GroundFireLifetimeOverride;

    public EntityUid Source;
    public EntityUid Grid;
    public Vector2i Origin;
    public Vector2 Forward;
    public Vector2 BreathOrigin;
    public int Width;
    public int Length;
    public int Step;
    public bool Rings;
    public float Distance;
    public List<(Vector2 Position, float Distance)> BreathPositions = new();
    public int NextBreathPosition;
    public TimeSpan Interval;

    /// <summary>
    /// Next time the fire wave advances.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextStep;
}
