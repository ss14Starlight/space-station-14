using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Power;

[Serializable, NetSerializable]
public enum PowerCellFallbackVisuals : byte
{
    VisualState,
}

[Serializable, NetSerializable]
public enum PowerCellFallbackVisualState : byte
{
    Off,
    OnBatteryIdle,
    OnBatteryInUse,
}
