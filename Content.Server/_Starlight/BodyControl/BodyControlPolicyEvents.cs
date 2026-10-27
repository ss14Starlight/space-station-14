namespace Content.Server._Starlight.BodyControl;

/// <summary>Allows a provider to keep a message on its original source instead of speaking through the controlled body.</summary>
public sealed class BodyControlSpeechRelayEvent(EntityUid source, string message) : EntityEventArgs
{
    public readonly EntityUid Source = source;
    public readonly string Message = message;
    public bool Cancelled;
}
