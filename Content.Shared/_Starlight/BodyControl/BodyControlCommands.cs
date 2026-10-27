namespace Content.Shared._Starlight.BodyControl;

/// <summary>
/// Raised before a cooperative grant is invalidated so consumers can close interfaces that still need its body reference.
/// </summary>
[ByRefEvent]
public readonly record struct BodyControlStoppingEvent(EntityUid Controller, EntityUid Body);
