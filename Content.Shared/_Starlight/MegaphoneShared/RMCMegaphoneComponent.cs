using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Speech;

namespace Content.Shared._Starlight.Megaphone;

[RegisterComponent, NetworkedComponent]
[Access(typeof(RMCMegaphoneSystem))]
public sealed partial class RMCMegaphoneComponent : Component
{
    [DataField]
    public ProtoId<SpeechSoundsPrototype> MegaphoneSpeechSound = "PreacherMegaphone";

    [DataField]
    public ProtoId<SpeechVerbPrototype> SpeechVerb = "MegaphoneYell";

    [DataField]
    public Dictionary<string, ProtoId<SpeechVerbPrototype>> SuffixSpeechVerbs = new()
    {
        { "chat-speech-verb-suffix-exclamation-strong", "MegaphoneYell" },
        { "chat-speech-verb-suffix-exclamation", "MegaphoneYell" },
        { "chat-speech-verb-suffix-question", "MegaphoneYell" },
        { "chat-speech-verb-suffix-stutter", "MegaphoneYell" },
        { "chat-speech-verb-suffix-mumble", "MegaphoneYell" },
        { "chat-speech-verb-suffix-pause", "MegaphoneYell" },
    };
}
