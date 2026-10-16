using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.FireDragon;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class DragonGroundFireComponent : Component
{
    /// <summary>
    /// How long this patch of ground fire lasts.
    /// </summary>
    [DataField] public TimeSpan Lifetime = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Time between contact damage ticks; also limits damage from overlapping flames.
    /// </summary>
    [DataField] public TimeSpan DamageInterval = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Radius in meters used to find entities touching the fire.
    /// </summary>
    [DataField] public float ContactRange = 0.7f;

    /// <summary>
    /// Damage applied to an eligible entity on each contact tick.
    /// </summary>
    [DataField] public DamageSpecifier Damage = new();

    /// <summary>
    /// Fire stacks added to an eligible entity on each contact tick.
    /// </summary>
    [DataField] public float FireStacks = 1f;

    /// <summary>
    /// Entity responsible for the fire, used for damage attribution and faction checks.
    /// </summary>
    [DataField] public EntityUid? Source;

    /// <summary>
    /// Time when this patch of fire expires.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ExpiresAt;

    /// <summary>
    /// Next time this patch checks for contact damage.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDamage;
}
