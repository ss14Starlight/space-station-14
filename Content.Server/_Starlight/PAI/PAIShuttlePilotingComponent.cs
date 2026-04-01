using System.Numerics;

namespace Content.Server.PAI;

/// <summary>
/// Added to a PAI while it is piloting a shuttle console.
/// Tracks the shuttle grid for ram detection and is removed when piloting ends.
/// </summary>
[RegisterComponent]
public sealed partial class PAIShuttlePilotingComponent : Component
{
    /// <summary>
    /// The shuttle grid entity this PAI is piloting.
    /// Used to detect collisions via velocity-delta polling.
    /// </summary>
    public EntityUid ShuttleGrid;

    /// <summary>
    /// The shuttle's linear velocity as of the last Update() tick.
    /// A sudden large delta-V indicates a collision.
    /// </summary>
    public Vector2 LastShuttleVelocity;
}
