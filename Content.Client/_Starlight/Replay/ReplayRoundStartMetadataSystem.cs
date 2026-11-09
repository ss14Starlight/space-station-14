using Content.Client.GameTicking.Managers;
using Content.Client.Gameplay;
using Content.Shared._Starlight.Replay;
using Robust.Client.State;

namespace Content.Client._Starlight.Replay;

public sealed class ReplayRoundStartMetadataSystem : SharedReplayRoundStartMetadataSystem
{
    [Dependency] private ClientGameTicker _ticker = default!;
    [Dependency] private IStateManager _state = default!;

    // Reconnecting into a round skips the lobby status that sets IsGameStarted, but leaves the client in gameplay.
    protected override bool RoundStarted => _ticker.IsGameStarted || _state.CurrentState is GameplayStateBase;
}
