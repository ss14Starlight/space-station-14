using System.Collections.Generic;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Content.Shared.Medical.SuitSensor;

namespace Content.Shared._Starlight.Cargo.MailCompanion;

[RegisterComponent]
public sealed partial class MailCompanionComponent : Component
{
    [DataField]
    public TimeSpan TrackingDuration = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan CooldownDuration = TimeSpan.FromSeconds(30);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? ExpiresAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? CooldownEndsAt;

    public EntityUid? TrackedDelivery;

    public EntityUid? TrackedTarget;

    public EntityUid? TrackedSensor;

    public EntityUid? LastUser;

    public string? RecipientName;

    public MailCompanionStatus Status = MailCompanionStatus.Idle;

    public Dictionary<string, SuitSensorStatus> ConnectedSensors = new();

    public TimeSpan LastSensorDataReceivedAt = TimeSpan.Zero;
}
