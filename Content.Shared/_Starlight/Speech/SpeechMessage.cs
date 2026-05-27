using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Speech;

public sealed class SpeechMessage
{
    public string Text = "";
    public string? Tts { get; set; }
    public SpeechModifier Modifier { get; set; } = SpeechModifier.None;

    public static implicit operator SpeechMessage(string text) => new() { Text = text, Tts = text };
    public override string ToString() => Text;
}

[Serializable, NetSerializable]
public enum SpeechModifier : byte
{
    None,
    Spell,
}
