// ReSharper disable CheckNamespace
using System.Linq;
using Content.Shared.Administration;

namespace Content.Client.Administration.Systems;

public sealed partial class AdminSystem
{
    /// <summary>
    /// Set during replay playback, which has no playtime data.
    /// </summary>
    public bool HideOverlayPlaytime { get; set; }

    /// <summary>
    /// For replay playback, where the server's player list isn't recorded. Null clears it.
    /// </summary>
    public void SetPlayerList(IEnumerable<PlayerInfo>? players)
    {
        if (players == null)
        {
            _playerList = null;
            PlayerListChanged?.Invoke(new List<PlayerInfo>());
            return;
        }

        _playerList = players.ToDictionary(x => x.SessionId, x => x);
        PlayerListChanged?.Invoke(_playerList.Values.ToList());
    }
}
