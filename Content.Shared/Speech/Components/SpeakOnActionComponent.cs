using Content.Shared._Starlight.Speech;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.Speech.Components;

/// <summary>
/// Action components which should write a message to ICChat on use
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedSpeakOnActionSystem))]
public sealed partial class SpeakOnActionComponent : Component
{
    /// <summary>
    /// The ftl id of the sentence that the user will speak.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId? Sentence;

    // starlight start
    [DataField, AutoNetworkedField]
    public LocId? Tts;

    [DataField, AutoNetworkedField]
    public SpeechModifier Modifier = SpeechModifier.None;

    /// <summary>
    /// Should this be sent as a whisper?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Whisper = false;
    // starlight end
}
