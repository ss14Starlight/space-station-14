using Content.Shared.Medical.SuitSensor;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Cargo.MailCompanion;

[Serializable, NetSerializable]
public enum MailCompanionUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum MailCompanionStatus : byte
{
    Idle,
    Tracking,
    Cooldown,
    SensorsOff,
    TrackingUnavailable,
    RecipientUnavailable,
    DeliveryOpened,
    DeliveryAlreadyOpened,
    Expired
}

[Serializable, NetSerializable]
public enum MailCompanionVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum MailCompanionVisualLayers : byte
{
    Base
}

[Serializable, NetSerializable]
public enum MailCompanionVisualState : byte
{
    Idle,
    Active
}

[Serializable, NetSerializable]
public sealed partial class MailCompanionState : BoundUserInterfaceState
{
    public TimeSpan Timestamp;
    public string? RecipientName;
    public MailCompanionStatus Status;
    public TimeSpan TimeRemaining;
    public SuitSensorStatus? TrackedSensor;

    public MailCompanionState(TimeSpan timestamp, string? recipientName, MailCompanionStatus status, TimeSpan timeRemaining, SuitSensorStatus? trackedSensor)
    {
        Timestamp = timestamp;
        RecipientName = recipientName;
        Status = status;
        TimeRemaining = timeRemaining;
        TrackedSensor = trackedSensor;
    }
}
