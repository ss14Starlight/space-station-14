using Content.Shared._Starlight.Speech;

namespace Content.Shared.Speech;

public sealed class AccentGetEvent : EntityEventArgs
{
    /// <summary>
    ///     The entity to apply the accent to.
    /// </summary>
    public EntityUid Entity { get; }

    /// <summary>
    ///     The message to apply the accent transformation to.
    ///     Modify this to apply the accent.
    /// </summary>
    public SpeechMessage Message { get; set; } // Starlight

    public AccentGetEvent(EntityUid entity, SpeechMessage message) // Starlight
    {
        Entity = entity;
        Message = message;
    }
}
