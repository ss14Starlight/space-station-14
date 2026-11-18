using Content.Shared.GameTicking;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Time;

public abstract partial class SharedTimeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedGameTicker _gameTicker = default!;

    /// <summary>
    /// Default value is sensible but will be updated later.
    /// </summary>
    protected DateTime Date = DateTime.UtcNow.AddYears(500);

    /// <summary>
    /// Station time at which the current shift started.
    /// </summary>
    protected TimeSpan ShiftStart = TimeSpan.FromHours(12);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RoundDateSetEvent>(OnRoundDateSetEvent);
    }

    /// <summary>
    /// Updates the Date every time a round starts.
    /// </summary>
    private void OnRoundDateSetEvent(RoundDateSetEvent ev)
    {
        Date = ev.Date;
        ShiftStart = ev.ShiftStart;
    }

    /// <summary>
    /// Gets a flavorful version of the station time. Shifts start at <see cref="ShiftStart"/> (picked at random
    /// each round) and station time runs 4x faster than real time.
    /// </summary>
    public (TimeSpan Time, string Date) GetStationTime()
    {
        var scaledTimeSinceStart = _timing.CurTime.Subtract(_gameTicker.RoundStartTimeSpan).Multiply(4);
        var stationTime = scaledTimeSinceStart.Add(ShiftStart);

        // very long shifts could roll over into the following day.
        var totalDays = (int)stationTime.TotalDays;
        stationTime = stationTime.Subtract(TimeSpan.FromDays(totalDays));

        var newDate = Date.AddDays(totalDays);

        // ISO 8601 (YYYY-MM-DD or YYYYMMDD)
        return (stationTime, newDate.ToString("yyyy-MM-dd"));
    }

    /// <summary>
    /// Gets the station's date, 500 years in the future from today's date. Rolls over at station midnight
    /// like <see cref="GetStationTime"/>, so a date and a time shown together always match.
    /// </summary>
    public string GetDate()
        => GetStationTime().Date;

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
public sealed class RoundDateSetEvent(DateTime date, TimeSpan shiftStart) : EntityEventArgs
{
    public DateTime Date { get; } = date;

    /// <summary>
    /// Station time at which the shift started.
    /// </summary>
    public TimeSpan ShiftStart { get; } = shiftStart;
}
