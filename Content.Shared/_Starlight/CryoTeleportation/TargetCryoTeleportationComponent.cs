using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.CryoTeleportation;

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
    /// Job this body spawned as. Reopened on cryo if the player has moved on.
    /// </summary>
    [DataField]
    public ProtoId<JobPrototype>? Job;

    /// <summary>
    /// Determines how much extra time we need to wait for cryo teleportation.
    /// </summary>
    [DataField]
    public TimeSpan TimeDelay = TimeSpan.FromSeconds(0);
}
