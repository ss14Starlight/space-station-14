namespace Content.Shared._Starlight.Actions.Events;

/// <summary>
/// Raised on the would-be target before a latch starts. Set <see cref="Cancelled"/>
/// to block it; the latch action isn't consumed. The canceller is responsible
/// for any popup explaining why.
/// </summary>
[ByRefEvent]
public record struct LatchAttemptEvent(EntityUid Latcher, EntityUid Target, bool Cancelled = false);
