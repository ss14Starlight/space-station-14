using Robust.Shared.Network;

namespace Content.Shared.Starlight.CryoTeleportation;

[RegisterComponent]
public sealed partial class TargetCryoTeleportationComponent : Component
{
    /// <summary>
    /// Station uid where entity will be cryo teleported.
    /// </summary>
    [DataField]
    public EntityUid? Station;

    /// <summary>
    /// Time when player detached from entity.
    /// </summary>
    [DataField]
    public TimeSpan? ExitTime;
    
    [DataField]
    public NetUserId? UserId;

    /// <summary>
    /// Determines how much extra time we need to wait for cryo teleportation.
    /// </summary>
    [DataField]
    public TimeSpan TimeDelay = TimeSpan.FromSeconds(0);
}