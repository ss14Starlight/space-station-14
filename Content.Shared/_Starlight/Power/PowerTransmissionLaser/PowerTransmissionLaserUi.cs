using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Power.PowerTransmissionLaser;

[Serializable, NetSerializable]
public enum PowerTransmissionLaserUiKey
{
    Key
}

[Serializable, NetSerializable]
public enum PowerTransmissionLaserVisuals : byte
{
    Active
}

[Serializable, NetSerializable]
public sealed class PowerTransmissionLaserSetPowerMessage : BoundUserInterfaceMessage
{
    public float TargetPowerMw { get; }

    public PowerTransmissionLaserSetPowerMessage(float targetPowerMw) => TargetPowerMw = targetPowerMw;
}

[Serializable, NetSerializable]
public sealed class PowerTransmissionLaserSetEnabledMessage : BoundUserInterfaceMessage
{
    public bool Enabled { get; }

    public PowerTransmissionLaserSetEnabledMessage(bool enabled) => Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class PowerTransmissionLaserBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool Enabled { get; }

    public float BatteryCurrentJoules { get; }
    public float BatteryMaxJoules { get; }

    public float ReservedBatteryCurrentJoules { get; }
    public float ReservedBatteryMaxJoules { get; }
    public float GridSaturation { get; }

    public float TargetPowerMw { get; }
    public float MinPowerMw { get; }
    public float MaxPowerMw { get; }

    public int TotalSpesosEarned { get; }

    public PowerTransmissionLaserBoundUserInterfaceState(
        bool enabled,
        float batteryCurrentJoules,
        float batteryMaxJoules,
        float reservedBatteryCurrentJoules,
        float reservedBatteryMaxJoules,
        float gridSaturation,
        float targetPowerMw,
        float minPowerMw,
        float maxPowerMw,
        int totalSpesosEarned)
    {
        Enabled = enabled;
        BatteryCurrentJoules = batteryCurrentJoules;
        BatteryMaxJoules = batteryMaxJoules;
        ReservedBatteryCurrentJoules = reservedBatteryCurrentJoules;
        ReservedBatteryMaxJoules = reservedBatteryMaxJoules;
        GridSaturation = gridSaturation;
        TargetPowerMw = targetPowerMw;
        MinPowerMw = minPowerMw;
        MaxPowerMw = maxPowerMw;
        TotalSpesosEarned = totalSpesosEarned;
    }
}
