using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Content.Server._Starlight.Language;
using Content.Server._Starlight.Radio.Systems;
using Content.Server._Starlight.TextToSpeech;
using Content.Shared._Starlight.Speech;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Content.Shared.Starlight.CCVar;
using Content.Shared.Starlight.TextToSpeech;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Starlight.TTS;

public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly RadioChimeSystem _chime = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ITTSClient _client = default!;
    [Dependency] private readonly IRobustRandom _rng = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    private readonly List<string> _sampleText =
    [
        "Can someone bring me a pair of insulating gloves, please?",
        "Security, the clown has stolen the captain's ID!",
        "The singularity has reached the arrivals area!",
    ];

    private const int DefaultAnnounceVoice = 2001;
    private const int DefaultVoice = 0;
    private const int MaxChars = 200;
    private const float WhisperVoiceVolumeModifier = 0.6f;
    private readonly ISawmill _sawmill = Logger.GetSawmill(nameof(TTSSystem));
    private readonly List<ICommonSession> _ignoredRecipients = [];

    private bool _isEnabled;

    public override void Initialize()
    {
        _cfg.OnValueChanged(StarlightCCVars.TTSEnabled, v => _isEnabled = v, true);

        SubscribeNetworkEvent<PreviewTTSRequestEvent>(OnRequestPreviewTTS);
        SubscribeNetworkEvent<ClientOptionTTSEvent>(OnClientOptionTTS);

        SubscribeLocalEvent<TextToSpeechComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<RadioSpokeEvent>(OnRadioReceiveEvent);
        SubscribeLocalEvent<CollectiveMindSpokeEvent>(OnCollectiveMindReceiveEvent);
        SubscribeLocalEvent<AnnouncementSpokeEvent>(OnAnnouncementSpoke);
    }

    private async void OnRequestPreviewTTS(PreviewTTSRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!_isEnabled) return;

        await Task.Yield();
        try
        {
            if (!_prototypeManager.TryIndex<VoicePrototype>(ev.VoiceId, out var protoVoice))
                return;

            var previewText = _rng.Pick(_sampleText);
            var filter = Filter.SinglePlayer(args.SenderSession);

            await GenerateAndStream(TTSType.System, protoVoice.Voice, previewText, filter);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"TTS Preview error: {ex.Message}");
        }
    }

    private async void OnRadioReceiveEvent(RadioSpokeEvent args)
    {
        args.Message.Tts ??= args.Message.Text;
        if (!_isEnabled
            || args.Message.Tts.Length > MaxChars
            || args.SuppressTTS)
            return;

        await Task.Yield();
        try
        {
            var text = CleanText(args.Message.Tts);
            _chime.TryGetSenderHeadsetChime(args.Source, out var chime);
            var filter = Filter.Entities(args.Receivers).RemovePlayers(_ignoredRecipients);
            var voice = GetOrAssignVoice(args.Source);
            var channel = new ProtoId<RadioChannelPrototype>(args.Channel.ID);

            await GenerateAndStream(TTSType.Radio, voice, text, filter, TTSEffect.Walkie, chime, null, channel);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"TTS Radio error: {ex.Message}");
        }
    }

    private async void OnCollectiveMindReceiveEvent(CollectiveMindSpokeEvent args)
    {
        if (!_isEnabled
            || args.Message.Length > MaxChars)
            return;

        await Task.Yield();
        try
        {
            var text = CleanText(args.Message);
            var filter = Filter.Entities(args.Receivers).RemovePlayers(_ignoredRecipients);
            var voice = GetOrAssignVoice(args.Source);

            await GenerateAndStream(TTSType.Mind, voice, text, filter, TTSEffect.Underwater);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"TTS Mind error: {ex.Message}");
        }
    }

    private async void OnAnnouncementSpoke(AnnouncementSpokeEvent args)
    {
        if (!_isEnabled
            || args.Message.Text.Length > MaxChars * 2)
            return;

        await Task.Yield();
        try
        {
            var text = CleanText(args.Message.Tts ?? args.Message.Text);
            var filter = args.Receivers.RemovePlayers(_ignoredRecipients);
            var voice = args.SpeakerUid.HasValue
                ? GetOrAssignVoice(GetEntity(args.SpeakerUid.Value), fallbackVoice: DefaultAnnounceVoice)
                : DefaultAnnounceVoice;

            await GenerateAndStream(TTSType.Announcement, voice, text, filter, TTSEffect.Megaphone, args.AnnouncementSound);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"TTS Announcement error: {ex.Message}");
        }
    }
    private void OnTransformSpeech(TransformSpeechEvent args)
    {
        if (!_isEnabled) return;
        args.Message = args.Message.Replace("+", "");
    }
    private async void HandleSay(EntityUid uid, string message, int voice, LanguagePrototype language)
    {
        var recipients = Filter.Pvs(uid, 1F).RemovePlayers(_ignoredRecipients);
        var understoodRecipients = Filter.Empty();
        var misunderstoodRecipients = Filter.Empty();

        var selfUnderstands = _language.CanUnderstand(uid, language.ID);
        var soundData = (understoodRecipients.Count > 0 || selfUnderstands)
            ? await GenerateTTS(message, voice) : null;
            
        var obfuscatedData = misunderstoodRecipients.Count > 0
            ? await GenerateTTS(_language.ObfuscateSpeech(message, language), voice) : null;

        foreach (var session in recipients.Recipients)
        {
            if (!session.AttachedEntity.HasValue)
                continue;

            if (_language.CanUnderstand(session.AttachedEntity.Value, language.ID))
                understoodRecipients.AddPlayer(session);
            else
                misunderstoodRecipients.AddPlayer(session);
        }

        if (TryComp<EyeComponent>(uid, out var eye) && eye is not null)
        {
            understoodRecipients.RemovePlayerByAttachedEntity(uid);
            misunderstoodRecipients.RemovePlayerByAttachedEntity(uid);

             if (selfUnderstands && soundData is not null)
            {
                SpeechModifier.None => TTSEffect.None,
                SpeechModifier.Spell => TTSEffect.Mystical,
                _ => TTSEffect.None
            };

        if (soundData is not null && understoodRecipients.Count > 0)
        {
            RaiseNetworkEvent(new PlayTTSEvent
            {
                Data = soundData,
                SourceUid = GetNetEntity(uid)
            }, understoodRecipients, false);
        }

        if (obfuscatedData is not null && misunderstoodRecipients.Count > 0)
        {
            RaiseNetworkEvent(new PlayTTSEvent
            {
                Data = obfuscatedData,
                SourceUid = GetNetEntity(uid)
            }, misunderstoodRecipients, false);
        }
    }

    private Filter GetFilter(EntityUid uid, EntitySpokeEvent args)
    {
        Filter filter;
        if (!args.IsWhisper)
        {
            filter = Filter.Pvs(uid, 1F);
        }
        else
        {
            var xform = Comp<TransformComponent>(uid);
            var mapCoords = _xforms.GetMapCoordinates(xform);
            filter = Filter.Empty()
               .AddInRange(mapCoords, SharedChatSystem.WhisperClearRange);
        }

        return filter.RemovePlayers(_ignoredRecipients)
               .RemoveWhere(x => x.AttachedEntity.HasValue
                   && x.AttachedEntity != uid
                   && !_language.CanUnderstand(x.AttachedEntity.Value, args.Language.ID));
    }

    private async Task GenerateAndStream(TTSType type,
                                         int voice,
                                         string text,
                                         Filter filter,
                                         TTSEffect effect = TTSEffect.None,
                                         SoundSpecifier? chime = null,
                                         EntityUid? SourceUid = null,
                                         ProtoId<RadioChannelPrototype>? channel = null,
                                         float volume = 1f)
    {
        var recipients = Filter.Entities(uIds).RemovePlayers(_ignoredRecipients);
        var understoodRecipients = Filter.Empty();
        var misunderstoodRecipients = Filter.Empty();

        var soundData = understoodRecipients.Count > 0
            ? await GenerateTTS(message, voice, isRadio: true) : null;

        var obfuscatedData = misunderstoodRecipients.Count > 0
            ? await GenerateTTS(_language.ObfuscateSpeech(message, language), voice, isRadio: true) : null;

        foreach (var session in recipients.Recipients)
        {
            if (!session.AttachedEntity.HasValue)
                continue;

            if (_language.CanUnderstand(session.AttachedEntity.Value, language.ID))
                understoodRecipients.AddPlayer(session);
            else
                misunderstoodRecipients.AddPlayer(session);
        }

        if (soundData is not null && understoodRecipients.Count > 0)
        {
            RaiseNetworkEvent(new PlayTTSEvent
            {
                IsRadio = true,
                Chime = chime,
                Data = soundData
            }, understoodRecipients, false);
        }

        if (obfuscatedData is not null && misunderstoodRecipients.Count > 0)
        {
            RaiseNetworkEvent(new PlayTTSEvent
            {
                IsRadio = true,
                Chime = chime,
                Data = obfuscatedData
            }, misunderstoodRecipients, false);
        }
    }

    private async void OnClientOptionTTS(ClientOptionTTSEvent ev, EntitySessionEventArgs args)
    {
        if (ev.Enabled)
            _ignoredRecipients.Remove(args.SenderSession);
        else
            _ignoredRecipients.Add(args.SenderSession);
    }

    private static string CleanText(string text)
    {
        try
        {
            text = _chat.SanitizeMessageReplaceWords(text);
            text = DecimalConverter().Replace(text, " point ");
            text = Number2Word().Replace(text, ReplaceNumber2Word);
            text = SymbolFilter().Replace(text, ReplaceAbbreviations);
            text = CharFilter().Replace(text.Trim(), "");

            if (text == "") return null;
            if (char.IsLetter(text[^1]))
                text += ".";

            return isRadio
                ? await _ttsManager.ConvertTextToSpeechRadio(voice, text)
                : isAnnounce
                    ? await _ttsManager.ConvertTextToSpeechAnnounce(voice, text)
                    : await _ttsManager.ConvertTextToSpeechStandard(voice, text);
        }
        catch (Exception e)
        {
            _sawmill.Error($"TTS System error: {e.Message}");
        }

        return null;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9,.\-?! ]")]
    private static partial Regex CharFilter();

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex TagStripperRegex();
}
