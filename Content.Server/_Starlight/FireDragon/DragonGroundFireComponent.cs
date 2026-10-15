using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.FireDragon;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class DragonGroundFireComponent : Component
{
    [DataField] public TimeSpan Lifetime = TimeSpan.FromSeconds(6);
    [DataField] public TimeSpan DamageInterval = TimeSpan.FromSeconds(0.5);
    [DataField] public float ContactRange = 0.7f;
    [DataField] public DamageSpecifier Damage = new();
    [DataField] public float FireStacks = 1f;
    [DataField] public EntityUid? Source;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ExpiresAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDamage;
}
