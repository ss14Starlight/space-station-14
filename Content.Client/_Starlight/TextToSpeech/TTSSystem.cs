using System.Collections.Concurrent;
using System.IO;
using Content.Client._Starlight.Radio.Systems;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.TextToSpeech;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Spawners;

namespace Content.Client._Starlight.TextToSpeech;

/// <summary>
/// Plays TTS audio. Announcements use a global queue; IG/radio/mind use a
/// per-speaker queue so one character's lines never overlap while others can talk.
/// </summary>
public sealed partial class TextToSpeechSystem : EntitySystem
{
    protected override string SawmillName => "tts";

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedAudioSystem _sharedAudio = default!;
    [Dependency] private IAudioManager _audioManager = default!;
    [Dependency] private RadioChimeSystem _chime = default!;

    private readonly ConcurrentQueue<(Queue<byte[]> data, SoundSpecifier? specifier, float volume)> _announceQueue = [];
    private (EntityUid Entity, AudioComponent Component)? _announcePlaying;

    /// <summary>Per-speaker FIFO of completed TTS streams waiting to play.</summary>
    private readonly Dictionary<EntityUid, Queue<SpeakerQueuedTts>> _speakerQueues = new();

    /// <summary>Speaker source uid → currently playing audio entity.</summary>
    private readonly Dictionary<EntityUid, EntityUid> _speakerPlaying = new();

    private readonly MemoryContentRoot _contentRoot = new();

    private static readonly TimeSpan MaxChimeLength = TimeSpan.FromSeconds(3);
    private const float CrossFade = 0.010f;
    private float _volume;
    private float _radioVolume;
    private float _announceVolume;
    private float _chimeVolume;
    private bool _speakerQueueEnabled;

    private readonly record struct SpeakerQueuedTts(
        Queue<byte[]> Data,
        SoundSpecifier? Chime,
        AudioParams Params,
        EntityUid SourceUid);

    public void ClearQueue()
    {
        _announceQueue.Clear();
        _speakerQueues.Clear();

        if (_announcePlaying.HasValue)
        {
            var (entity, _) = _announcePlaying.Value;
            if (!Deleted(entity))
                QueueDel(entity);
            _announcePlaying = null;
        }

        foreach (var audioUid in _speakerPlaying.Values)
        {
            if (!Deleted(audioUid))
                QueueDel(audioUid);
        }

        _speakerPlaying.Clear();
    }

    public override void Initialize()
    {
        _cfg.OnValueChanged(StarlightCCVars.TTSVolume, OnTtsVolumeChanged, true);
        _cfg.OnValueChanged(StarlightCCVars.TTSAnnounceVolume, OnTtsAnnounceVolumeChanged, true);
        _cfg.OnValueChanged(StarlightCCVars.TTSRadioVolume, OnTtsRadioVolumeChanged, true);
        _cfg.OnValueChanged(StarlightCCVars.TTSChimeVolume, OnTtsChimeVolumeChanged, true);
        _cfg.OnValueChanged(StarlightCCVars.TTSRadioQueueEnabled, OnTtsSpeakerQueueChanged, true);
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
        _cfg.UnsubValueChanged(StarlightCCVars.TTSRadioQueueEnabled, OnTtsSpeakerQueueChanged);
        _cfg.UnsubValueChanged(StarlightCCVars.TTSClientEnabled, OnTtsClientOptionChanged);
        _contentRoot.Dispose();
    }

    public void RequestPreviewTts(string voiceId)
        => RaiseNetworkEvent(new PreviewTTSRequestEvent() { VoiceId = voiceId });

    /// <summary>
    /// Plays a radio chime and attaches <paramref name="data"/> so speech continues after it.
    /// Returns the chime audio entity when successful.
    /// </summary>
    public EntityUid? TryPlayChime(Queue<byte[]> data, AudioParams audioParams, EntityUid? entity, SoundSpecifier chime)
    {
        if (_chime.IsMuted)
            return null;

        var audio = _sharedAudio.ResolveSound(chime);
        var ent = _audio.PlayGlobal(audio, EntityUid.Invalid, AudioParams.Default.WithVolume(_chimeVolume));
        if (ent == null)
            return null;

        var audioLength = _audio.GetAudioLength(audio);
        var comp = EnsureComp<TTSAudioStreamComponent>(ent.Value.Entity);
        comp.Data = data;
        comp.EntityUid = ent.Value.Entity;
        comp.SourceUid = entity;
        comp.AudioParams = audioParams;
        comp.AudioLength = audioLength > MaxChimeLength ? MaxChimeLength : audioLength;
        return ent.Value.Entity;
    }

    private void OnTtsVolumeChanged(float volume)
        => _volume = volume;

    private void OnTtsRadioVolumeChanged(float volume)
        => _radioVolume = volume;

    private void OnTtsChimeVolumeChanged(float volume)
        => _chimeVolume = volume;

    private void OnTtsSpeakerQueueChanged(bool enabled)
        => _speakerQueueEnabled = enabled;

    private void OnTtsAnnounceVolumeChanged(float volume)
        => _announceVolume = volume;

    private void OnTtsClientOptionChanged(bool option)
        => RaiseNetworkEvent(new ClientOptionTTSEvent { Enabled = option });

    private void PlayAnnounceQueue()
    {
        if (!_announceQueue.TryDequeue(out var entry))
            return;

        var volume = SharedAudioSystem.GainToVolume(entry.volume);
        var finalParams = AudioParams.Default.WithVolume(volume);

        if (entry.specifier is SoundSpecifier chime)
        {
            var chimeUid = TryPlayChime(entry.data, finalParams, null, chime);
            if (chimeUid != null && TryComp<AudioComponent>(chimeUid.Value, out var audio))
            {
                _announcePlaying = (chimeUid.Value, audio);
                return;
            }
        }

        var played = PlayTTS(entry.data, null, finalParams);
        if (played != null)
            _announcePlaying = played;
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
            _announceQueue.Enqueue((ev.Data, !_chime.IsMuted ? ev.Chime : null, _radioVolume));
            return;
        }

        volume = SharedAudioSystem.GainToVolume(volume * ev.VolumeModifier);
        var audioParams = AudioParams.Default.WithVolume(volume);
        var entity = GetEntity(ev.SourceUid);

        if (!_speakerQueueEnabled || entity is not { } sourceUid || !sourceUid.IsValid())
        {
            StartSpeakerPlayback(ev.Data, ev.Chime, audioParams, entity ?? EntityUid.Invalid);
            return;
        }

        if (IsSpeakerBusy(sourceUid))
        {
            if (!_speakerQueues.TryGetValue(sourceUid, out var queue))
            {
                queue = new Queue<SpeakerQueuedTts>();
                _speakerQueues[sourceUid] = queue;
            }

            queue.Enqueue(new SpeakerQueuedTts(ev.Data, !_chime.IsMuted ? ev.Chime : null, audioParams, sourceUid));
            return;
        }

        StartSpeakerPlayback(ev.Data, !_chime.IsMuted ? ev.Chime : null, audioParams, sourceUid);
    }

    private bool IsSpeakerBusy(EntityUid sourceUid)
    {
        if (!_speakerPlaying.TryGetValue(sourceUid, out var audioUid))
            return false;

        if (!Deleted(audioUid))
            return true;

        _speakerPlaying.Remove(sourceUid);
        return false;
    }

    private void StartSpeakerPlayback(
        Queue<byte[]> data,
        SoundSpecifier? chime,
        AudioParams audioParams,
        EntityUid sourceUid)
    {
        EntityUid? audioEntity = null;
        var source = sourceUid.IsValid() ? sourceUid : (EntityUid?)null;

        if (chime is SoundSpecifier chimeSpec)
            audioEntity = TryPlayChime(data, audioParams, source, chimeSpec);

        if (audioEntity == null)
        {
            var played = PlayTTS(data, source, audioParams);
            if (played != null)
                audioEntity = played.Value.Entity;
        }

        if (audioEntity != null && sourceUid.IsValid())
            _speakerPlaying[sourceUid] = audioEntity.Value;
    }

    private void TryPlayNextForSpeaker(EntityUid sourceUid)
    {
        if (!_speakerQueues.TryGetValue(sourceUid, out var queue) || queue.Count == 0)
        {
            _speakerQueues.Remove(sourceUid);
            _speakerPlaying.Remove(sourceUid);
            return;
        }

        var next = queue.Dequeue();
        if (queue.Count == 0)
            _speakerQueues.Remove(sourceUid);

        StartSpeakerPlayback(next.Data, next.Chime, next.Params, next.SourceUid);
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
            var played = PlayTTS(tts.Data, tts.SourceUid, tts.AudioParams, (eid, audio, tts));
            if (played is null)
                continue;

            tts.Handled = true;

            // Multi-chunk continuation creates a new audio entity — keep queue tracking current.
            if (tts.SourceUid is { } src && src.IsValid() && _speakerPlaying.ContainsKey(src))
                _speakerPlaying[src] = played.Value.Entity;

            if (_announcePlaying?.Entity == eid)
                _announcePlaying = played;
        }

        // Advance per-speaker queues when the current clip for that speaker is gone.
        if (_speakerPlaying.Count > 0)
        {
            List<EntityUid>? finished = null;
            foreach (var (sourceUid, audioUid) in _speakerPlaying)
            {
                if (!Deleted(audioUid))
                    continue;
                finished ??= [];
                finished.Add(sourceUid);
            }

            if (finished != null)
            {
                foreach (var sourceUid in finished)
                    TryPlayNextForSpeaker(sourceUid);
            }
        }

        if (_announcePlaying.HasValue)
        {
            var (entity, _) = _announcePlaying.Value;
            if (Deleted(entity))
                _announcePlaying = null;
            else
                return;
        }

        PlayAnnounceQueue();
    }
}
