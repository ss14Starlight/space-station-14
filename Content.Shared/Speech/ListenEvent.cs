namespace Content.Shared.Speech;

public sealed class ListenEvent : EntityEventArgs
{
    public readonly string Message;
    public readonly EntityUid Source;

    public ListenEvent(string message, EntityUid source)
    {
        Message = message;
        Source = source;
    }
}

public sealed class ListenAttemptEvent : CancellableEntityEventArgs
{
    public readonly EntityUid Source;

    public ListenAttemptEvent(EntityUid source)
    {
        Source = source;
    }
}

// Starlight Start: Holopads support LOOC
public sealed class LoocListenEvent : EntityEventArgs
{
    public readonly string Message;
    public readonly EntityUid Source;

    public LoocListenEvent(string message, EntityUid source)
    {
        Message = message;
        Source = source;
    }
}
// Starlight End