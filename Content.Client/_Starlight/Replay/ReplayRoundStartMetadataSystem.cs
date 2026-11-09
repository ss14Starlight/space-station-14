using Content.Client.GameTicking.Managers;
using Content.Client.Gameplay;
using Content.Shared._Starlight.Replay;
using Robust.Client.State;

namespace Content.Client._Starlight.Replay;

public sealed partial class ReplayRoundStartMetadataSystem : SharedReplayRoundStartMetadataSystem
{
    [Dependency] private ClientGameTicker _ticker = default!;
    [Dependency] private IStateManager _state = default!;

    // Reconnecting into a round skips the lobby status that sets IsGameStarted, but leaves the client in gameplay.
    // Zero means the server hasn't sent the round start yet.
    protected override bool RoundStarted =>
        _ticker.RoundStartTimeSpan > TimeSpan.Zero && (_ticker.IsGameStarted || _state.CurrentState is GameplayStateBase);
}
