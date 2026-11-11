namespace Content.Shared._Blimpuf.Traits.Assorted;

/// <summary>
/// Notifies other episode schedulers on the patient when migraine prevention changes.
/// </summary>
[ByRefEvent]
public readonly record struct MigrainePreventionUpdatedEvent(TimeSpan ProtectionEnd, TimeSpan MinimumDelay);
