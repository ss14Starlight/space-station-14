// ReSharper disable CheckNamespace
using System.Globalization;
using System.IO;
using System.Linq;
using Content.Client._Starlight.Replay;
using Content.Shared._Starlight.Replay;
using Content.Shared.Chat;
using Robust.Client.Replays.Loading;
using Robust.Shared.Replays;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Client.Replay;

public sealed partial class ContentReplayPlaybackManager
{
    [Dependency] private ILogManager _log = default!;

    // Round start from the loaded replay's final metadata, for recordings begun in the lobby.
    public TimeSpan? FinalRoundStart { get; private set; }

    private bool ShouldSkipChatMessage(ChatMessage chat) =>
        !_entMan.System<ReplayObserverSystem>().ShouldHearChat(chat);

    private void NoteRecordedMessage() =>
        _entMan.System<ReplayObserverSystem>().IsApplyingRecordedMessages = true;

    // Playback only keeps the start metadata, so the final metadata is read while the replay's files are open. This
    // manager handles every load, and exists before entity systems do.
    private void ReadFinalRoundStart(IReplayFileReader reader)
    {
        FinalRoundStart = null;
        if (!reader.Exists(ReplayConstants.FileMetaFinal))
            return;

        // This runs before the load job. A broken file is left for the engine, which reads the same file inside the job,
        // where the failure reaches the loading error screen.
        try
        {
            using var stream = reader.Open(ReplayConstants.FileMetaFinal);
            if (DataNodeParser.ParseYamlStream(new StreamReader(stream)).FirstOrDefault()?.Root is MappingDataNode final
                && final.TryGet(ReplayRoundTime.MetaKeyRoundStart, out ValueDataNode? node)
                && long.TryParse(node.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            {
                FinalRoundStart = TimeSpan.FromTicks(ticks);
            }
        }
        catch (Exception e)
        {
            _log.GetSawmill("replay").Warning($"Couldn't read the round start from final replay metadata: {e.Message}");
        }
    }
}
