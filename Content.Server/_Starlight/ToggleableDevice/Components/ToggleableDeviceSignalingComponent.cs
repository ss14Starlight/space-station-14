using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.ToggleableDevice.Components;

/// <summary>
/// Adds signaling functionality to an entity with a ToggleableDeviceComponent.
/// </summary>
[RegisterComponent]
public sealed partial class ToggleableDeviceSignalingComponent : Component
{
    /// <summary>
    /// Input port to turn the device on.
    /// </summary>
    [DataField] public ProtoId<SinkPortPrototype> OnPort = "On";

    /// <summary>
    /// Input port to turn the device off.
    /// </summary>
    [DataField] public ProtoId<SinkPortPrototype> OffPort = "Off";

    /// <summary>
    /// Input port that toggles the device state upon going High.
    /// </summary>
    [DataField] public ProtoId<SinkPortPrototype> TogglePort = "Toggle";

    /// <summary>
    /// Input state port that when connected will make the device state follow the signal state.
    /// That means that while the signal is High, the device will be on, and if it goes Low, it will turn off.
    /// </summary>
    [DataField] public ProtoId<SinkPortPrototype> StatePort = "ToggleableDeviceState";

    /// <summary>
    /// The inverse of <see cref="StatePort"/>.
    /// </summary>
    [DataField] public ProtoId<SinkPortPrototype> StateInvertedPort = "ToggleableDeviceStateInverted";

    /// <summary>
    /// Status port that is High when the device is on and Low when it's off.
    /// </summary>
    [DataField] public ProtoId<SourcePortPrototype> StatusPort = "Status";

    /// <summary>
    /// The inverse of <see cref="StatusPort"/>.
    /// </summary>
    [DataField] public ProtoId<SourcePortPrototype> StatusInvertedPort = "ToggleableDeviceStatusInverted";
}
