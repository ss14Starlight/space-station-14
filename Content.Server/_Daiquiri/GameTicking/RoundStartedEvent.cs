namespace Content.Server._Daiquiri.GameTicking;

public sealed class RoundStartedEvent : EntityEventArgs
{
    public int RoundId { get; }

    public RoundStartedEvent(int roundId)
    {
        RoundId = roundId;
    }
}
