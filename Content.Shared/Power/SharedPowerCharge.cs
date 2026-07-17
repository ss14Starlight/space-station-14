using Robust.Shared.Serialization;

namespace Content.Shared.Power;

/// <summary>
///     Sent to the server to set whether the machine should be on or off
/// </summary>
[Serializable, NetSerializable]
public sealed class SwitchChargingMachineMessage : BoundUserInterfaceMessage
{
    public bool On;

    public SwitchChargingMachineMessage(bool on)
    {
        On = on;
    }
}

[Serializable, NetSerializable]
public sealed class PowerChargeState : BoundUserInterfaceState
{
    public bool On;
    // 0 -> 255
    public byte Charge;
    public PowerChargePowerStatus PowerStatus;
    public short PowerDraw;
    public short PowerDrawMax;
    public short EtaSeconds;

    /// <summary>
    /// Optional localization ID for a blocking error (e.g. remote terminal with no linked machine).
    /// When set, the client shows this instead of normal charge controls.
    /// </summary>
    public string? Error;

    public PowerChargeState(
        bool on,
        byte charge,
        PowerChargePowerStatus powerStatus,
        short powerDraw,
        short powerDrawMax,
        short etaSeconds,
        string? error = null)
    {
        On = on;
        Charge = charge;
        PowerStatus = powerStatus;
        PowerDraw = powerDraw;
        PowerDrawMax = powerDrawMax;
        EtaSeconds = etaSeconds;
        Error = error;
    }

    public static PowerChargeState UnlinkedError(string errorLocId)
    {
        return new PowerChargeState(
            on: false,
            charge: 0,
            PowerChargePowerStatus.Off,
            powerDraw: 0,
            powerDrawMax: 0,
            etaSeconds: short.MinValue,
            error: errorLocId);
    }
}

[Serializable, NetSerializable]
public enum PowerChargeUiKey
{
    Key
}

[Serializable, NetSerializable]
public enum PowerChargeVisuals
{
    State,
    Charge,
    Active
}

[Serializable, NetSerializable]
public enum PowerChargeStatus
{
    Broken,
    Unpowered,
    Off,
    On
}

[Serializable, NetSerializable]
public enum PowerChargePowerStatus : byte
{
    Off,
    Discharging,
    Charging,
    FullyCharged
}
