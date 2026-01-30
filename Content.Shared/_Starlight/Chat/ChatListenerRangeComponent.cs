namespace Content.Shared._Starlight.Chat;

/// <summary>
/// Component used to modify the range at which the listener can hear chat messages, whispers, and affects the muffled range too.
/// </summary>
[RegisterComponent]
public sealed partial class ChatListenerRangeComponent : Component
{
    /// <summary>
    /// How far voice travels before being unable to hear, in world units.
    /// </summary>
    [DataField] public int VoiceRange = 10;
    /// <summary>
    /// How far you can be from the source of a whisper while still understanding them clearly, in world units.
    /// </summary>
    [DataField] public int WhisperClearRange = 2;
    /// <summary>
    /// How far a whisper travels in general, in world units.
    /// </summary>
    [DataField] public int WhisperMuffledRange = 5;
    /// <summary>
    /// Allows listeners to hear a message sent that would otherwise be outside the base voice range, using the range defined on <see cref="VoiceRange"/>.
    /// </summary>
    [DataField] public bool AllowExtendListenRange;
}