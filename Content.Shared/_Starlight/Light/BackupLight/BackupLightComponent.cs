using Content.Shared.Power.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Light.BackupLight;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BackupLightComponent : Component
{
    /// <summary>
    /// Colour of the backup glow, replacing the bulb's own.
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
    /// Watts drawn from the cell while the fixture runs from it.
    /// </summary>
    [DataField]
    public float DrainRate = 5f;

    /// <summary>
    /// Watts put back into the cell while the fixture is powered.
    /// </summary>
    [DataField]
    public float RechargeRate = 10f;

    /// <summary>
    /// Charge, as a fraction of the cell, below which the light starts flickering.
    /// </summary>
    [DataField]
    public float FlickerCharge = 0.1f;

    /// <summary>
    /// Whether the cell has anything left to light the bulb with.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Charged = true;

    /// <summary>
    /// Whether the fixture is running from the cell. Server side only.
    /// </summary>
    [ViewVariables]
    public bool Draining;

    /// <summary>
    /// Whether the fixture is powered and topping the cell up. Server side only.
    /// </summary>
    [ViewVariables]
    public bool Charging;

    /// <summary>
    /// Whether the nearly flat cell started the light flickering. Server side only.
    /// </summary>
    [ViewVariables]
    public bool Flickering;
}

[Serializable, NetSerializable]
public enum BackupLightVisuals : byte
{
    Active,
}
