using Content.Server.GameTicking;
using Content.Shared.Audio.Events;

// ReSharper disable CheckNamespace
namespace Content.Server.Audio;

public sealed partial class ContentAudioSystem
{
    [SubscribeLocalEvent]
    private void OnPlayerJoinedGame(GameTicker.PlayerJoinedGameEvent ev)
    {
        if (_lobbyPlaylist == null || !IsPostRound())
            return;

        RaiseNetworkEvent(new LobbyPlaylistChangedEvent(_lobbyPlaylist, true), ev.PlayerSession);
    }

    private bool IsPostRound() => _gameTicker.RunLevel == GameRunLevel.PostRound;
}
