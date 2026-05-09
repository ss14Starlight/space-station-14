using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Weapons.Gunnery;

/// <summary>
/// Marks an entity as a gunnery console — a targeting radar that can remotely aim and fire
/// shuttle-mounted cannons and guide EMP rockets.
/// </summary>
[RegisterComponent]
[AutoGenerateComponentPause] // Starlight
public sealed partial class GunneryConsoleComponent : Component
{
    // ── Server-only runtime state ──────────────────────────────────────────
    // These are never serialized; they are set each frame by GunneryConsoleSystem.

    /// <summary>Server: EntityUid of the guided projectile currently being steered by this console, if any.</summary>
    public EntityUid? TrackedGuidedProjectile;

    /// <summary>Server: When this console last updated its UI.</summary>
    [AutoPausedField]
    public TimeSpan LastInterfaceUpdateTime;

    /// <summary>Server: game time at which the last fire command was sent (used to associate spawned guided projectiles).</summary>
    public TimeSpan LastFireTime;

    /// <summary>Server: map-space position of the last fire target (used to immediately activate guided projectile steering).</summary>
    public Vector2 LastFireTargetPos;
}

/// <summary>
/// UI key for <see cref="GunneryConsoleComponent"/>.
/// </summary>
[Serializable, NetSerializable]
public enum GunneryConsoleUiKey : byte
{
    Key,
}
