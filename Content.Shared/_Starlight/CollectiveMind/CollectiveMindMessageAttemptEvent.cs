namespace Content.Shared.CollectiveMind;

public sealed class CollectiveMindMessageAttemptEvent : CancellableEntityEventArgs
{
    /// <summary>
    ///     The entity sending the message.
    /// </summary>
    public EntityUid Entity { get; }

    /// <summary>
    /// The collective mind channel being spoken into.
    /// </summary>
    public CollectiveMindPrototype CollectiveMind { get; }

    /// <summary>
    ///     The message being sent.
    ///     Modify this to apply effects to the text.
    /// </summary>
    public string Message { get; set; }

    public CollectiveMindMessageAttemptEvent(EntityUid entity, string message, CollectiveMindPrototype collectiveMind)
    {
        Entity = entity;
        Message = message;
        CollectiveMind = collectiveMind;
    }
}
