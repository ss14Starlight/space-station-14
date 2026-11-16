namespace Content.Server._Daiquiri.CartridgeLoader.Cartridges;

/// <summary>
/// Outcome of a finished Chess.NT game. The Board API bridge will map Lichess game
/// statuses to this enum when it reports a result.
/// </summary>
public enum ChessGameOutcome
{
    WhiteWin,
    BlackWin,
    Draw,
}

/// <summary>
/// Everything the Discord result webhook needs to identify one player:
/// the SS14 account name, the current character name and the linked Lichess name.
/// </summary>
public readonly record struct ChessPlayerResult(
    string SessionName,
    string CharacterName,
    string LichessUsername)
{
    /// <summary>
    /// A Lichess opponent who is not an SS14 player (for example, a random human
    /// opponent found through the Board API). Only the Lichess name is known.
    /// </summary>
    public static ChessPlayerResult LichessOnly(string lichessUsername)
    {
        return new ChessPlayerResult(string.Empty, string.Empty, lichessUsername);
    }
}
