using Robust.Shared.Serialization;

namespace Content.Shared._Daiquiri.CartridgeLoader.Cartridges;

/// <summary>
/// Public Lichess link state. The access token is intentionally absent: it must never
/// leave the server.
/// </summary>
[Serializable, NetSerializable]
public sealed class ChessLinkStatusState : BoundUserInterfaceState
{
    public bool Configured;
    public bool Linked;
    public string Username;
    public string LichessUserId;
    public bool TokenExpired;
    public string Error;

    /// <summary>
    /// Lichess has temporarily banned this account from playing. Starting a new search or
    /// challenge is pointless until the ban expires.
    /// </summary>
    public bool Blocked;

    /// <summary>Human readable reason for <see cref="Blocked"/>.</summary>
    public string BlockReason;

    /// <summary>Approximate minutes left until the block expires, 0 when unknown.</summary>
    public int BlockedMinutesLeft;

    /// <summary>
    /// The server could not use the stored token (revoked, missing scope or a 401). The
    /// player has to link the account again before searching for a game.
    /// </summary>
    public bool AccessBroken;

    public ChessLinkStatusState(
        bool configured,
        bool linked,
        string username,
        string lichessUserId,
        bool tokenExpired,
        string error = "",
        bool blocked = false,
        string blockReason = "",
        int blockedMinutesLeft = 0,
        bool accessBroken = false)
    {
        Configured = configured;
        Linked = linked;
        Username = username;
        LichessUserId = lichessUserId;
        TokenExpired = tokenExpired;
        Error = error;
        Blocked = blocked;
        BlockReason = blockReason;
        BlockedMinutesLeft = blockedMinutesLeft;
        AccessBroken = accessBroken;
    }
}

/// <summary>
/// Live game snapshot for chess.nt. Contains only public data: the board position,
/// clocks and the opponent's public Lichess name. Never contains the access token.
/// </summary>
[Serializable, NetSerializable]
public sealed class ChessGameState : BoundUserInterfaceState
{
    /// <summary>True while the server is waiting for a Lichess opponent.</summary>
    public bool Searching;

    /// <summary>True while a game exists (waiting for acceptance or being played).</summary>
    public bool InGame;

    public string GameId;
    public string GameUrl;

    /// <summary>"white", "black" or empty when not in a game.</summary>
    public string Color;

    public string OpponentName;

    /// <summary>64 characters, index 0 = a8, index 63 = h1. "." for an empty square.</summary>
    public string Board;

    /// <summary>Side to move: "white" or "black".</summary>
    public string Turn;

    /// <summary>Last move in UCI notation, empty at game start.</summary>
    public string LastMove;

    /// <summary>Lichess game status: created, started, mate, resign, draw, aborted...</summary>
    public string Status;

    public string StatusText;

    /// <summary>"white", "black", "draw" or empty.</summary>
    public string Winner;

    public int WhiteTimeMs;
    public int BlackTimeMs;

    public string Error;

    /// <summary>
    /// True when the current viewer is not the player who started the game. The viewer may
    /// finish the game but never receives access to the owner's Lichess account.
    /// </summary>
    public bool Delegated;

    /// <summary>SS14 nickname of the player who started the game, set for a delegated view.</summary>
    public string OwnerName;

    /// <summary>Whether this client is allowed to submit moves right now.</summary>
    public bool CanControl;

    /// <summary>Lichess name of the account playing this game (the PDA owner's account).</summary>
    public string PlayerName;

    /// <summary>Time control of the game, for example "10+0".</summary>
    public string TimeControl;

    /// <summary>Castling rights in FEN form: "KQkq", "Kq" or empty.</summary>
    public string Castling;

    /// <summary>En passant target square, for example "e3", or empty.</summary>
    public string EnPassant;

    /// <summary>True for an unlimited (correspondence) game: the clocks never tick.</summary>
    public bool Unlimited;

    /// <summary>Pending incoming challenge id, empty when there is nothing to accept.</summary>
    public string ChallengeId;

    /// <summary>Lichess name of the player who sent the pending challenge.</summary>
    public string ChallengerName;

    /// <summary>Time control of the pending challenge, for example "5+3".</summary>
    public string ChallengeTimeControl;

    public bool ChallengeRated;

    /// <summary>Colour the challenger gave us: "white", "black" or "random".</summary>
    public string ChallengeColor;

    public ChessGameState(
        bool searching,
        bool inGame,
        string gameId,
        string gameUrl,
        string color,
        string opponentName,
        string board,
        string turn,
        string lastMove,
        string status,
        string statusText,
        string winner,
        int whiteTimeMs,
        int blackTimeMs,
        string error = "",
        bool delegated = false,
        string ownerName = "",
        bool canControl = false,
        string playerName = "",
        string timeControl = "",
        string castling = "",
        string enPassant = "",
        bool unlimited = false,
        string challengeId = "",
        string challengerName = "",
        string challengeTimeControl = "",
        bool challengeRated = false,
        string challengeColor = "")
    {
        Searching = searching;
        InGame = inGame;
        GameId = gameId;
        GameUrl = gameUrl;
        Color = color;
        OpponentName = opponentName;
        Board = board;
        Turn = turn;
        LastMove = lastMove;
        Status = status;
        StatusText = statusText;
        Winner = winner;
        WhiteTimeMs = whiteTimeMs;
        BlackTimeMs = blackTimeMs;
        Error = error;
        Delegated = delegated;
        OwnerName = ownerName;
        CanControl = canControl;
        PlayerName = playerName;
        TimeControl = timeControl;
        Castling = castling;
        EnPassant = enPassant;
        Unlimited = unlimited;
        ChallengeId = challengeId;
        ChallengerName = challengerName;
        ChallengeTimeControl = challengeTimeControl;
        ChallengeRated = challengeRated;
        ChallengeColor = challengeColor;
    }
}

/// <summary>
/// One-shot OAuth start data. The PKCE verifier stays on the server; only the URL the
/// client should open and the loopback redirect Lichess will use are sent.
/// </summary>
[Serializable, NetSerializable]
public sealed class ChessLinkStartState : BoundUserInterfaceState
{
    public string AuthUrl;
    public string RedirectUri;
    public float TimeoutSeconds;
    public string RequestId;
    public string Error;

    public ChessLinkStartState(
        string authUrl,
        string redirectUri,
        float timeoutSeconds,
        string requestId = "",
        string error = "")
    {
        AuthUrl = authUrl;
        RedirectUri = redirectUri;
        TimeoutSeconds = timeoutSeconds;
        RequestId = requestId;
        Error = error;
    }
}
