namespace Content.Shared._Starlight.StationRadio.Events;

/// <summary>
/// Raised on a vinyl player when it starts, pauses or stops playing, or its current disc changes.
/// </summary>
[ByRefEvent]
public record struct VinylPlayerStateChangedEvent;

/// <summary>
/// Raised on a station radio server after its tracks or mixer volumes were pushed to the receivers.
/// </summary>
[ByRefEvent]
public record struct StationRadioTracksChangedEvent;
