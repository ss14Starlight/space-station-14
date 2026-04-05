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
        catch (TaskCanceledException ex)
        {
            _sawmill.Info($"TTS Preview was cancelled: {ex.Message}");
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
            var filter = Filter.Entities(args.Receivers).RemovePlayers(_ignoredRecipients)
                .RemoveWhere(x => x.AttachedEntity.HasValue
                    && x.AttachedEntity != args.Source
                    && !_language.CanUnderstand(x.AttachedEntity.Value, args.Language.ID));
            var voice = GetOrAssignVoice(args.Source);
            var channel = new ProtoId<RadioChannelPrototype>(args.Channel.ID);
            var languageradio = args.Channel == args.Language.SpeechOverride.RadioChannel;
            var type = languageradio ? TTSType.Mind : TTSType.Radio;
            var effect = languageradio ? TTSEffect.Underwater : TTSEffect.Walkie;

            await GenerateAndStream(type, voice, text, filter, effect, chime, null, channel);
        }
        catch (TaskCanceledException ex)
        {
            _sawmill.Info($"TTS Radio was cancelled: {ex.Message}");
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
        catch (TaskCanceledException ex)
        {
            _sawmill.Info($"TTS Mind was cancelled: {ex.Message}");
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
        catch (TaskCanceledException ex)
        {
            _sawmill.Info($"TTS Announcement was cancelled: {ex.Message}");
        }
        catch (Exception ex)
        {
            _sawmill.Error($"TTS Announcement error: {ex.Message}");
        }
    }

    private async void OnEntitySpoke(EntityUid uid, TextToSpeechComponent component, EntitySpokeEvent args)
    {
        args.Message.Tts ??= args.Message.Text;
        if (!_isEnabled
            || args.Message.Tts.Length > MaxChars
            || (!args.Language.SpeechOverride.RequireSpeech && !args.Language.SpeechOverride.RequireSound)
            )
            return;

        await Task.Yield();
        try
        {
            var text = CleanText(args.Message.Tts);
            var filter = GetFilter(uid, args);
            var voice = GetOrAssignVoice(args.Source);
            var effect = args.Message.Modifier switch
            {
                SpeechModifier.None => TTSEffect.None,
                SpeechModifier.Spell => TTSEffect.Mystical,
                _ => TTSEffect.None
            };

            await GenerateAndStream(TTSType.IG, voice, text, filter, effect, null, uid,
                volume: args.IsWhisper ? WhisperVoiceVolumeModifier : 1f);
        }
        catch (TaskCanceledException ex)
        {
            _sawmill.Info($"TTS Entity was cancelled: {ex.Message}");
        }
        catch (Exception ex)
        {
            _sawmill.Error($"TTS Entity error: {ex.Message}");
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
        var id = Guid.NewGuid();

        RaiseNetworkEvent(new TTSHeaderEvent
        {
            Channel = channel,
            Id = id,
            Type = type,
            Chime = chime,
            VolumeModifier = volume,
            SourceUid = SourceUid.HasValue ? GetNetEntity(SourceUid.Value) : null,
        }, filter, false);

        await foreach (var chunk in _client.GenerateTTS(text, voice, effect))
            RaiseNetworkEvent(new TTSChunkEvent { Id = id, Data = chunk }, filter, false);
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
        text = TagStripperRegex().Replace(text, "");
        text = CharFilter().Replace(text, "");
        text = NumberConverter.NumberPattern().Replace(text, match => NumberConverter.Convert(match.Value));
        return text;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9,.\-?! ]")]
    private static partial Regex CharFilter();

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex TagStripperRegex();
}
