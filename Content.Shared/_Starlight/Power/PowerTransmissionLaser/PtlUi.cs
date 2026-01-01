using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Power.PowerTransmissionLaser;

[Serializable, NetSerializable]
public enum PtlUiKey
{
    Key
}

[Serializable, NetSerializable]
public enum PtlVisuals : byte
{
    Active
}

[Serializable, NetSerializable]
public sealed class PtlSetPowerMessage : BoundUserInterfaceMessage
{
    public float TargetPowerMw { get; }

    public PtlSetPowerMessage(float targetPowerMw) => TargetPowerMw = targetPowerMw;
}

[Serializable, NetSerializable]
public sealed class PtlSetEnabledMessage : BoundUserInterfaceMessage
{
    public bool Enabled { get; }

    public PtlSetEnabledMessage(bool enabled) => Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class PtlBoundUserInterfaceState : BoundUserInterfaceState
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

    public PtlBoundUserInterfaceState(
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
