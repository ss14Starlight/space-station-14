using Content.Server.GameTicking;
using Content.Shared._Starlight.Replay;

namespace Content.Server._Starlight.Replay;

public sealed partial class ReplayRoundStartMetadataSystem : SharedReplayRoundStartMetadataSystem
{
    [Dependency] private GameTicker _ticker = default!;

    protected override bool RoundStarted => _ticker.RunLevel != GameRunLevel.PreRoundLobby;
}
