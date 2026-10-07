using Content.Server.GameTicking.Events;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Time;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Time;

public sealed partial class TimeSystem : SharedTimeSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestRoundDateEvent>(OnRequestRoundDate);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
    }

    /// <summary>
    /// When a client requests the authoritative round start date from the server, we answer.
    /// </summary>
    private void OnRequestRoundDate(RequestRoundDateEvent msg, EntitySessionEventArgs args) =>
        RaiseNetworkEvent(new RoundDateSetEvent(Date, ShiftStart), args.SenderSession);

    /// <summary>
    /// When a round starts, we update the round-start date and shift start time and broadcast them.
    /// </summary>
    private void OnRoundStart(RoundStartingEvent ev)
    {
        Date = DateTime.UtcNow.AddYears(500);

        var minHour = Math.Clamp(_cfg.GetCVar(StarlightCCVars.ShiftStartMinHour), 0, 23);
        var maxHour = Math.Clamp(_cfg.GetCVar(StarlightCCVars.ShiftStartMaxHour), minHour, 23);
        var start = TimeSpan.FromHours(_random.Next(minHour, maxHour + 1));
        // Quarter-hour steps, unless the start is pinned to a single hour.
        if (minHour != maxHour)
            start += TimeSpan.FromMinutes(_random.Next(0, 4) * 15);
        ShiftStart = start;

        RaiseNetworkEvent(new RoundDateSetEvent(Date, ShiftStart));
    }
}
