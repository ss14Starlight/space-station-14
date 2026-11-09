using System.Globalization;
using System.IO;
using System.Linq;
using Content.Client.GameTicking.Managers;
using Content.Client.Gameplay;
using Content.Shared._Starlight.Replay;
using Robust.Client.Replays.Loading;
using Robust.Client.State;
using Robust.Shared.Replays;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Client._Starlight.Replay;

public sealed class ReplayRoundStartMetadataSystem : SharedReplayRoundStartMetadataSystem
{
    [Dependency] private ClientGameTicker _ticker = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IReplayLoadManager _loadMan = default!;

    // Round start from the loaded replay's final metadata, for recordings begun in the lobby.
    public TimeSpan? FinalRoundStart { get; private set; }

    // Reconnecting into a round skips the lobby status that sets IsGameStarted, but leaves the client in gameplay.
    // Zero means the server hasn't sent the round start yet.
    protected override bool RoundStarted =>
        _ticker.RoundStartTimeSpan > TimeSpan.Zero && (_ticker.IsGameStarted || _state.CurrentState is GameplayStateBase);

    public override void Initialize()
    {
        base.Initialize();

        _loadMan.LoadOverride += OnReplayLoading;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _loadMan.LoadOverride -= OnReplayLoading;
    }

    // Playback only keeps the start metadata, so the final metadata is read here while the replay's files are open.
    private void OnReplayLoading(IReplayFileReader reader)
    {
        FinalRoundStart = null;
        if (!reader.Exists(ReplayConstants.FileMetaFinal))
            return;

        using var stream = reader.Open(ReplayConstants.FileMetaFinal);
        if (DataNodeParser.ParseYamlStream(new StreamReader(stream)).FirstOrDefault()?.Root is MappingDataNode final
            && final.TryGet(ReplayRoundTime.MetaKeyRoundStart, out ValueDataNode? node)
            && long.TryParse(node.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
        {
            FinalRoundStart = TimeSpan.FromTicks(ticks);
        }
    }
}
