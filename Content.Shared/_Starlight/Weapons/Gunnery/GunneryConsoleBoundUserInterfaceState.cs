using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Weapons.Gunnery;

/// <summary>
/// Full BUI state sent from server to client for the gunnery console.
/// Wraps the standard <see cref="NavInterfaceState"/> radar data and adds
/// a list of cannon positions and guided-projectile tracking.
/// </summary>
[Serializable, NetSerializable]
public sealed class GunneryConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    /// <summary>Standard radar state (grids, docks, blips, laser traces).</summary>
    public readonly NavInterfaceState NavState;

    /// <summary>
    /// Positions and identities of all shuttle-mounted cannons on this grid
    /// that are visible to this console.
    /// </summary>
    public readonly List<CannonBlipData> Cannons;

    /// <summary>
    /// Network entity of the guided projectile currently being tracked by this
    /// console, or <c>null</c> if no guidance is active.
    /// </summary>
    public readonly NetEntity? TrackedGuidedProjectile;
    
    public readonly bool HasServer = true;

    public GunneryConsoleBoundUserInterfaceState(
        NavInterfaceState navState,
        List<CannonBlipData> cannons,
        NetEntity? trackedGuidedProjectile,
        bool hasServer = true)
    {
        NavState       = navState;
        Cannons        = cannons;
        TrackedGuidedProjectile = trackedGuidedProjectile;
        HasServer      = hasServer;
    }
}

/// <summary>
/// Represents a shuttle-mounted cannon on the gunnery radar.
/// </summary>
[Serializable, NetSerializable]
public readonly struct CannonBlipData
{
    /// <summary>Entity-space coordinates of the cannon (same grid as the console).</summary>
    public readonly NetCoordinates Coordinates;

    /// <summary>Network entity identifier — sent back in fire messages.</summary>
    public readonly NetEntity Entity;

    /// <summary>Display name shown in the cannon list.</summary>
    public readonly string Name;

    /// <summary>Remaining cooldown in seconds; 0 when the cannon is ready to fire.</summary>
    public readonly float CooldownSeconds;

    public CannonBlipData(NetCoordinates coordinates, NetEntity entity, string name, float cooldownSeconds = 0f)
    {
        Coordinates     = coordinates;
        Entity          = entity;
        Name            = name;
        CooldownSeconds = cooldownSeconds;
    }
}
