using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TextToSpeech;

[Serializable, NetSerializable]
public sealed class PreviewTTSRequestEvent : EntityEventArgs
{
    public string VoiceId { get; set; } = null!;
}
