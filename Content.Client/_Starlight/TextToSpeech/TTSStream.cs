using Content.Shared.Starlight.TextToSpeech;
using Robust.Shared.Audio;

namespace Content.Client._Starlight.TextToSpeech;

public sealed class TTSStream : EntityEventArgs
{
    public Guid Id { get; init; }
    public TTSType Type { get; init; }
    public NetEntity? SourceUid { get; init; }
    public SoundSpecifier? Chime { get; init; }
    public float VolumeModifier { get; init; } = 1f;
    public bool IsStarted { get; set; }
    public Queue<byte[]> Data { get; } = new();
}
