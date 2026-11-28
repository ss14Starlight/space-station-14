using Content.Shared.Actions;
using Content.Shared.Atmos;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Vehicles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleFanModComponent : Component
{
    /// <summary>
    /// Whether the fan is currently active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsActive = false;

    /// <summary>
    /// Current fan state see <see cref="MechFanState"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FanState State = FanState.Off;

    /// <summary>
    /// How much gas the fan can process per second when active.
    /// </summary>
    [DataField]
    public float GasProcessingRate = 1f;

    /// <summary>
    /// Gases that will be filtered during fan operation.
    /// </summary>
    [DataField(required: true)]
    public HashSet<Gas> FilterGases = new();
}

/// <summary>
/// Event raised to toggle the fan state of a mech
/// </summary>
public sealed partial class VehicleFanToggle : InstantActionEvent;

[Serializable, NetSerializable]
public enum FanState : byte
{
    Off,
    On,
    Idle,
    Na
}