using Robust.Shared.Audio;

namespace Content.Client._Starlight.TTS;

[RegisterComponent]
[Access(typeof(TextToSpeechSystem))]
public sealed partial class TTSAudioStreamComponent : Component
{
    public Queue<byte[]> Data { get; set; } = new();
    public EntityUid? EntityUid { get; set; }
    public EntityUid? SourceUid { get; set; }
    public AudioParams? AudioParams { get; set; }
    public bool Handled { get; set; }
    public TimeSpan AudioLength { get; internal set; }
}
