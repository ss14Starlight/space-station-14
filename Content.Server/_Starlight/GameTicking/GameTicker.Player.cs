using Robust.Shared.Player;

// ReSharper disable CheckNamespace
namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    /// <summary>
    /// Raised specifically when entering the game proper, as in spawning in, observing,
    /// or reconnecting while already in-game and not the lobby.
    /// </summary>
    public sealed class PlayerJoinedGameEvent(ICommonSession playerSession) : EntityEventArgs
    {
        public readonly ICommonSession PlayerSession = playerSession;
    }
}
