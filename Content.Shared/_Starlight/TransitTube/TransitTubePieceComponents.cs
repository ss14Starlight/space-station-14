using Content.Shared.DeviceLinking;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TransitTube;

/// <summary>
/// Changes the speed of pods passing through in <see cref="Direction"/>. Speed-ups push pods past
/// stations; brakes bring them back to their normal speed. Pods going the other way are not affected.
/// </summary>
[RegisterComponent]
public sealed partial class TransitTubeSpeedComponent : Component
{
    /// <summary>
    /// The direction, before rotation, that pods must travel in to be affected.
    /// </summary>
    [DataField]
    public Direction Direction = Direction.North;

    /// <summary>
    /// The new speed in tiles per second. Null puts the pod back to its normal speed.
    /// </summary>
    [DataField]
    public float? Speed;
}

/// <summary>
/// A junction whose arrow is switched over the device network instead of being fixed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TransitTubeSwitchComponent : Component
{
    /// <summary>
    /// The exit, before rotation, that pods take while the switch is set to the left.
    /// </summary>
    [DataField]
    public Direction LeftExit = Direction.NorthWest;

    /// <summary>
    /// The exit, before rotation, that pods take while the switch is set to the right.
    /// </summary>
    [DataField]
    public Direction RightExit = Direction.NorthEast;

    [DataField, AutoNetworkedField]
    public bool Right = true;

    [DataField]
    public ProtoId<SinkPortPrototype> LeftPort = "TransitSwitchLeft";

    [DataField]
    public ProtoId<SinkPortPrototype> RightPort = "TransitSwitchRight";

    [DataField]
    public ProtoId<SinkPortPrototype> TogglePort = "Toggle";
}

/// <summary>
/// Turns pods around in place, instead of a loop of tubes.
/// </summary>
[RegisterComponent]
public sealed partial class TransitTubeTurntableComponent : Component
{
    /// <summary>
    /// How far the pod is turned, in degrees clockwise. 180 sends it back the way it came.
    /// </summary>
    [DataField]
    public float Turn = 180f;
}

/// <summary>
/// A block signal: pods travelling in <see cref="Direction"/> wait here while there is another pod
/// in the section ahead, up to the next signal or station.
/// </summary>
[RegisterComponent]
public sealed partial class TransitTubeBlockSignalComponent : Component
{
    /// <summary>
    /// The direction, before rotation, that the signal guards.
    /// </summary>
    [DataField]
    public Direction Direction = Direction.North;

    /// <summary>
    /// The longest section, in tubes, that is checked ahead of the signal.
    /// </summary>
    [DataField]
    public int MaxSectionLength = 64;
}

/// <summary>
/// Lets a tube piece be turned on and off over the device network. While off, a one-way tube
/// lets pods through both ways.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TransitTubeToggleComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    public ProtoId<SinkPortPrototype> OnPort = "On";

    [DataField]
    public ProtoId<SinkPortPrototype> OffPort = "Off";

    [DataField]
    public ProtoId<SinkPortPrototype> TogglePort = "Toggle";
}

[Serializable, NetSerializable]
public enum TransitSwitchVisuals : byte
{
    Right,
}

[Serializable, NetSerializable]
public enum TransitTubeToggleVisuals : byte
{
    Enabled,
}
