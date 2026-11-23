using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

namespace Content.Shared._FarHorizons.Towing.Components;

[RegisterComponent]
public sealed partial class TiedComponent : Component
{
    /// <summary>
    /// how long does it take to untie an entity
    /// </summary>
    [DataField]
    public TimeSpan UntieTime = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Attached to entity
    /// </summary>
    [DataField]
    public EntityUid? AttachedTo;

    /// <summary>
    /// The towing joint
    /// </summary>
    public Joint? Joint; // Starlight
}

[Serializable, NetSerializable]
public sealed partial class DeployHitchDoAfter : SimpleDoAfterEvent;
