using JetBrains.Annotations;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    readonly int _effectivePlayerCutoff = 30; // The number of online players at which unready players start counting as effectively ready
    readonly double _unreadyPlayerMultiplier = 0.60; // The fraction of unready players that count as effectively ready when above the cutoff

    /// <summary>
    /// Gets the player count used to determine whether presets and game rules can start.
    /// </summary>
    [PublicAPI]
    public int GetEffectivePlayerCountGamerule(int activePlayers)
    {
        var onlinePlayers = _playerManager.Sessions.Length;

        if (onlinePlayers < _effectivePlayerCutoff)
            return activePlayers;

        var unreadyPlayers = Math.Max(0, onlinePlayers - activePlayers);
        return activePlayers + (int) Math.Floor(unreadyPlayers * _unreadyPlayerMultiplier);
    }
}
