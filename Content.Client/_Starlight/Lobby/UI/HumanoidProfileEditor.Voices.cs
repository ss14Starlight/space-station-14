using System.Linq;
using Content.Client._Starlight.TextToSpeech;
using Content.Shared._Starlight.TextToSpeech;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private List<VoicePrototype> _voices = [];
    private VoiceSelectorWindow _voiceSelectorWindow = default!;

    private List<VoicePrototype> _siliconVoices = [];
    private VoiceSelectorWindow _voiceSiliconSelectorWindow = default!;

    private void InitializeVoiceSelectors()
    {
        _voices = [.. _prototypeManager
            .EnumeratePrototypes<VoicePrototype>()
            .Where(voice => !voice.Silicon)];

        _voiceSelectorWindow = new VoiceSelectorWindow(_voices);
        _voiceSelectorWindow.OnVoiceSelected += voice =>
        {
            Profile = Profile?.WithVoice(voice.ID);
            IsDirty = true;
        };
        _voiceSelectorWindow.OnPreviewRequested += () =>
            _entManager.System<TextToSpeechSystem>().RequestPreviewTts(Profile?.Voice ?? "");
        VoiceButton.OnPressed += _ => _voiceSelectorWindow.OpenCentered();

        _siliconVoices = [.. _prototypeManager
            .EnumeratePrototypes<VoicePrototype>()
            .Where(voice => voice.Silicon)];

        _voiceSiliconSelectorWindow = new VoiceSelectorWindow(_siliconVoices);
        _voiceSiliconSelectorWindow.OnVoiceSelected += voice =>
        {
            Profile = Profile?.WithSiliconVoice(voice.ID);
            IsDirty = true;
        };
        _voiceSiliconSelectorWindow.OnPreviewRequested += () =>
            _entManager.System<TextToSpeechSystem>().RequestPreviewTts(Profile?.SiliconVoice ?? "");
        SiliconVoiceButton.OnPressed += _ => _voiceSiliconSelectorWindow.OpenCentered();
    }

    private void UpdateVoicesControls()
    {
        if (Profile is null)
            return;

        _voiceSelectorWindow.UpdateVoices(_voices, updateVoice: false);

        if (string.IsNullOrEmpty(Profile.Voice) && _voices.Count > 0)
            Profile.Voice = _voices[Random.Shared.Next(_voices.Count)].ID;

        var voiceChoice = _voices.FirstOrDefault(voice => voice.ID == Profile.Voice);
        if (voiceChoice != default)
            _voiceSelectorWindow.SelectVoice(voiceChoice);
    }

    private void UpdateSiliconVoicesControls()
    {
        if (Profile is null)
            return;

        _voiceSiliconSelectorWindow.UpdateVoices(_siliconVoices, updateVoice: false);

        if (string.IsNullOrEmpty(Profile.SiliconVoice) && _siliconVoices.Count > 0)
            Profile.SiliconVoice = _siliconVoices[Random.Shared.Next(_siliconVoices.Count)].ID;

        var voiceChoice = _siliconVoices.FirstOrDefault(voice => voice.ID == Profile.SiliconVoice);
        if (voiceChoice != default)
            _voiceSiliconSelectorWindow.SelectVoice(voiceChoice);
    }
}
