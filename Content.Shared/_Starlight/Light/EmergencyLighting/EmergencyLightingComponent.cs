using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Light.EmergencyLighting;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EmergencyLightingComponent : Component
{
    /// <summary>
    /// Colour of the emergency glow, replacing the bulb's own.
    /// </summary>
    [DataField]
    public Color Color = Color.FromHex("#FF3B30");

    /// <summary>
    /// Bulb energy is multiplied by this while running from the cell.
    /// </summary>
    [DataField]
    public float EnergyMultiplier = 0.45f;

    /// <summary>
    /// Bulb radius is multiplied by this while running from the cell.
    /// </summary>
    [DataField]
    public float RadiusMultiplier = 0.6f;

    /// <summary>
    /// How long a full cell keeps the light going.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(8);

    /// <summary>
    /// How long a flat cell takes to charge back up while the fixture is powered.
    /// </summary>
    [DataField]
    public TimeSpan RechargeTime = TimeSpan.FromMinutes(4);

    /// <summary>
    /// Charge, from 0 to 1, below which the light starts flickering.
    /// </summary>
    [DataField]
    public float FlickerCharge = 0.1f;

    /// <summary>
    /// Whether the cell has anything left to light the bulb with.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Charged = true;

    /// <summary>
    /// Charge, from 0 to 1, as of <see cref="ChargeUpdated"/>. Server side only.
    /// </summary>
    [ViewVariables]
    public float Charge = 1f;

    /// <summary>
    /// When <see cref="Charge"/> was last brought up to date. Server side only.
    /// </summary>
    [ViewVariables]
    public TimeSpan ChargeUpdated;

    /// <summary>
    /// Whether the fixture is unpowered and running down the cell. Server side only.
    /// </summary>
    [ViewVariables]
    public bool Discharging;

    /// <summary>
    /// Whether the cell started the light flickering. Server side only.
    /// </summary>
    [ViewVariables]
    public bool Flickering;
}

[Serializable, NetSerializable]
public enum EmergencyLightingVisuals : byte
{
    Active,
}
