using Content.Shared.GameTicking;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Time;

public abstract class SharedTimeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedGameTicker _gameTicker = default!;

    // Default value is sensible but will be updated later.
    protected DateTime Date = DateTime.UtcNow.AddYears(500);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RoundDateSetEvent>(OnRoundDateSetEvent);
    }

    // Updates the Date every time a round starts.
    private void OnRoundDateSetEvent(RoundDateSetEvent ev) => Date = ev.Date;

    /// <summary>
    /// Gets a flavorful version of the station time. Shifts start at 12PM and station time runs 4x faster than real time.
    /// </summary>
    public (TimeSpan Time, string Date) GetStationTime()
    {
        var scaledTimeSinceStart = _timing.CurTime.Subtract(_gameTicker.RoundStartTimeSpan).Multiply(4);
        var stationTime = scaledTimeSinceStart.Add(TimeSpan.FromHours(12));

        // very long shifts could roll over into the following day.
        var totalDays = (int)stationTime.TotalDays;
        stationTime = stationTime.Subtract(TimeSpan.FromDays(totalDays));

        var newDate = Date.AddDays(totalDays);

        // ISO 8601 (YYYY-MM-DD or YYYYMMDD)
        return (stationTime, newDate.ToString("yyyy-MM-dd"));
    }

    /// <summary>
    /// Gets the station's date. This is 500 years in the future from today's date.
    /// </summary>
    public string GetDate()
    {
        // ISO 8601 (YYYY-MM-DD or YYYYMMDD)
        return Date.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// Gets the ellapsed time of the round, useful for paperwork.
    /// This value is not affected by time scaling and reflects the real duration of a round.
    /// </summary>
    public TimeSpan GetShiftDuration() => _timing.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
}

/// <summary>
/// Dispatched to request the canonical round-start date from the server.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestRoundDateEvent : EntityEventArgs
{
}

/// <summary>
/// Dispatched by the server to inform clients of the canonical round-start date.
/// </summary>
[Serializable, NetSerializable]
public sealed class RoundDateSetEvent(DateTime date) : EntityEventArgs
{
    public DateTime Date { get; } = date;
}
