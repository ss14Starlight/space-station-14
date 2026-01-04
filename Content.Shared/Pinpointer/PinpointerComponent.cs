using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Pinpointer;

/// <summary>
/// Displays a sprite on the item that points towards the target component.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
[Access(typeof(SharedPinpointerSystem))]
public sealed partial class PinpointerComponent : Component
{
    // TODO: Type serializer oh god
    [DataField("component"), ViewVariables(VVAccess.ReadWrite)]
    public string? Component;

    // Starlight Start
    /// <summary>
    ///     List of targets that this pinpointer can track. If empty or null, uses Component field for backward compatibility.
    /// </summary>
    [DataField("targets"), ViewVariables(VVAccess.ReadWrite)]
    public List<PinpointerTarget>? Targets;

    /// <summary>
    ///     Index of the currently selected target in the Targets list. -1 means using Component field.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public int CurrentTargetIndex = -1;
    // Starlight End

    [DataField("mediumDistance"), ViewVariables(VVAccess.ReadWrite)]
    public float MediumDistance = 16f;

    [DataField("closeDistance"), ViewVariables(VVAccess.ReadWrite)]
    public float CloseDistance = 8f;

    [DataField("reachedDistance"), ViewVariables(VVAccess.ReadWrite)]
    public float ReachedDistance = 1f;

    /// <summary>
    ///     Pinpointer arrow precision in radians.
    /// </summary>
    [DataField("precision"), ViewVariables(VVAccess.ReadWrite)]
    public double Precision = 0.09;

    /// <summary>
    ///     Name to display of the target being tracked.
    /// </summary>
    [DataField("targetName"), ViewVariables(VVAccess.ReadWrite)]
    public string? TargetName;

    /// <summary>
    ///     Whether or not the target name should be updated when the target is updated.
    /// </summary>
    [DataField("updateTargetName"), ViewVariables(VVAccess.ReadWrite)]
    public bool UpdateTargetName;

    /// <summary>
    ///     Whether or not the target can be reassigned.
    /// </summary>
    [DataField("canRetarget"), ViewVariables(VVAccess.ReadWrite)]
    public bool CanRetarget;

    [ViewVariables]
    public EntityUid? Target = null;

    [ViewVariables, AutoNetworkedField]
    public bool IsActive = false;

    [ViewVariables, AutoNetworkedField]
    public Angle ArrowAngle;

    [ViewVariables, AutoNetworkedField]
    public Distance DistanceToTarget = Distance.Unknown;

    [ViewVariables]
    public bool HasTarget => DistanceToTarget != Distance.Unknown;
}

// Starlight Start
/// <summary>
/// Represents a target configuration for a multi-target pinpointer.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class PinpointerTarget
{
    /// <summary>
    ///     Component type to track (e.g., "NukeDisk").
    /// </summary>
    [DataField("component")]
    public string? Component;

    /// <summary>
    ///     Tag to track (e.g., "PlutoniumCore"). Takes priority over Component if both are set.
    /// </summary>
    [DataField("tag")]
    public string? Tag;

    /// <summary>
    ///     Display name for this target.
    /// </summary>
    [DataField("name", required: true)]
    public string Name = string.Empty;
}
// Starlight End

[Serializable, NetSerializable]
public enum Distance : byte
{
    Unknown,
    Reached,
    Close,
    Medium,
    Far
}
