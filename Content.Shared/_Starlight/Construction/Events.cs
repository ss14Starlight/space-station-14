namespace Content.Shared._Starlight.Construction;

/// <summary>
/// Fired when a user attempts to interact with something, triggering a construction step.
/// </summary>
[ByRefEvent]
public record struct ConstructionInteractAttemptEvent(EntityUid User, EntityUid Target, bool Canceled = false);
