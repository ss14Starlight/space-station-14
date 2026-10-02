using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Starlight.TextToSpeech;

[RegisterComponent, NetworkedComponent]
public sealed partial class TextToSpeechComponent : Component
{
    [DataField("voice")]
    public ProtoId<VoicePrototype>? VoicePrototypeId { get; set; }
}
