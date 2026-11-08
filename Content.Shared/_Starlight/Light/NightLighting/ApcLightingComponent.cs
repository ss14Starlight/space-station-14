using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.Light.NightLighting;

/// <summary>
/// Lets an APC switch the lights it powers between day and night (evening) lighting.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class ApcLightingComponent : Component
{
    [DataField, AutoNetworkedField]
    public ApcLightingMode Mode = ApcLightingMode.Auto;

    /// <summary>
    /// Whether the lights on this APC currently use the night profile.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Night;

    /// <summary>
    /// Why <see cref="Night"/> is set, shown in the APC menu.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public NightLightingReason Reason = NightLightingReason.None;

    /// <summary>
    /// Light energy is multiplied by this at night.
    /// </summary>
    [DataField]
    public float NightEnergyMultiplier = 0.5f;

    /// <summary>
    /// Light radius is multiplied by this at night.
    /// </summary>
    [DataField]
    public float NightRadiusMultiplier = 0.85f;

    /// <summary>
    /// Power drawn by the lights is multiplied by this at night.
    /// </summary>
    [DataField]
    public float NightPowerMultiplier = 0.5f;

    /// <summary>
    /// Bulb colours are multiplied by this at night, warming them up.
    /// </summary>
    [DataField]
    public Color NightTint = Color.FromHex("#FFDDCC");

    /// <summary>
    /// Minimum time between two manual mode changes.
    /// </summary>
    [DataField]
    public TimeSpan ToggleCooldown = TimeSpan.FromSeconds(10);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextToggle;

    /// <summary>
    /// How long the battery has to drain before the APC switches to night lighting to save power.
    /// </summary>
    [DataField]
    public TimeSpan LowPowerDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Battery charge the APC has to get back to, while charging, to leave low power mode.
    /// </summary>
    [DataField]
    public float LowPowerRecoverCharge = 0.9f;

    /// <summary>
    /// Whether the APC is in low power mode, server side only.
    /// </summary>
    [ViewVariables]
    public bool LowPower;

    /// <summary>
    /// When the battery started draining, server side only.
    /// </summary>
    [ViewVariables]
    public TimeSpan? DrainingSince;
}

[Serializable, NetSerializable]
public enum ApcLightingMode : byte
{
    /// <summary>
    /// Night at night shift or on low power, day otherwise.
    /// </summary>
    Auto,
    Day,
    Night,
}

[Serializable, NetSerializable]
public enum NightLightingReason : byte
{
    None,

    /// <summary>
    /// Someone set the APC to night.
    /// </summary>
    Manual,

    /// <summary>
    /// Station-wide night shift: the schedule, an event or an admin.
    /// </summary>
    Station,

    /// <summary>
    /// The APC is running down its battery.
    /// </summary>
    LowPower,
}

[Serializable, NetSerializable]
public sealed class ApcSetLightingModeMessage(ApcLightingMode mode) : BoundUserInterfaceMessage
{
    public readonly ApcLightingMode Mode = mode;
}
