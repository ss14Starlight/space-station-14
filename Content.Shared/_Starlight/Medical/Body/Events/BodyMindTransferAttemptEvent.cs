namespace Content.Shared._Starlight.Medical.Body.Events;

/// <summary>
/// Raised on both entities before an organ entering or leaving a body transfers a mind between them.
/// Does not apply to transfers unrelated to anatomy, such as mind swap spells.
/// </summary>
[ByRefEvent]
public record struct BodyMindTransferAttemptEvent(EntityUid Source, EntityUid Target, bool Cancelled = false);
