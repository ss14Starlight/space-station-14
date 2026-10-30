using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Starlight.TextToSpeech;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TextToSpeechComponent : Component
{
    [DataField("voice"), AutoNetworkedField]
    public ProtoId<VoicePrototype>? VoicePrototypeId { get; set; }
}
