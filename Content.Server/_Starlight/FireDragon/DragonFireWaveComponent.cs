using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.FireDragon;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class DragonFireWaveComponent : Component
{
    [DataField] public EntProtoId GroundFirePrototype = "FireDragonGroundFire";
    [DataField] public float BreathStepDistance = 0.25f;
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

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextStep;
}
