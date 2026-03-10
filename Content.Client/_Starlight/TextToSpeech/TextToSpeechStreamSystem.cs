using Content.Client._Starlight.TTS;
using Content.Shared.GameTicking;
using Content.Shared.Radio;
using Content.Shared.Starlight.TextToSpeech;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.TextToSpeech;

public sealed class TextToSpeechStreamSystem : EntitySystem
{
    // private ISawmill _sawmill = default!;
    private readonly Dictionary<Guid, TTSStream> _streams = [];
    private readonly HashSet<ProtoId<RadioChannelPrototype>> _mutedChannels = [];

    [Dependency] private readonly TextToSpeechSystem _tts = default!;

    public override void Initialize()
    {
        // FYI dark if you see this, just use Log? No need to grab a new sawmill...
        // _sawmill = Logger.GetSawmill("tts.stream");

        SubscribeNetworkEvent<TTSHeaderEvent>(OnHeader);
        SubscribeNetworkEvent<TTSChunkEvent>(OnChunk);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnReset);
    }

    public void SetChannelMuted(ProtoId<RadioChannelPrototype> channel, bool muted)
    {
        if (muted)
            _mutedChannels.Add(channel);
        else
            _mutedChannels.Remove(channel);
    }

    private void OnHeader(TTSHeaderEvent ev)
    {
        if (_streams.ContainsKey(ev.Id))
        {
            // _sawmill.Warning("Duplicate TTS header received for {Id}", ev.Id);
            return;
        }

        if (ev.Channel != null && _mutedChannels.Contains(ev.Channel.Value))
        {
            if (ev.Chime is not null)
                _tts.TryPlayChime([], AudioParams.Default, null, ev.Chime);
            return;
        }

        var stream = new TTSStream
        {
            Id = ev.Id,
            SourceUid = ev.SourceUid,
            Chime = ev.Chime,
            Type = ev.Type,
            VolumeModifier = ev.VolumeModifier,
        };

        _streams[ev.Id] = stream;
        // _sawmill.Debug("TTS stream started: {Id}", ev.Id);
    }

    private void OnChunk(TTSChunkEvent ev)
    {
        if (!_streams.TryGetValue(ev.Id, out var stream))
            return;

        if (ev.Data.Length == 0)
        {
            // _sawmill.Debug("TTS stream completed: {Id}", ev.Id);
            _streams.Remove(ev.Id);
        }
        else
        {
            // _sawmill.Debug("TTS stream {Id} received chunk of {Size} bytes", ev.Id, ev.Data.Length);
            stream.Data.Enqueue(ev.Data);
            if (!stream.IsStarted)
            {
                // _sawmill.Debug("TTS stream started playback: {Id}", ev.Id);
                stream.IsStarted = true;
                RaiseLocalEvent(stream);
            }
        }
    }

    private void OnReset(RoundRestartCleanupEvent ev) => Reset();

    private void Reset() => _streams.Clear();
}
