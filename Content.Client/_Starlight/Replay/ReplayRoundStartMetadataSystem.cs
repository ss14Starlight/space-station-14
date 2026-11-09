using Content.Client.GameTicking.Managers;
using Content.Shared._Starlight.Replay;

namespace Content.Client._Starlight.Replay;

public sealed class ReplayRoundStartMetadataSystem : SharedReplayRoundStartMetadataSystem
{
    [Dependency] private ClientGameTicker _ticker = default!;

    protected override bool RoundStarted => _ticker.IsGameStarted;
}
