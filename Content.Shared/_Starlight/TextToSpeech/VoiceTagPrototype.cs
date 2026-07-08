using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Starlight.TextToSpeech;

[Prototype("voiceTag")]
public sealed partial class VoiceTagPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("parents")]
    public List<ProtoId<VoiceTagPrototype>> Parents { get; private set; } = new();
}
