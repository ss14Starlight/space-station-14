using System.Collections.Concurrent;
using System.IO;
using Content.Client._Starlight.Radio.Systems;
using Content.Client._Starlight.TextToSpeech;
using Content.Shared.Starlight.CCVar;
using Content.Shared.Starlight.TextToSpeech;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Spawners;

namespace Content.Client._Starlight.TTS;

/// <summary>
/// Plays TTS audio
/// </summary>
public sealed class TextToSpeechSystem : EntitySystem
{
    protected override string SawmillName => "tts";

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedAudioSystem _sharedAudio = default!;
    [Dependency] private readonly IAudioManager _audioManager = default!;
    [Dependency] private readonly RadioChimeSystem _chime = default!;

    private readonly ConcurrentQueue<(Queue<byte[]> data, SoundSpecifier? specifier, float volume)> _ttsQueue = [];
    private readonly MemoryContentRoot _contentRoot = new();
    private (EntityUid Entity, AudioComponent Component)? _currentPlaying;

    private const float CrossFade = 0.010f;
    private float _volume;
    private float _radioVolume;
    private float _announceVolume;
    private float _chimeVolume;
    private bool _ttsQueueEnabled;

    public void ClearQueue()
    {
        _ttsQueue.Clear();

        if (_currentPlaying.HasValue)
        {
            var (entity, _) = _currentPlaying.Value;
            if (!Deleted(entity))
                QueueDel(entity);
            _currentPlaying = null;
        }
    }

    public override void Initialize()
    {
        _cfg.OnValueChanged(StarlightCCVars.TTSVolume, OnTtsVolumeChanged, true);
        _cfg.OnValueChanged(StarlightCCVars.TTSAnnounceVolume, OnTtsAnnounceVolumeChanged, true);
        _cfg.OnValueChanged(StarlightCCVars.TTSRadioVolume, OnTtsRadioVolumeChanged, true);
        _cfg.OnValueChanged(StarlightCCVars.TTSChimeVolume, OnTtsChimeVolumeChanged, true);
        _cfg.OnValueChanged(StarlightCCVars.TTSRadioQueueEnabled, OnTtsRadioQueueChanged, true);
        _cfg.OnValueChanged(StarlightCCVars.TTSClientEnabled, OnTtsClientOptionChanged, true);
        SubscribeLocalEvent<TTSStream>(OnTTSStream);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(StarlightCCVars.TTSVolume, OnTtsVolumeChanged);
        _cfg.UnsubValueChanged(StarlightCCVars.TTSAnnounceVolume, OnTtsAnnounceVolumeChanged);
        _cfg.UnsubValueChanged(StarlightCCVars.TTSRadioVolume, OnTtsRadioVolumeChanged);
        _cfg.UnsubValueChanged(StarlightCCVars.TTSChimeVolume, OnTtsChimeVolumeChanged);
        _cfg.UnsubValueChanged(StarlightCCVars.TTSRadioQueueEnabled, OnTtsRadioQueueChanged);
        _cfg.UnsubValueChanged(StarlightCCVars.TTSClientEnabled, OnTtsClientOptionChanged);
        _contentRoot.Dispose();
    }

    public void RequestPreviewTts(string voiceId)
        => RaiseNetworkEvent(new PreviewTTSRequestEvent() { VoiceId = voiceId });

    public bool TryPlayChime(Queue<byte[]> data, AudioParams audioParams, EntityUid? entity, SoundSpecifier chime)
    {
        if (_chime.IsMuted)
            return false;

        var audio = _sharedAudio.ResolveSound(chime);
        var ent = _audio.PlayGlobal(audio, EntityUid.Invalid, AudioParams.Default.WithVolume(_chimeVolume));
        if (ent != null)
        {
            var comp = EnsureComp<TTSAudioStreamComponent>(ent.Value.Entity);
            comp.Data = data;
            comp.EntityUid = ent.Value.Entity;
            comp.SourceUid = entity;
            comp.AudioParams = audioParams;
            comp.AudioLength = _audio.GetAudioLength(audio);
            _currentPlaying = ent;
            return true;
        }

        return false;
    }

    private void OnTtsVolumeChanged(float volume)
        => _volume = volume;

    private void OnTtsRadioVolumeChanged(float volume)
        => _radioVolume = volume;
    private void OnTtsChimeVolumeChanged(float volume)
        => _chimeVolume = volume;

    private void OnTtsRadioQueueChanged(bool enabled)
        => _ttsQueueEnabled = enabled;

    private void OnTtsAnnounceVolumeChanged(float volume)
        => _announceVolume = volume;

    private void OnTtsClientOptionChanged(bool option)
        => RaiseNetworkEvent(new ClientOptionTTSEvent { Enabled = option });

    private void PlayQueue()
    {
        if (!_ttsQueue.TryDequeue(out var entry))
            return;

        var volume = SharedAudioSystem.GainToVolume(entry.volume);
        var finalParams = AudioParams.Default.WithVolume(volume);

        if (entry.specifier == null || !TryPlayChime(entry.data, finalParams, null, entry.specifier))
            _currentPlaying = PlayTTS(entry.data, null, finalParams);
    }

    private void OnTTSStream(TTSStream ev)
    {
        var volume = ev.Type switch
        {
            TTSType.Announcement => _announceVolume,
            TTSType.System => _announceVolume,
            TTSType.Radio => _radioVolume,
            TTSType.Mind => _radioVolume,
            TTSType.IG => _volume,
            _ => _volume
        };

        if (ev.Type == TTSType.Announcement)
        {
            _ttsQueue.Enqueue((ev.Data, !_chime.IsMuted ? ev.Chime : null, _radioVolume));
        }
        else
        {
            volume = SharedAudioSystem.GainToVolume(volume * ev.VolumeModifier);
            var audioParams = AudioParams.Default.WithVolume(volume);
            var entity = GetEntity(ev.SourceUid);

            if (ev.Chime is SoundSpecifier chime && TryPlayChime(ev.Data, audioParams, entity, chime))
                return;
            _currentPlaying = PlayTTS(ev.Data, entity, audioParams);
        }
    }

    private (EntityUid Entity, AudioComponent Component)? PlayTTS(
        Queue<byte[]> data,
        EntityUid? sourceUid = null,
        AudioParams? audioParams = null,
        (EntityUid eid, AudioComponent audio, TTSAudioStreamComponent tts)? previous = null)
    {
        try
        {
            if (!data.TryDequeue(out var audioBytes))
                return null;

            if (audioBytes.Length < 10 || (sourceUid != null && sourceUid.Value.Id == 0))
                return null;

            var silencePadding = 1f;
            var @params = audioParams ?? AudioParams.Default;
            var audioStream = _audioManager.LoadAudioOggVorbis(new MemoryStream(audioBytes));

            if (previous is var (eid, audio, tts))
                silencePadding = Math.Clamp(1f - (float)(tts.AudioLength.TotalSeconds - audio.PlaybackPosition) - CrossFade, 0f, 1f);

            Log.Debug($"Play TTS chunk: {audioBytes.Length}, prependSilence: {silencePadding:F3}s");
            @params = @params.WithPlayOffset(silencePadding);
            var ent = sourceUid != null && sourceUid != _player.LocalEntity
                ? _audio.PlayEntity(audioStream, sourceUid.Value, null, @params)
                : _audio.PlayGlobal(audioStream, null, @params);

            if (ent != null)
            {
                var comp = EnsureComp<TTSAudioStreamComponent>(ent.Value.Entity);
                comp.Data = data;
                comp.EntityUid = ent.Value.Entity;
                comp.SourceUid = sourceUid;
                comp.AudioParams = audioParams;
                comp.AudioLength = audioStream.Length;

                if (_currentPlaying.HasValue && previous.HasValue && previous.Value.eid == ent.Value.Entity)
                    _currentPlaying = ent;
            }

            return ent;
        }
        catch (Exception ex)
        {
            Log.Error($"Error playing TTS audio: {ex.Message}", ex);
        }

        return null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toPlay = new List<(EntityUid eid, AudioComponent audio, TTSAudioStreamComponent tts)>();
        var query = EntityQueryEnumerator<TTSAudioStreamComponent, TimedDespawnComponent, AudioComponent>();

        while (query.MoveNext(out var uid, out var ttsComp, out var despawnComponent, out var audio))
        {
            if (ttsComp.Handled)
                continue;
            var timeRemaining = despawnComponent.Lifetime - SharedAudioSystem.AudioDespawnBuffer - 1f;

            if (timeRemaining < 0.066f && (ttsComp.AudioLength.TotalSeconds - audio.PlaybackPosition) < 0.096f)
                toPlay.Add((uid, audio, ttsComp));
        }

        foreach (var (eid, audio, tts) in toPlay)
        {
            if (PlayTTS(tts.Data, tts.SourceUid, tts.AudioParams, (eid, audio, tts)) is not null)
                tts.Handled = true;
        }

        if (_currentPlaying.HasValue)
        {
            var (entity, _) = _currentPlaying.Value;

            if (Deleted(entity))
                _currentPlaying = null;
            else
                return;
        }

        PlayQueue();
    }
}
