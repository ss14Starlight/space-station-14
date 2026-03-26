// Starlight: Event for spells that fire multiple projectiles in a spread pattern
using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Magic.Events;

/// <summary>
/// Starlight: Fires multiple projectiles in a spread pattern, like a shotgun blast.
/// Used for spells that need to shoot 2-3 projectiles with a tight spread.
/// </summary>
public sealed partial class MultiProjectileSpellEvent : WorldTargetActionEvent
{
    /// <summary>
    /// Starlight: What entity should be spawned for each projectile.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Prototype;

    /// <summary>
    /// Starlight: How many projectiles to spawn.
    /// </summary>
    [DataField]
    public int ProjectileCount = 3;

    /// <summary>
    /// Starlight: The spread angle in degrees. Total spread from center.
    /// For example, 12 degrees means projectiles spread 6 degrees left to 6 degrees right.
    /// </summary>
    [DataField]
    public float SpreadDegrees = 12f;
}
