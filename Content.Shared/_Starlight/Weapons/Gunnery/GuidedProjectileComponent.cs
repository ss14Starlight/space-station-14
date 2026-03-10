using System.Numerics;

namespace Content.Shared._Starlight.Weapons.Gunnery;

/// <summary>
/// A simple marker that lets the gunnery console identify shuttle-mounted guns
/// on the shuttle's grid. Add this component to any gun entity prototype that
/// should appear as a cannon blip in the gunnery console radar.
/// </summary>
[RegisterComponent]
public sealed partial class GunneryTrackableComponent : Component { }

/// <summary>
/// Marks a projectile as remotely guidable via a <see cref="GunneryConsoleComponent"/>.
/// The server's <c>GuidedProjectileSystem</c> steers the projectile's linear
/// velocity toward <see cref="SteeringTarget"/> each tick, limited by
/// <see cref="TurnRate"/>.
/// </summary>
[RegisterComponent]
public sealed partial class GuidedProjectileComponent : Component
{
    /// <summary>
    /// Maximum angular steering rate in degrees per second.
    /// Higher values allow tighter turns.
    /// </summary>
    [DataField]
    public float TurnRate = 180f;

    // ── Server-only runtime state ───────────────────────────────────────────

    /// <summary>The gunnery console entity that is currently guiding this projectile.</summary>
    public EntityUid? Controller;

    /// <summary>
    /// Map-space target position that the projectile should steer toward.
    /// Updated every tick by <c>GunneryConsoleSystem</c> when the player holds LMB.
    /// </summary>
    public Vector2 SteeringTarget;

    /// <summary>Whether active guidance is currently being applied this frame.</summary>
    public bool Active;
}
