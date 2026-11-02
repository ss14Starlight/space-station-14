namespace Content.Shared._Starlight.StationRadio.Components;

[RegisterComponent]
public sealed partial class BroadcastTapeComponent : Component
{
    /// <summary>
    /// How much broadcast the tape can hold.
    /// </summary>
    [DataField]
    public TimeSpan MaxLength = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How much of the tape is recorded.
    /// </summary>
    [DataField]
    public TimeSpan Length;

    /// <summary>
    /// Recorded events, in time order.
    /// </summary>
    [DataField]
    public List<BroadcastTapeEvent> Events = new();
}

public enum BroadcastTapeEventKind : byte
{
    TrackStart, // Sound started

    TrackStop, // Sound ended

    Gain, // Volume changed

    Speech, // Someone spoke
}

[DataDefinition]
public sealed partial class BroadcastTapeEvent
{
    /// <summary>
    /// Tape position of the event.
    /// </summary>
    [DataField]
    public TimeSpan Time;

    [DataField]
    public BroadcastTapeEventKind Kind;

    /// <summary>
    /// Recorded channel a track event belongs to.
    /// </summary>
    [DataField]
    public int Channel;

    [DataField]
    public string? Sound;

    /// <summary>
    /// How far into the sound it already was when it started on the tape.
    /// </summary>
    [DataField]
    public TimeSpan Offset;

    [DataField]
    public float Gain = 1f;

    /// <summary>
    /// Name the speaker was heard under.
    /// </summary>
    [DataField]
    public string? Name;

    /// <summary>
    /// TTS voice prototype of the speaker.
    /// </summary>
    [DataField]
    public string? Voice;

    [DataField]
    public string? Text;

    [DataField]
    public string? Language;
}
