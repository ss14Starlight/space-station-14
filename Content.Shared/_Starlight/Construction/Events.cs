namespace Content.Shared._Starlight.Construction;

[ByRefEvent]
public record struct ConstructionInteractAttemptEvent(EntityUid User, EntityUid Target, bool Cancelled = false);
