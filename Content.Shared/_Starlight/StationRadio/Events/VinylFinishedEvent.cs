namespace Content.Shared._Starlight.StationRadio.Events;

/// <summary>
/// Raised on a vinyl player when a vinyl is finished.
/// </summary>
[ByRefEvent]
public record struct VinylFinishedEvent(EntityUid Player);
