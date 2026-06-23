using Content.Shared._Starlight.Language;

namespace Content.Shared.Speech;

public sealed class ListenEvent : EntityEventArgs
{
    public readonly string Message;
    public readonly EntityUid Source;
    public readonly LanguagePrototype? Language; //Starlight
    public readonly string OriginalMessage; // starlight

    public ListenEvent(string message, EntityUid source, LanguagePrototype? language, string original) //Starlight
    {
        Message = message;
        Source = source;
        Language = language; //Starlight
        OriginalMessage = original; // starlight
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
