using System.Threading.Tasks;
using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.TextToSpeech;
using Content.Shared.Radio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.TextToSpeech;

public sealed partial class TTSSystem
{
    /// <summary>
    /// Plays a radio message out of several loudspeakers at once in the speaker's voice.
    /// The speech is generated once; every group hears it positioned at its own loudspeaker,
    /// so a listener given to only one group never hears an echo.
    /// </summary>
    public async void PlayAtSpeakers(
        EntityUid source,
        string text,
        LanguagePrototype language,
        ProtoId<RadioChannelPrototype> channel,
        IReadOnlyList<(EntityUid Speaker, Filter Listeners, float Volume)> groups)
    {
        if (!_isEnabled || groups.Count == 0 || text.Length > MaxChars)
            return;

        await Task.Yield();
        try
        {
            var streams = new List<(Guid Id, Filter Filter)>();
            foreach (var (speaker, listeners, volume) in groups)
            {
                // Devices like tape decks speak Universal, which everyone understands.
                var filter = listeners.RemovePlayers(_ignoredRecipients)
                    .RemoveWhere(x => x.AttachedEntity.HasValue
                        && x.AttachedEntity != source
                        && !_language.CanUnderstand(x.AttachedEntity.Value, language.ID));
                if (filter.Count == 0)
                    continue;

                var id = Guid.NewGuid();
                RaiseNetworkEvent(new TTSHeaderEvent
                {
                    Channel = channel,
                    Id = id,
                    Type = TTSType.Radio,
                    VolumeModifier = volume,
                    SourceUid = GetNetEntity(speaker),
                }, filter, false);
                streams.Add((id, filter));
            }

            if (streams.Count == 0)
                return;

            var voice = GetOrAssignVoice(source);
            await foreach (var chunk in _client.GenerateTTS(CleanText(text), voice, TTSEffect.Radio))
            {
                foreach (var (id, filter) in streams)
                    RaiseNetworkEvent(new TTSChunkEvent { Id = id, Data = chunk }, filter, false);
            }
        }
        catch (TaskCanceledException ex)
        {
            _sawmill.Info($"TTS loudspeakers were cancelled: {ex.Message}");
        }
        catch (Exception ex)
        {
            _sawmill.Error($"TTS loudspeakers error: {ex.Message}");
        }
    }
}
