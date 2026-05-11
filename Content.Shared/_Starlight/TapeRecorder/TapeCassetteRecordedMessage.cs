using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.TapeRecorder;

/// <summary>
/// Every chat event recorded on a tape is saved in this format
/// </summary>
[ImplicitDataDefinitionForInheritors]
public sealed partial class TapeCassetteRecordedMessage : IComparable<TapeCassetteRecordedMessage>
{
    /// <summary>
    /// Number of seconds since the start of the tape that this event was recorded at
    /// </summary>
    [DataField(required: true)]
    public float Timestamp = 0;

    /// <summary>
    /// The name of the entity that spoke
    /// </summary>
    [DataField]
    public string? Name;

    /// <summary>
    /// The verb used for this message.
    /// </summary>
    [DataField]
    public ProtoId<SpeechVerbPrototype>? Verb;

    /// <summary>
    /// What was spoken
    /// </summary>
    [DataField]
    public string Message = string.Empty;

    /// <summary>
    /// The voice prototype ID of the speaker (for TTS on playback)
    /// </summary>
    [DataField]
    public string? VoiceId;

    public TapeCassetteRecordedMessage(float timestamp, string name, ProtoId<SpeechVerbPrototype> verb, string message, string? voiceId = null)
    {
        Timestamp = timestamp;
        Name = name;
        Verb = verb;
        Message = message;
        VoiceId = voiceId;
    }

    public int CompareTo(TapeCassetteRecordedMessage? other)
    {
        if (other == null)
            return 0;

        return (int) (Timestamp - other.Timestamp);
    }
}
