using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Starlight.TextToSpeech;

[Prototype]
public sealed partial class VoiceTagPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<ProtoId<VoiceTagPrototype>> Parents { get; private set; } = new();
}
