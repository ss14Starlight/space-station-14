using Content.Shared.GameTicking;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Time;

public abstract class SharedTimeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    // Default value is sensible but will be updated later.
    protected DateTime Date = DateTime.UtcNow.AddYears(500);

    private TimeSpan _roundStart;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TickerLobbyStatusEvent>(LobbyStatus);
        SubscribeNetworkEvent<RoundDateSetEvent>(OnRoundDateSetEvent);
    }

    private void OnRoundDateSetEvent(RoundDateSetEvent ev) => Date = ev.Date;
    private void LobbyStatus(TickerLobbyStatusEvent ev) => _roundStart = ev.RoundStartTimeSpan;

    public (TimeSpan Time, string Date) GetStationTime()
    {
        var scaledTimeSinceStart = _timing.CurTime.Subtract(_roundStart).Multiply(4);
        var stationTime = scaledTimeSinceStart.Add(TimeSpan.FromHours(12));

        var totalDays = (int)stationTime.TotalDays;
        stationTime = stationTime.Subtract(TimeSpan.FromDays(totalDays));

        var newDate = Date.AddDays(totalDays);

        return (stationTime, newDate.ToString("dd.MM.yyyy"));
    }

    public string GetDate()
    {
        // please tell me you guys aren't gonna have a 4 week round yet...
        return Date.ToString("dd.MM.yyyy");
    }
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
