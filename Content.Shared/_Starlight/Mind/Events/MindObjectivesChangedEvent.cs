namespace Content.Shared._Starlight.Mind.Events;

/// <summary>
/// Raised on a mind after its assigned objective list changes.
/// </summary>
[ByRefEvent]
public readonly record struct MindObjectivesChangedEvent;
