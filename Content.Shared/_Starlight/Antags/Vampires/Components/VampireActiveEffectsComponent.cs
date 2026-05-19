using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using System.Numerics;

namespace Content.Shared._Starlight.Antags.Vampires.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireRejuvenateComponent : Component
{
    [DataField] public int TicksRemaining;

    [DataField] public TimeSpan TickInterval = TimeSpan.FromSeconds(3.5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan NextTick;

    [DataField] public Dictionary<string, FixedPoint2> HealGroups = new();

    [DataField] public Dictionary<string, FixedPoint2> HealTypes = new();
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireGlareDotComponent : Component
{
    [DataField] public EntityUid Source;

    [DataField] public float StaminaDamage;

    [DataField] public int TicksRemaining;

    [DataField] public TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan NextTick;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampirePacifyComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan EndTime;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireInvisibilityComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan EndTime;

    [DataField] public bool HadStealthComponent;

    [DataField] public bool PreviousStealthEnabled;

    [DataField] public float PreviousStealthVisibility = 1f;
}

[RegisterComponent]
public sealed partial class ActiveVampireHemomancerClawsComponent : Component
{
    [DataField] public EntityUid? SpawnedClaws;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireBloodBondComponent : Component
{
    [DataField] public EntityUid ActionEntity;

    [DataField] public float Range;

    [DataField] public int BloodCostPerTick;

    [DataField] public TimeSpan TickInterval = TimeSpan.FromSeconds(2);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan NextTick;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireBloodBringersRiteComponent : Component
{
    [DataField] public int TicksRemaining = 150;

    [DataField] public int BloodCost;

    [DataField] public float Range;

    [DataField] public FixedPoint2 Damage;

    [DataField] public FixedPoint2 HealBrute;

    [DataField] public FixedPoint2 HealBurn;

    [DataField] public float HealStamina;

    [DataField] public TimeSpan TickInterval = TimeSpan.FromSeconds(2);

    [DataField] public EntProtoId BeamPrototype;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan NextTick;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireEternalDarknessComponent : Component
{
    [DataField] public int TicksRemaining;

    [DataField] public int CurrentTick;

    [DataField] public int BloodPerTick;

    [DataField] public int TempDropInterval;

    [DataField] public float FreezeRadius;

    [DataField] public float TargetFreezeTemp;

    [DataField] public float TempDropPerInterval;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan NextTick;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireShadowBoxingComponent : Component
{
    [DataField] public EntityUid Target;

    [DataField] public float Range;

    [DataField] public int BrutePerTick;

    [DataField] public SoundSpecifier? HitSound;

    [DataField] public EntProtoId PunchEffectPrototype = "WeaponArcPunch";

    [DataField] public TimeSpan TickInterval = TimeSpan.FromSeconds(0.9);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan NextTick;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan EndTime;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PendingVampireTendrilsComponent : Component
{
    [DataField] public EntityCoordinates TileCoordinates;

    [DataField] public EntProtoId PuddlePrototype = "PuddleBlood";

    [DataField] public float TargetRange;

    [DataField] public TimeSpan SlowDuration;

    [DataField] public float SlowMultiplier;

    [DataField] public FixedPoint2 ToxinDamage;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan TriggerTime;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireDemonicGraspComponent : Component
{
    [DataField] public EntityCoordinates StartCoordinates;

    [DataField] public EntityUid GridUid;

    [DataField] public Vector2 Direction;

    [DataField] public int CurrentTile;

    [DataField] public int MaxTiles;

    [DataField] public TimeSpan TileInterval = TimeSpan.FromMilliseconds(50);

    [DataField] public TimeSpan ImmobilizeDuration;

    [DataField] public bool PullTarget;

    [DataField] public EntProtoId EffectPrototype = "VampireDemonicGraspEffect";

    [DataField] public EntProtoId ImmobilizedEffectPrototype = "VampireImmobilizedEffect";

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan NextTileTime;
}
