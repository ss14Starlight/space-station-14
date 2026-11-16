using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._Daiquiri.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class ChessUiMessageEvent : CartridgeMessageEvent
{
    public readonly ChessUiAction Action;
    public readonly string Code;
    public readonly string State;
    public readonly string Error;
    public readonly string ErrorDescription;
    public readonly string RequestId;

    /// <summary>Requested time control in minutes for a new game.</summary>
    public readonly int TimeMinutes;

    /// <summary>Requested increment in seconds for a new game.</summary>
    public readonly int IncrementSeconds;

    /// <summary>Move in UCI notation, for example "e2e4".</summary>
    public readonly string Move;

    /// <summary>Opponent Lichess nickname for a direct challenge.</summary>
    public readonly string Opponent;

    /// <summary>Preferred colour for a direct challenge: "white", "black" or "random".</summary>
    public readonly string Color;

    /// <summary>Whether a direct challenge should be rated.</summary>
    public readonly bool Rated;

    /// <summary>Id of an incoming challenge to accept or decline.</summary>
    public readonly string ChallengeId;

    /// <summary>Lichess AI strength, from 1 (easiest) to 8 (hardest).</summary>
    public readonly int BotLevel;

    public ChessUiMessageEvent(
        ChessUiAction action,
        string code = "",
        string state = "",
        string error = "",
        string errorDescription = "",
        string requestId = "",
        int timeMinutes = 0,
        int incrementSeconds = 0,
        string move = "",
        string opponent = "",
        string color = "random",
        bool rated = false,
        string challengeId = "",
        int botLevel = 0)
    {
        Action = action;
        Code = code;
        State = state;
        Error = error;
        ErrorDescription = errorDescription;
        RequestId = requestId;
        TimeMinutes = timeMinutes;
        IncrementSeconds = incrementSeconds;
        Move = move;
        Opponent = opponent;
        Color = color;
        Rated = rated;
        ChallengeId = challengeId;
        BotLevel = botLevel;
    }
}

[Serializable, NetSerializable]
public enum ChessUiAction
{
    RequestLinkStatus,
    StartLink,
    CompleteLink,
    CancelLink,

    RequestGameState,
    StartSeek,
    StartChallenge,
    StartBot,
    CancelSeek,
    MakeMove,
    Resign,
    AbortGame,

    /// <summary>Offer a draw, or accept the opponent's pending draw offer.</summary>
    OfferDraw,

    /// <summary>Accept the pending incoming Lichess challenge.</summary>
    AcceptChallenge,

    /// <summary>Decline the pending incoming Lichess challenge.</summary>
    DeclineChallenge,

    /// <summary>Remove the Lichess link of the calling player.</summary>
    UnlinkAccount,
}
