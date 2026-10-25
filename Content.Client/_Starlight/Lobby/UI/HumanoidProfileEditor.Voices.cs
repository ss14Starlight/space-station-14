using System.Linq;
using Content.Client._Starlight.TextToSpeech;
using Content.Shared._Starlight.TextToSpeech;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private const int CharacterVoice = 0;
    private const int SiliconVoice = 1;

    private List<VoicePrototype> _voices = [];
    private VoicePicker _voicePicker = default!;

    private List<VoicePrototype> _siliconVoices = [];
    private VoicePicker _siliconVoicePicker = default!;

    private void InitializeVoiceSelectors()
    {
        _voices = [.. _prototypeManager
            .EnumeratePrototypes<VoicePrototype>()
            .Where(voice => !voice.Silicon)];

        _voicePicker = new VoicePicker(_voices, _prototypeManager);
        _voicePicker.OnVoiceSelected += voice =>
        {
            Profile = Profile?.WithVoice(voice.ID);
            IsDirty = true;
            UpdateCurrentVoiceLabel();
        };
        _voicePicker.OnPreviewRequested += PreviewVoice;

        _siliconVoices = [.. _prototypeManager
            .EnumeratePrototypes<VoicePrototype>()
            .Where(voice => voice.Silicon)];

        _siliconVoicePicker = new VoicePicker(_siliconVoices, _prototypeManager) { Visible = false };
        _siliconVoicePicker.OnVoiceSelected += voice =>
        {
            Profile = Profile?.WithSiliconVoice(voice.ID);
            IsDirty = true;
            UpdateCurrentVoiceLabel();
        };
        _siliconVoicePicker.OnPreviewRequested += PreviewVoice;

        VoicePickerSlot.AddChild(_voicePicker);
        VoicePickerSlot.AddChild(_siliconVoicePicker);

        VoiceKindButton.AddItem(Loc.GetString("voice-selector-kind-character"), CharacterVoice);
        VoiceKindButton.AddItem(Loc.GetString("voice-selector-kind-silicon"), SiliconVoice);
        VoiceKindButton.SelectId(CharacterVoice);
        VoiceKindButton.Visible = _siliconVoices.Count > 0;
        VoiceKindButton.OnItemSelected += args =>
        {
            _voicePicker.Visible = args.Id == CharacterVoice;
            _siliconVoicePicker.Visible = args.Id == SiliconVoice;
            UpdateCurrentVoiceLabel();
        };
    }

    private void PreviewVoice(VoicePrototype voice)
        => _entManager.System<TextToSpeechSystem>().RequestPreviewTts(voice.ID);

    private void UpdateCurrentVoiceLabel()
    {
        var silicon = VoiceKindButton.SelectedId == SiliconVoice;
        var id = silicon ? Profile?.SiliconVoice : Profile?.Voice;
        var voice = (silicon ? _siliconVoices : _voices).FirstOrDefault(v => v.ID == id);
        CurrentVoiceLabel.Text = voice == null
            ? string.Empty
            : Loc.GetString("voice-selector-current-voice", ("voice", Loc.GetString(voice.Name)));
    }

    private void UpdateVoicesControls()
    {
        if (Profile is null)
            return;

        if (string.IsNullOrEmpty(Profile.Voice) && _voices.Count > 0)
            Profile.Voice = _voices[Random.Shared.Next(_voices.Count)].ID;

        _voicePicker.Select(Profile.Voice);
        UpdateCurrentVoiceLabel();
    }

    private void UpdateSiliconVoicesControls()
    {
        if (Profile is null)
            return;

        if (string.IsNullOrEmpty(Profile.SiliconVoice) && _siliconVoices.Count > 0)
            Profile.SiliconVoice = _siliconVoices[Random.Shared.Next(_siliconVoices.Count)].ID;

        _siliconVoicePicker.Select(Profile.SiliconVoice);
        UpdateCurrentVoiceLabel();
    }
}
