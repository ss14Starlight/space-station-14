using Content.Server.GameTicking.Events;
using Content.Shared._Starlight.Time;

namespace Content.Server._Starlight.Time;

public sealed partial class TimeSystem : SharedTimeSystem
{
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
        RaiseNetworkEvent(new RoundDateSetEvent(Date), args.SenderSession);

    /// <summary>
    /// When a round starts, we update the round-start date and broadcast it.
    /// </summary>
    private void OnRoundStart(RoundStartingEvent ev)
    {
        Date = DateTime.UtcNow.AddYears(500);
        RaiseNetworkEvent(new RoundDateSetEvent(Date));
    }

}
