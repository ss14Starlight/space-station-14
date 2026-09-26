using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Magic.Events;

/// <summary>
/// Fires multiple projectiles in a spread pattern, like a shotgun blast.
/// Used for spells that need to shoot 2-3 projectiles with a tight spread.
/// </summary>
public sealed partial class MultiProjectileSpellEvent : WorldTargetActionEvent
{
    /// <summary>
    /// What entity should be spawned for each projectile.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Prototype;

    /// <summary>
    /// How many projectiles to spawn.
    /// </summary>
    [DataField]
    public int ProjectileCount = 3;

    /// <summary>
    /// The spread angle in degrees. Total spread from center.
    /// For example, 12 degrees means projectiles spread 6 degrees left to 6 degrees right.
    /// </summary>
    [DataField]
    public float SpreadDegrees = 12f;
}
