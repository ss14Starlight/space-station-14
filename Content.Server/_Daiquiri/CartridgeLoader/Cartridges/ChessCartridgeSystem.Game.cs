using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared._Daiquiri.CartridgeLoader.Cartridges;
using Robust.Shared.Player;

namespace Content.Server._Daiquiri.CartridgeLoader.Cartridges;

/// <summary>
/// Lichess Board API bridge for chess.nt: finds opponents, plays the game and reports the
/// result to Discord. The access token stays on the server; the client only ever receives
/// the public board snapshot in <see cref="ChessGameState"/>.
/// </summary>
public sealed partial class ChessCartridgeSystem
{
    private const string BoardSeekEndpoint = "https://lichess.org/api/board/seek";
    private const string BoardGameEndpoint = "https://lichess.org/api/board/game";
    private const string EventStreamEndpoint = "https://lichess.org/api/stream/event";
    private const string ChallengeEndpoint = "https://lichess.org/api/challenge";
    private const string OpenChallengeEndpoint = "https://lichess.org/api/challenge/open";
    private const string GameUrlBase = "https://lichess.org/";

    /// <summary>
    /// Lichess rejects public board seeks faster than this with "Invalid time control",
    /// even though the API docs do not mention the limit. Bullet and blitz therefore go
    /// through an open challenge, which any Lichess player can accept.
    /// </summary>
    private const int MinSeekMinutes = 8;

    private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    // Streaming responses must not be killed by HttpClient.Timeout, so they use their own
    // client and rely on per-game cancellation instead.
    private static readonly HttpClient StreamingHttp = new() { Timeout = Timeout.InfiniteTimeSpan };

    // Games are owned by the PDA they were started from, not by an account. This is what
    // lets the player close the cartridge loader and another person at the same machine
    // reopen it and finish the game. Account access is never part of that: the Lichess
    // token stays in this record, is used server-side only, and the record is destroyed as
    // soon as the game ends.
    private readonly Dictionary<EntityUid, ActiveChessGame> _games = new();
    private readonly Dictionary<Guid, AccountStream> _accountStreams = new();
    private readonly object _gamesLock = new();

    private sealed class ActiveChessGame
    {
        public EntityUid Loader;
        public Guid UserId;
        public string Token = string.Empty;
        public string LichessUserId = string.Empty;
        public ChessPlayerResult Player;

        public string GameId = string.Empty;
        public string ChallengeId = string.Empty;
        public string Color = string.Empty;
        public string PreferredColor = "random";
        public bool Rated;
        public string OpponentName = string.Empty;
        public string PlayerName = string.Empty;
        public int Minutes = 10;
        public int Increment;
        public bool Unlimited;
        public string TimeControl => Unlimited ? "∞" : $"{Minutes}+{Increment}";
        public string InitialFen = StartFen;
        public string Board = new string('.', 64);
        public string Turn = "white";
        public string Castling = "KQkq";
        public string EnPassant = string.Empty;
        public string LastMove = string.Empty;
        public string Status = "searching";
        public string StatusText = "Ищем соперника на Lichess...";
        public string Winner = string.Empty;
        public int WhiteTimeMs;
        public int BlackTimeMs;

        /// <summary>Pending incoming challenge, empty when nothing is waiting.</summary>
        public string IncomingChallengeId = string.Empty;
        public string IncomingChallenger = string.Empty;
        public int IncomingMinutes;
        public int IncomingIncrement;
        public bool IncomingRated;
        public string IncomingColor = "random";

        public bool Searching;
        public bool ResultReported;

        /// <summary>True for a casual game against the local engine instead of Lichess.</summary>
        public bool LocalBot;
        public int BotLevel;

        /// <summary>
        /// Only random public Lichess opponents count toward the leaderboard. Bot games and
        /// games started from a friend challenge are excluded.
        /// </summary>
        public bool LeaderboardEligible;
        public ChessPosition? LocalPosition;
        public DateTime LocalTurnStartedUtc;
        public bool LocalBotThinking;

        /// <summary>
        /// Last player who asked for this board. Used so stream updates keep the same
        /// "delegated" wording for whoever currently has the PDA open.
        /// </summary>
        public Guid ViewerId;

        public readonly CancellationTokenSource Cts = new();
    }

    private void HandleGameAction(EntityUid loader, ICommonSession session, ChessUiMessageEvent message)
    {
        switch (message.Action)
        {
            case ChessUiAction.RequestGameState:
                SendGameState(loader, session.UserId.UserId);
                break;

            case ChessUiAction.StartSeek:
                StartSeek(loader, session, message.TimeMinutes, message.IncrementSeconds);
                break;

            case ChessUiAction.StartChallenge:
                StartChallenge(
                    loader,
                    session,
                    message.Opponent,
                    message.TimeMinutes,
                    message.IncrementSeconds,
                    message.Color,
                    message.Rated);
                break;

            case ChessUiAction.StartBot:
                StartBot(
                    loader,
                    session,
                    message.TimeMinutes,
                    message.IncrementSeconds,
                    message.Color,
                    message.BotLevel);
                break;

            case ChessUiAction.CancelSeek:
                CancelActiveGame(loader, loader);
                break;

            case ChessUiAction.MakeMove:
                _ = MakeMoveAsync(loader, message.Move);
                break;

            case ChessUiAction.Resign:
                _ = AbortGameAsync(loader, resign: true);
                break;

            case ChessUiAction.AbortGame:
                _ = AbortGameAsync(loader, resign: false);
                break;

            case ChessUiAction.OfferDraw:
                _ = OfferDrawAsync(loader);
                break;

            case ChessUiAction.AcceptChallenge:
                _ = AcceptChallengeAsync(loader, message.ChallengeId);
                break;

            case ChessUiAction.DeclineChallenge:
                _ = DeclineChallengeAsync(loader, message.ChallengeId);
                break;
        }
    }

    private void ShutdownGames()
    {
        lock (_gamesLock)
        {
            foreach (var game in _games.Values)
            {
                try
                {
                    game.Cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            _games.Clear();
        }
    }

    #region Starting a game

    private void StartSeek(EntityUid loader, ICommonSession session, int minutes, int increment)
    {
        if (!TryGetAccessToken(session.UserId.UserId, out var token))
        {
            SendGameState(loader, session.UserId.UserId, "Сначала привяжите аккаунт Lichess.");
            return;
        }

        if (!CanPlay(session.UserId.UserId, out var reason))
        {
            SendGameState(loader, session.UserId.UserId, reason);
            return;
        }

        CancelActiveGame(loader);

        // Lichess accepts every real-time time control in the public seek pool,
        // including bullet and blitz, so only sanity-clamp the requested values.
        minutes = Math.Clamp(minutes <= 0 ? 5 : minutes, 1, 60);
        increment = Math.Clamp(increment, 0, 60);

        var game = CreateGame(loader, session, token, minutes, increment);
        game.Searching = true;
        game.LeaderboardEligible = true;
        game.Status = "searching";
        game.StatusText = $"Ищем соперника: {game.TimeControl}. Это может занять время.";

        lock (_gamesLock)
            _games[loader] = game;

        // The account stream has to be open before the seek, otherwise a game that is
        // accepted immediately can be missed.
        EnsureAccountStream(loader, session.UserId.UserId, token);

        SendGameState(loader, session.UserId.UserId);
        _ = RunSeekAsync(game);
    }

    private void StartChallenge(
        EntityUid loader,
        ICommonSession session,
        string opponent,
        int minutes,
        int increment,
        string color,
        bool rated)
    {
        if (!TryGetAccessToken(session.UserId.UserId, out var token))
        {
            SendGameState(loader, session.UserId.UserId, "Сначала привяжите аккаунт Lichess.");
            return;
        }

        if (!CanUseAccount(session.UserId.UserId, out var reason))
        {
            SendGameState(loader, session.UserId.UserId, reason);
            return;
        }

        opponent = (opponent ?? string.Empty).Trim();
        if (opponent.Length == 0)
        {
            SendGameState(loader, session.UserId.UserId, "Укажите ник соперника на Lichess.");
            return;
        }

        CancelActiveGame(loader);

        minutes = Math.Clamp(minutes <= 0 ? 5 : minutes, 1, 60);
        increment = Math.Clamp(increment, 0, 60);

        var game = CreateGame(loader, session, token, minutes, increment);
        game.Searching = true;
        game.Status = "created";
        game.PreferredColor = NormalizeChallengeColor(color);
        game.Rated = rated;
        game.StatusText = $"Вызов отправлен игроку {opponent}. Ждём принятия...";

        lock (_gamesLock)
            _games[loader] = game;

        EnsureAccountStream(loader, session.UserId.UserId, token);

        SendGameState(loader, session.UserId.UserId);
        _ = RunChallengeAsync(game, opponent);
    }

    /// <summary>
    /// Starts an unrated casual game against the local chess engine. Lichess AI games
    /// cannot be played through the Board API ("This game cannot be played with the Board
    /// API"), so the bot runs server-side and never touches the player's Lichess account.
    /// </summary>
    private void StartBot(
        EntityUid loader,
        ICommonSession session,
        int minutes,
        int increment,
        string color,
        int level)
    {
        CancelActiveGame(loader);

        minutes = Math.Clamp(minutes <= 0 ? 10 : minutes, 1, 60);
        increment = Math.Clamp(increment, 0, 60);
        level = Math.Clamp(level, 1, 8);

        var game = CreateGame(loader, session, string.Empty, minutes, increment);
        game.LocalBot = true;
        game.BotLevel = level;
        game.GameId = "local-" + Guid.NewGuid().ToString("N");
        game.Searching = false;
        game.Status = "started";
        game.PreferredColor = NormalizeChallengeColor(color);
        game.Color = string.Equals(game.PreferredColor, "random", StringComparison.OrdinalIgnoreCase)
            ? (Random.Shared.Next(2) == 0 ? "white" : "black")
            : game.PreferredColor;
        game.Rated = false;
        game.OpponentName = $"Бот (уровень {level})";
        game.PlayerName = GetLinkInfo(session.UserId.UserId)?.Username ?? session.Name;
        game.LocalPosition = ChessPosition.FromFen(StartFen);
        game.LocalTurnStartedUtc = DateTime.UtcNow;
        game.WhiteTimeMs = minutes * 60_000;
        game.BlackTimeMs = minutes * 60_000;
        game.StatusText = DescribeStatus(game, "started", string.Empty);

        lock (_gamesLock)
            _games[loader] = game;

        SendGameState(loader, session.UserId.UserId);

        // If the player chose black, the engine makes the first move.
        if (string.Equals(game.Color, "black", StringComparison.OrdinalIgnoreCase))
            ScheduleLocalBotMove(game);
    }

    private ActiveChessGame CreateGame(
        EntityUid loader,
        ICommonSession session,
        string token,
        int minutes,
        int increment)
    {
        var info = GetLinkInfo(session.UserId.UserId);
        var board = new char[64];
        Array.Fill(board, '.');
        FenToBoard(StartFen, board);

        return new ActiveChessGame
        {
            Loader = loader,
            UserId = session.UserId.UserId,
            Token = token,
            LichessUserId = info?.LichessUserId ?? string.Empty,
            Player = GetPlayerResult(session),
            PlayerName = info?.Username ?? string.Empty,
            Minutes = minutes,
            Increment = increment,
            WhiteTimeMs = minutes * 60_000,
            BlackTimeMs = minutes * 60_000,
            InitialFen = StartFen,
            Board = new string(board),
            Turn = "white",
        };
    }

    #endregion

    #region Lichess streaming

    /// <summary>
    /// Lichess only accepts rapid and classical time controls in the public seek pool, so
    /// bullet and blitz are routed through an open challenge. Either way the game id comes
    /// from the global event stream: the seek response itself carries no data at all.
    /// </summary>
    private async Task RunSeekAsync(ActiveChessGame game)
    {
        if (game.Minutes < MinSeekMinutes)
        {
            await RunOpenChallengeAsync(game);
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BoardSeekEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", game.Token);
            // Lichess now rejects the old x-ndjson value on this route with HTTP 406.
            // The response is still a stream, but content negotiation requires JSON here.
            request.Headers.Accept.ParseAdd("application/json");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["rated"] = "false",
                ["time"] = game.Minutes.ToString(CultureInfo.InvariantCulture),
                ["increment"] = game.Increment.ToString(CultureInfo.InvariantCulture),
                ["color"] = "random",
            });

            using var response = await StreamingHttp.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                game.Cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(game.Cts.Token);
                FailGame(
                    game,
                    DescribeLichessRejection("поиск", response, body),
                    "Lichess отклонил поиск.",
                    response,
                    body);
                return;
            }

            // Keep the connection open to keep the seek alive. Lichess closes it when the
            // seek is accepted or expires, and never sends any data on it.
            await DrainAsync(response, game.Cts.Token);

            if (string.IsNullOrEmpty(game.GameId) && !game.Cts.IsCancellationRequested)
            {
                // The seek can close a moment before gameStart reaches the event stream.
                for (var i = 0; i < 50 && string.IsNullOrEmpty(game.GameId) && !game.Cts.IsCancellationRequested; i++)
                    await Task.Delay(100, game.Cts.Token);
            }

            if (string.IsNullOrEmpty(game.GameId) && !game.Cts.IsCancellationRequested)
                FailGame(game, "Соперник не найден. Попробуйте ещё раз.", "Соперник не найден.");
        }
        catch (OperationCanceledException)
        {
            // Player cancelled or the game was replaced.
        }
        catch (Exception e)
        {
            Sawmill.Error($"Chess seek failed: {e}");
            FailGame(game, "Не удалось связаться с Lichess. Попробуйте ещё раз.", "Ошибка связи с Lichess.");
        }
    }

    /// <summary>
    /// Creates a public challenge that any Lichess player can accept. This is the only way
    /// to play bullet and blitz without naming an opponent, because the board seek pool
    /// refuses anything faster than eight minutes.
    /// </summary>
    private async Task RunOpenChallengeAsync(ActiveChessGame game)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, OpenChallengeEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", game.Token);
            request.Headers.Accept.ParseAdd("application/json");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["rated"] = "false",
                ["clock.limit"] = (game.Minutes * 60).ToString(CultureInfo.InvariantCulture),
                ["clock.increment"] = game.Increment.ToString(CultureInfo.InvariantCulture),
            });

            using var response = await StreamingHttp.SendAsync(request, game.Cts.Token);
            var body = await response.Content.ReadAsStringAsync(game.Cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                FailGame(
                    game,
                    DescribeLichessRejection("поиск", response, body),
                    "Lichess отклонил поиск.",
                    response,
                    body);
                return;
            }

            using (var doc = JsonDocument.Parse(body))
            {
                if (doc.RootElement.TryGetProperty("id", out var idProp) &&
                    idProp.GetString() is { Length: > 0 } challengeId)
                {
                    game.ChallengeId = challengeId;
                }
            }

            game.Status = "created";
            game.StatusText = $"Открытый вызов {game.TimeControl} создан. Ждём соперника...";
            SendGameStateOnMainThread(game, string.Empty);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Sawmill.Error($"Chess open challenge failed: {e}");
            FailGame(game, "Не удалось связаться с Lichess. Попробуйте ещё раз.", "Ошибка связи с Lichess.");
        }
    }

    private static string NormalizeChallengeColor(string? color)
    {
        return color?.Trim().ToLowerInvariant() switch
        {
            "white" => "white",
            "black" => "black",
            _ => "random",
        };
    }

    private async Task RunChallengeAsync(ActiveChessGame game, string opponent)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{ChallengeEndpoint}/{Uri.EscapeDataString(opponent)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", game.Token);
            request.Headers.Accept.ParseAdd("application/json");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["rated"] = game.Rated ? "true" : "false",
                ["time"] = game.Minutes.ToString(CultureInfo.InvariantCulture),
                ["increment"] = game.Increment.ToString(CultureInfo.InvariantCulture),
                ["color"] = game.PreferredColor,
            });

            using var response = await StreamingHttp.SendAsync(request, game.Cts.Token);
            var body = await response.Content.ReadAsStringAsync(game.Cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                FailGame(
                    game,
                    DescribeLichessRejection("вызов", response, body),
                    "Lichess отклонил вызов.",
                    response,
                    body);
                return;
            }

            using (var doc = JsonDocument.Parse(body))
            {
                if (doc.RootElement.TryGetProperty("id", out var idProp) &&
                    idProp.GetString() is { Length: > 0 } challengeId)
                {
                    game.ChallengeId = challengeId;
                }
            }

            game.Searching = true;
            game.Status = "created";
            game.StatusText = $"Вызов отправлен игроку {opponent}. Ждём принятия...";
            SendGameStateOnMainThread(game, string.Empty);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Sawmill.Error($"Chess challenge failed: {e}");
            FailGame(game, "Не удалось связаться с Lichess. Попробуйте ещё раз.", "Ошибка связи с Lichess.");
        }
    }

    /// <summary>
    /// A long lived account-wide event stream. It is the only reliable source of the game
    /// id (a seek or challenge only produces "gameStart" once someone accepts it) and it
    /// is also how incoming friend challenges reach the PDA.
    /// </summary>
    private sealed class AccountStream
    {
        public Guid UserId;
        public EntityUid Loader;
        public string Token = string.Empty;
        public readonly CancellationTokenSource Cts = new();
    }

    private void EnsureAccountStream(EntityUid loader, Guid userId, string token)
    {
        AccountStream? previous = null;
        AccountStream stream;

        lock (_gamesLock)
        {
            if (_accountStreams.TryGetValue(userId, out var existing))
            {
                existing.Loader = loader;
                if (string.Equals(existing.Token, token, StringComparison.Ordinal))
                    return;

                // Re-linked with a different token: the old stream belongs to a token
                // Lichess may already have revoked.
                _accountStreams.Remove(userId);
                previous = existing;
            }

            stream = new AccountStream
            {
                UserId = userId,
                Loader = loader,
                Token = token,
            };

            _accountStreams[userId] = stream;
        }

        if (previous != null)
        {
            try
            {
                previous.Cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _ = RunAccountStreamAsync(stream);
    }

    private void StopAccountStream(Guid userId)
    {
        AccountStream? stream;
        lock (_gamesLock)
        {
            if (!_accountStreams.TryGetValue(userId, out stream))
                return;

            _accountStreams.Remove(userId);
        }

        try
        {
            stream.Cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void StopAccountStreamsForLoader(EntityUid loader)
    {
        List<Guid> users;
        lock (_gamesLock)
        {
            users = _accountStreams
                .Where(pair => pair.Value.Loader == loader)
                .Select(pair => pair.Key)
                .ToList();
        }

        foreach (var userId in users)
            StopAccountStream(userId);
    }

    private void ShutdownAccountStreams()
    {
        List<AccountStream> streams;
        lock (_gamesLock)
        {
            streams = _accountStreams.Values.ToList();
            _accountStreams.Clear();
        }

        foreach (var stream in streams)
        {
            try
            {
                stream.Cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private async Task RunAccountStreamAsync(AccountStream stream)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, EventStreamEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", stream.Token);
            request.Headers.Accept.ParseAdd("application/x-ndjson");

            using var response = await StreamingHttp.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                stream.Cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(stream.Cts.Token);
                Sawmill.Warning(
                    $"Chess: Lichess event stream rejected ({(int) response.StatusCode}). {TrimForClient(body)}");

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    MarkAccessBroken(
                        stream.UserId,
                        "Lichess больше не принимает токен. Привяжите аккаунт заново.");

                    if (Exists(stream.Loader))
                        SendLinkStatusOnMainThread(stream.Loader, stream.UserId);
                }

                return;
            }

            await using var content = await response.Content.ReadAsStreamAsync(stream.Cts.Token);
            using var reader = new StreamReader(content, Encoding.UTF8);

            while (!stream.Cts.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(stream.Cts.Token);
                if (line == null)
                    break;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    HandleAccountEvent(stream, line);
                }
                catch (JsonException e)
                {
                    Sawmill.Warning($"Chess: bad Lichess event '{TrimForClient(line)}': {e.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Sawmill.Error($"Chess event stream failed: {e}");
        }
    }

    /// <summary>
    /// Routes account events to the PDA that owns them. A game is only claimed while that
    /// PDA is actually waiting for its own opponent, so games started on lichess.org are
    /// not hijacked by the cartridge.
    /// </summary>
    private void HandleAccountEvent(AccountStream stream, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProp))
            return;

        switch (typeProp.GetString())
        {
            case "gameStart":
                HandleGameStart(stream, root);
                break;

            case "challenge":
                HandleIncomingChallenge(stream, root);
                break;

            case "challengeCanceled":
            case "challengeDeclined":
                HandleChallengeGone(stream, root);
                break;
        }
    }

    private void HandleGameStart(AccountStream stream, JsonElement root)
    {
        if (!root.TryGetProperty("game", out var gameProp) ||
            !gameProp.TryGetProperty("id", out var idProp) ||
            idProp.GetString() is not { Length: > 0 } gameId)
        {
            return;
        }

        var game = FindSearchingGame(stream.UserId);
        if (game == null || !string.IsNullOrEmpty(game.GameId) || !game.Searching)
            return;

        game.GameId = gameId;
        game.Searching = false;
        game.IncomingChallengeId = string.Empty;
        // Stay in a non-controllable state until gameFull arrives with the real colours.
        game.Status = "starting";
        game.StatusText = "Партия началась. Загружаем позицию...";
        SendGameStateOnMainThread(game, string.Empty);

        _ = RunGameStreamAsync(game);
    }

    private ActiveChessGame? FindSearchingGame(Guid userId)
    {
        lock (_gamesLock)
        {
            foreach (var game in _games.Values)
            {
                if (game.UserId == userId && game.Searching)
                    return game;
            }
        }

        return null;
    }

    private ActiveChessGame? FindGameForUser(Guid userId)
    {
        lock (_gamesLock)
        {
            foreach (var game in _games.Values)
            {
                if (game.UserId == userId)
                    return game;
            }
        }

        return null;
    }

    private void HandleIncomingChallenge(AccountStream stream, JsonElement root)
    {
        if (!root.TryGetProperty("challenge", out var challenge))
            return;

        if (!challenge.TryGetProperty("id", out var idProp) ||
            idProp.GetString() is not { Length: > 0 } challengeId)
        {
            return;
        }

        // A running game cannot accept another challenge anyway, so do not offer one.
        var game = FindGameForUser(stream.UserId);
        if (game != null && (!string.IsNullOrEmpty(game.GameId) || game.Searching))
            return;

        var challenger = string.Empty;
        if (challenge.TryGetProperty("challenger", out var challengerProp))
        {
            if (challengerProp.TryGetProperty("name", out var nameProp))
                challenger = nameProp.GetString() ?? string.Empty;
            else if (challengerProp.TryGetProperty("id", out var challengerId))
                challenger = challengerId.GetString() ?? string.Empty;
        }

        var minutes = 0;
        var increment = 0;
        if (challenge.TryGetProperty("timeControl", out var timeControl))
        {
            minutes = ReadInt(timeControl, "limit", 0) / 60;
            increment = ReadInt(timeControl, "increment", 0);
        }

        var rated = challenge.TryGetProperty("rated", out var ratedProp) && ratedProp.ValueKind == JsonValueKind.True;
        var color = challenge.TryGetProperty("finalColor", out var colorProp)
            ? colorProp.GetString() ?? "random"
            : "random";

        if (game == null)
        {
            if (!TryGetAccessToken(stream.UserId, out var token))
                return;

            var info = GetLinkInfo(stream.UserId);
            var board = new char[64];
            Array.Fill(board, '.');
            FenToBoard(StartFen, board);

            game = new ActiveChessGame
            {
                Loader = stream.Loader,
                UserId = stream.UserId,
                Token = token,
                LichessUserId = info?.LichessUserId ?? string.Empty,
                Player = GetPlayerResultForUser(stream.UserId),
                PlayerName = info?.Username ?? string.Empty,
                Minutes = Math.Clamp(minutes <= 0 ? 5 : minutes, 1, 60),
                Increment = Math.Clamp(increment, 0, 60),
                WhiteTimeMs = Math.Clamp(minutes <= 0 ? 5 : minutes, 1, 60) * 60_000,
                BlackTimeMs = Math.Clamp(minutes <= 0 ? 5 : minutes, 1, 60) * 60_000,
                InitialFen = StartFen,
                Board = new string(board),
                Status = "idle",
            };

            lock (_gamesLock)
                _games[game.Loader] = game;
        }

        game.IncomingChallengeId = challengeId;
        game.IncomingChallenger = challenger;
        game.IncomingMinutes = Math.Clamp(minutes <= 0 ? 5 : minutes, 1, 60);
        game.IncomingIncrement = Math.Clamp(increment, 0, 60);
        game.IncomingRated = rated;
        game.IncomingColor = NormalizeChallengeColor(color);

        SendGameStateOnMainThread(game, string.Empty);
    }

    private void HandleChallengeGone(AccountStream stream, JsonElement root)
    {
        var id = string.Empty;
        if (root.TryGetProperty("challenge", out var challenge))
        {
            if (challenge.TryGetProperty("id", out var idProp))
                id = idProp.GetString() ?? string.Empty;
        }
        else if (root.TryGetProperty("challengeId", out var challengeIdProp))
        {
            id = challengeIdProp.GetString() ?? string.Empty;
        }

        var game = FindGameForUser(stream.UserId);
        if (game == null)
            return;

        if (id.Length > 0 && !string.Equals(game.IncomingChallengeId, id, StringComparison.Ordinal))
            return;

        game.IncomingChallengeId = string.Empty;
        game.IncomingChallenger = string.Empty;
        SendGameStateOnMainThread(game, string.Empty);
    }

    private async Task RunGameStreamAsync(ActiveChessGame game)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{BoardGameEndpoint}/stream/{Uri.EscapeDataString(game.GameId)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", game.Token);
            request.Headers.Accept.ParseAdd("application/x-ndjson");

            using var response = await StreamingHttp.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                game.Cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(game.Cts.Token);
                FailGame(
                    game,
                    $"Не удалось открыть партию Lichess ({(int) response.StatusCode}). {TrimForClient(body)}",
                    "Не удалось открыть партию Lichess.");
                return;
            }

            await ReadGameStreamAsync(game, response, game.Cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Sawmill.Error($"Chess game stream failed: {e}");
            FailGame(game, "Не удалось связаться с Lichess. Попробуйте ещё раз.", "Ошибка связи с Lichess.");
        }
    }

    private static async Task DrainAsync(HttpResponseMessage response, CancellationToken cancel)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancel);
        var buffer = new byte[512];

        while (!cancel.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buffer, cancel);
            if (read <= 0)
                break;
        }
    }

    /// <summary>
    /// Cancels a challenge that is still waiting for an opponent, so it does not linger on
    /// the account after the player closes the cartridge loader.
    /// </summary>
    private async Task CancelChallengeAsync(ActiveChessGame game)
    {
        if (string.IsNullOrEmpty(game.ChallengeId) || !string.IsNullOrEmpty(game.GameId))
            return;

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{ChallengeEndpoint}/{Uri.EscapeDataString(game.ChallengeId)}/cancel");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", game.Token);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await StreamingHttp.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                Sawmill.Warning($"Chess: Lichess rejected challenge cancel: {(int) response.StatusCode} {body}");
            }
        }
        catch (Exception e)
        {
            Sawmill.Warning($"Chess: failed to cancel challenge: {e.Message}");
        }
    }

    /// <summary>
    /// Accepts a challenge another Lichess player sent to this account. The resulting game
    /// id arrives on the account event stream; the response is only used as a fallback.
    /// </summary>
    private async Task AcceptChallengeAsync(EntityUid loader, string challengeId)
    {
        var game = GetGame(loader);
        if (game == null || string.IsNullOrEmpty(challengeId))
            return;

        if (!string.Equals(game.IncomingChallengeId, challengeId, StringComparison.Ordinal))
            return;

        if (!CanUseAccount(game.UserId, out var reason))
        {
            SendGameStateOnMainThread(game, reason);
            return;
        }

        game.Minutes = game.IncomingMinutes;
        game.Increment = game.IncomingIncrement;
        game.PreferredColor = game.IncomingColor;
        game.Rated = game.IncomingRated;
        game.OpponentName = game.IncomingChallenger;
        game.WhiteTimeMs = game.Minutes * 60_000;
        game.BlackTimeMs = game.Minutes * 60_000;
        game.IncomingChallengeId = string.Empty;
        game.Searching = true;
        game.Status = "starting";
        game.StatusText = $"Принимаем вызов от {game.OpponentName}...";
        SendGameStateOnMainThread(game, string.Empty);

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{ChallengeEndpoint}/{Uri.EscapeDataString(challengeId)}/accept");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", game.Token);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await StreamingHttp.SendAsync(request, game.Cts.Token);
            var body = await response.Content.ReadAsStringAsync(game.Cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                FailGame(
                    game,
                    DescribeLichessRejection("вызов", response, body),
                    "Lichess отклонил принятие вызова.",
                    response,
                    body);
                return;
            }

            // The gameStart event normally wins this race. Only use the response id when
            // the event has not arrived yet.
            if (!string.IsNullOrEmpty(game.GameId))
                return;

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("id", out var idProp) &&
                    idProp.GetString() is { Length: > 0 } gameId)
                {
                    game.GameId = gameId;
                    game.Searching = false;
                    game.Status = "starting";
                    game.StatusText = "Партия началась. Загружаем позицию...";
                    SendGameStateOnMainThread(game, string.Empty);
                    _ = RunGameStreamAsync(game);
                }
            }
            catch (JsonException)
            {
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Sawmill.Error($"Chess accept challenge failed: {e}");
            FailGame(game, "Не удалось принять вызов. Попробуйте ещё раз.", "Ошибка связи с Lichess.");
        }
    }

    private async Task DeclineChallengeAsync(EntityUid loader, string challengeId)
    {
        var game = GetGame(loader);
        if (game == null)
            return;

        if (!string.IsNullOrEmpty(challengeId) &&
            !string.Equals(game.IncomingChallengeId, challengeId, StringComparison.Ordinal))
        {
            return;
        }

        var id = game.IncomingChallengeId;
        game.IncomingChallengeId = string.Empty;
        game.IncomingChallenger = string.Empty;
        SendGameStateOnMainThread(game, string.Empty);

        if (id.Length == 0)
            return;

        await SendChallengeDeclineAsync(game, id);
    }

    private void DeclineIncomingChallenge(ActiveChessGame game)
    {
        var id = game.IncomingChallengeId;
        game.IncomingChallengeId = string.Empty;
        game.IncomingChallenger = string.Empty;

        if (id.Length > 0)
            _ = SendChallengeDeclineAsync(game, id);
    }

    private async Task SendChallengeDeclineAsync(ActiveChessGame game, string challengeId)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{ChallengeEndpoint}/{Uri.EscapeDataString(challengeId)}/decline");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", game.Token);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await StreamingHttp.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                Sawmill.Warning(
                    $"Chess: Lichess rejected challenge decline: {(int) response.StatusCode} {body}");
            }
        }
        catch (Exception e)
        {
            Sawmill.Warning($"Chess: failed to decline challenge: {e.Message}");
        }
    }

    private static string DescribeLichessRejection(string action, HttpResponseMessage response, string body)
    {
        if (body.Contains("challenge:write", StringComparison.OrdinalIgnoreCase))
        {
            return "Lichess требует доступ challenge:write. " +
                   "Выйдите из Lichess и привяжите аккаунт заново.";
        }

        if (body.Contains("Banned from playing", StringComparison.OrdinalIgnoreCase))
        {
            var match = BanMinutesRegex.Match(body);
            var minutes = match.Success ? match.Groups[1].Value : "?";
            return $"Lichess временно запретил игру на этом аккаунте (осталось ≈{minutes} мин). " +
                   "Это ограничение Lichess, а не chess.nt.";
        }

        return $"Lichess отклонил {action} ({(int) response.StatusCode}). {TrimForClient(body)}";
    }

    private async Task ReadGameStreamAsync(ActiveChessGame game, HttpResponseMessage response, CancellationToken cancel)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancel);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!cancel.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancel);
            if (line == null)
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                ApplyLichessEvent(game, line);
            }
            catch (JsonException e)
            {
                Sawmill.Warning($"Chess: bad Lichess event '{TrimForClient(line)}': {e.Message}");
                continue;
            }

            if (IsFinishedStatus(game.Status))
            {
                ReportFinishedGame(game);
                break;
            }
        }
    }

    private void ApplyLichessEvent(ActiveChessGame game, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProp))
            return;

        switch (typeProp.GetString())
        {
            case "gameFull":
                ApplyGameFull(game, root);
                break;

            case "gameState":
                ApplyGameState(game, root);
                break;
        }
    }

    private void ApplyGameFull(ActiveChessGame game, JsonElement root)
    {
        if (root.TryGetProperty("id", out var idProp) && idProp.GetString() is { Length: > 0 } id)
            game.GameId = id;

        var whiteId = ReadPlayerId(root, "white");
        var blackId = ReadPlayerId(root, "black");
        var whiteName = ReadPlayerName(root, "white");
        var blackName = ReadPlayerName(root, "black");

        var playingWhite = !string.IsNullOrEmpty(game.LichessUserId)
            ? string.Equals(whiteId, game.LichessUserId, StringComparison.OrdinalIgnoreCase)
            : string.Equals(whiteName, game.Player.LichessUsername, StringComparison.OrdinalIgnoreCase);

        game.Color = playingWhite ? "white" : "black";
        game.OpponentName = playingWhite ? blackName : whiteName;
        if (string.IsNullOrEmpty(game.OpponentName))
            game.OpponentName = playingWhite ? blackId : whiteId;

        var ownName = playingWhite ? whiteName : blackName;
        if (!string.IsNullOrEmpty(ownName))
            game.PlayerName = ownName;

        game.Searching = false;

        // gameFull carries the initial position and the real time control. The board API
        // sends "startpos" instead of a FEN for standard games.
        if (root.TryGetProperty("initialFen", out var initialFenProp) &&
            initialFenProp.GetString() is { Length: > 0 } initialFen)
        {
            game.InitialFen = string.Equals(initialFen, "startpos", StringComparison.OrdinalIgnoreCase)
                ? StartFen
                : initialFen;
        }

        if (root.TryGetProperty("clock", out var clock) && clock.ValueKind == JsonValueKind.Object)
        {
            var initial = ReadInt(clock, "initial", game.Minutes * 60_000);
            var increment = ReadInt(clock, "increment", game.Increment * 1000);
            game.Unlimited = false;
            game.Minutes = Math.Clamp(initial / 60_000, 1, 60);
            game.Increment = Math.Clamp(increment / 1000, 0, 60);
            game.WhiteTimeMs = Math.Max(0, initial);
            game.BlackTimeMs = Math.Max(0, initial);
        }
        else
        {
            // Correspondence / unlimited games have no clock object at all.
            game.Unlimited = true;
        }

        if (root.TryGetProperty("state", out var state))
        {
            ApplyGameState(game, state);
            return;
        }

        SendGameStateOnMainThread(game, string.Empty);
    }

    private void ApplyGameState(ActiveChessGame game, JsonElement state)
    {
        var moves = state.TryGetProperty("moves", out var movesProp) ? movesProp.GetString() ?? string.Empty : string.Empty;
        var status = state.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "started" : "started";
        var winner = state.TryGetProperty("winner", out var winnerProp) ? winnerProp.GetString() ?? string.Empty : string.Empty;

        if (!game.Unlimited)
        {
            var white = ReadInt(state, "wtime", game.WhiteTimeMs);
            var black = ReadInt(state, "btime", game.BlackTimeMs);

            // Lichess reports an unlimited game as int.MaxValue even when gameFull had no
            // clock object. Never let that reach the PDA as a countdown.
            if (IsUnlimitedClock(white) || IsUnlimitedClock(black))
            {
                game.Unlimited = true;
            }
            else
            {
                game.WhiteTimeMs = Math.Max(0, white);
                game.BlackTimeMs = Math.Max(0, black);
            }
        }

        RebuildBoard(game, moves);

        game.Status = status;
        game.Winner = winner;
        game.StatusText = DescribeStatus(game, status, winner);
        SendGameStateOnMainThread(game, string.Empty);
    }

    private static bool IsUnlimitedClock(int milliseconds)
    {
        return milliseconds >= 1_000_000_000;
    }

    private static int ReadInt(JsonElement element, string name, int fallback)
    {
        return element.TryGetProperty(name, out var prop) && prop.TryGetInt32(out var value)
            ? value
            : fallback;
    }

    private static string ReadPlayerId(JsonElement root, string side)
    {
        if (!root.TryGetProperty(side, out var player))
            return string.Empty;

        return player.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty;
    }

    private static string ReadPlayerName(JsonElement root, string side)
    {
        if (!root.TryGetProperty(side, out var player))
            return string.Empty;

        if (player.TryGetProperty("name", out var name) && name.GetString() is { Length: > 0 } value)
            return value;

        return player.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty;
    }

    #endregion

    #region Board state

    private static void RebuildBoard(ActiveChessGame game, string moves)
    {
        var position = ChessPosition.FromFen(game.InitialFen);
        var lastMove = string.Empty;

        foreach (var move in moves.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!position.TryApplyUci(move))
                continue;

            lastMove = move;
        }

        game.Board = position.BoardString;
        game.Turn = position.WhiteToMove ? "white" : "black";
        game.Castling = position.CastlingString();
        game.EnPassant = position.EnPassantSquare();
        game.LastMove = lastMove;
    }

    private static void FenToBoard(string fen, char[] board)
    {
        Array.Fill(board, '.');

        var placement = fen.Split(' ')[0];
        var index = 0;
        foreach (var symbol in placement)
        {
            if (symbol == '/')
                continue;

            if (char.IsDigit(symbol))
            {
                index += symbol - '0';
                continue;
            }

            if (index is >= 0 and < 64)
                board[index] = symbol;

            index++;
        }
    }

    #endregion

    #region Moves, aborting and results

    private async Task MakeMoveAsync(EntityUid loader, string move)
    {
        var game = GetGame(loader);
        if (game == null || string.IsNullOrEmpty(game.GameId))
            return;

        if (!string.Equals(game.Status, "started", StringComparison.OrdinalIgnoreCase))
            return;

        move = (move ?? string.Empty).Trim();
        if (move.Length is < 4 or > 5)
            return;

        if (game.LocalBot)
        {
            MakeLocalPlayerMove(game, move);
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{BoardGameEndpoint}/{Uri.EscapeDataString(game.GameId)}/move/{Uri.EscapeDataString(move)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", game.Token);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await StreamingHttp.SendAsync(request, game.Cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(game.Cts.Token);
                Sawmill.Warning($"Chess: Lichess rejected move {move}: {(int) response.StatusCode} {body}");
                SendGameStateOnMainThread(game, $"Lichess не принял ход {move}.");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Sawmill.Error($"Chess: failed to send move {move}: {e}");
            SendGameStateOnMainThread(game, "Не удалось отправить ход в Lichess.");
        }
    }

    private void MakeLocalPlayerMove(ActiveChessGame game, string uci)
    {
        if (game.LocalPosition == null || !TryParseUciMove(uci, out var requested))
            return;

        foreach (var move in game.LocalPosition.GetLegalMoves())
        {
            if (move.From != requested.From || move.To != requested.To)
                continue;

            // A four-character UCI move on the promotion rank means queen by convention.
            if (requested.Promotion != '\0' && move.Promotion != requested.Promotion)
                continue;
            if (requested.Promotion == '\0' && move.Promotion != '\0' && move.Promotion != 'q')
                continue;

            ApplyLocalMove(game, move, fromBot: false);
            return;
        }
    }

    private static bool TryParseUciMove(string uci, out ChessMove move)
    {
        move = default;
        if (uci.Length is < 4 or > 5)
            return false;

        var from = ChessPosition.ParseSquare(uci[..2]);
        var to = ChessPosition.ParseSquare(uci.Substring(2, 2));
        if (from < 0 || to < 0)
            return false;

        var promotion = uci.Length == 5 ? char.ToLowerInvariant(uci[4]) : '\0';
        if (promotion != '\0' && promotion is not ('q' or 'r' or 'b' or 'n'))
            return false;

        move = new ChessMove(from, to, promotion);
        return true;
    }

    private void ApplyLocalMove(ActiveChessGame game, ChessMove move, bool fromBot)
    {
        var position = game.LocalPosition;
        if (position == null || !string.Equals(game.Status, "started", StringComparison.OrdinalIgnoreCase))
            return;

        var moverWhite = position.WhiteToMove;
        var moverColor = moverWhite ? "white" : "black";
        if (fromBot == string.Equals(game.Color, moverColor, StringComparison.OrdinalIgnoreCase))
            return;

        var legal = position.GetLegalMoves();
        var legalMove = legal.FirstOrDefault(candidate =>
            candidate.From == move.From &&
            candidate.To == move.To &&
            candidate.Promotion == move.Promotion);
        if (legalMove == default)
            return;

        var now = DateTime.UtcNow;
        var elapsed = (int) Math.Clamp((now - game.LocalTurnStartedUtc).TotalMilliseconds, 0, int.MaxValue);
        var remaining = (moverWhite ? game.WhiteTimeMs : game.BlackTimeMs) - (long) elapsed;
        if (remaining <= 0)
        {
            FinishLocalTimeout(game, moverWhite);
            return;
        }

        remaining = Math.Min(int.MaxValue, remaining + game.Increment * 1000L);
        if (moverWhite)
            game.WhiteTimeMs = (int) remaining;
        else
            game.BlackTimeMs = (int) remaining;

        position.Apply(legalMove);
        game.LocalTurnStartedUtc = now;
        game.Board = position.BoardString;
        game.Turn = position.WhiteToMove ? "white" : "black";
        game.Castling = position.CastlingString();
        game.EnPassant = position.EnPassantSquare();
        game.LastMove = legalMove.ToUci();

        Sawmill.Info($"Chess local move {legalMove.ToUci()} fromBot={fromBot} " +
                     $"playerColor={game.Color} mover={moverColor} nextTurn={game.Turn}");

        if (position.GetLegalMoves().Count == 0)
        {
            if (position.IsKingAttacked(position.WhiteToMove))
            {
                game.Status = "mate";
                game.Winner = position.WhiteToMove ? "black" : "white";
            }
            else
            {
                game.Status = "stalemate";
                game.Winner = string.Empty;
            }
        }
        else
        {
            game.Status = "started";
            game.Winner = string.Empty;
        }

        game.StatusText = DescribeStatus(game, game.Status, game.Winner);
        SendGameStateOnMainThread(game, string.Empty);

        if (IsFinishedStatus(game.Status))
        {
            ReportFinishedGame(game);
            return;
        }

        var botColor = string.Equals(game.Color, "white", StringComparison.OrdinalIgnoreCase) ? "black" : "white";
        if (string.Equals(game.Turn, botColor, StringComparison.OrdinalIgnoreCase))
            ScheduleLocalBotMove(game);
    }

    private void ScheduleLocalBotMove(ActiveChessGame game)
    {
        if (!game.LocalBot || game.LocalBotThinking || game.Cts.IsCancellationRequested)
            return;

        var botColor = string.Equals(game.Color, "white", StringComparison.OrdinalIgnoreCase) ? "black" : "white";
        if (!string.Equals(game.Turn, botColor, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(game.Status, "started", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var position = game.LocalPosition?.Clone();
        if (position == null)
            return;

        game.LocalBotThinking = true;
        var level = game.BotLevel;
        _ = Task.Run(async () =>
        {
            ChessMove? chosen = null;
            try
            {
                chosen = ChessBotEngine.ChooseMove(position, level);

                // Keep the bot's answer separated from the player's move so the player
                // can see their own move land before the reply arrives. Keep this short;
                // a long artificial delay makes the whole game feel sluggish.
                if (chosen != null)
                    await Task.Delay(TimeSpan.FromMilliseconds(120), game.Cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Sawmill.Error($"Chess: local bot failed: {e}");
            }

            _taskManager.RunOnMainThread(() =>
            {
                game.LocalBotThinking = false;
                if (!IsCurrentGame(game) || game.Cts.IsCancellationRequested)
                    return;

                if (chosen is not { } move)
                {
                    FailGame(game, "Бот не смог выбрать ход.", "Ошибка бота.");
                    return;
                }

                Sawmill.Info($"Chess local bot move {move.ToUci()} level={level} " +
                             $"color={game.Color} turn={game.Turn}");
                ApplyLocalMove(game, move, fromBot: true);
            });
        });
    }

    private void FinishLocalTimeout(ActiveChessGame game, bool whiteTimedOut)
    {
        game.Status = "timeout";
        game.Winner = whiteTimedOut ? "black" : "white";
        if (whiteTimedOut)
            game.WhiteTimeMs = 0;
        else
            game.BlackTimeMs = 0;

        game.StatusText = DescribeStatus(game, "timeout", game.Winner);
        SendGameStateOnMainThread(game, string.Empty);
        ReportFinishedGame(game);
    }

    private void FinishLocalAbort(ActiveChessGame game, bool resign)
    {
        if (resign)
        {
            game.Status = "resign";
            game.Winner = string.Equals(game.Color, "white", StringComparison.OrdinalIgnoreCase)
                ? "black"
                : "white";
        }
        else
        {
            game.Status = "aborted";
            game.Winner = string.Empty;
        }

        game.StatusText = DescribeStatus(game, game.Status, game.Winner);
        SendGameStateOnMainThread(game, string.Empty);
        ReportFinishedGame(game);
    }

    private bool IsCurrentGame(ActiveChessGame game)
    {
        lock (_gamesLock)
            return _games.TryGetValue(game.Loader, out var current) && ReferenceEquals(current, game);
    }

    private async Task AbortGameAsync(EntityUid loader, bool resign)
    {
        var game = GetGame(loader);
        if (game == null)
            return;

        if (game.LocalBot)
        {
            FinishLocalAbort(game, resign);
            return;
        }

        if (string.IsNullOrEmpty(game.GameId))
        {
            CancelActiveGame(loader, loader);
            return;
        }

        try
        {
            var action = resign ? "resign" : "abort";
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{BoardGameEndpoint}/{Uri.EscapeDataString(game.GameId)}/{action}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", game.Token);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await StreamingHttp.SendAsync(request, game.Cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(game.Cts.Token);
                Sawmill.Warning($"Chess: Lichess rejected {action}: {(int) response.StatusCode} {body}");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Sawmill.Error($"Chess: failed to {resign} game: {e}");
        }
    }

    /// <summary>
    /// Offers a draw, or accepts the opponent's pending offer. Lichess uses the same
    /// endpoint for both directions.
    /// </summary>
    private async Task OfferDrawAsync(EntityUid loader)
    {
        var game = GetGame(loader);
        if (game == null)
            return;

        if (game.LocalBot)
        {
            if (string.Equals(game.Status, "started", StringComparison.OrdinalIgnoreCase))
            {
                game.Status = "draw";
                game.Winner = string.Empty;
                game.StatusText = "Ничья по согласию.";
                SendGameStateOnMainThread(game, string.Empty);
                ReportFinishedGame(game);
            }

            return;
        }

        if (string.IsNullOrEmpty(game.GameId) ||
            !string.Equals(game.Status, "started", StringComparison.OrdinalIgnoreCase))
        {
            SendGameState(loader, game.ViewerId == Guid.Empty ? game.UserId : game.ViewerId,
                "Предложить ничью можно только в начавшейся партии.");
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{BoardGameEndpoint}/{Uri.EscapeDataString(game.GameId)}/draw/yes");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", game.Token);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await StreamingHttp.SendAsync(request, game.Cts.Token);
            var body = await response.Content.ReadAsStringAsync(game.Cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Sawmill.Warning(
                    $"Chess: Lichess rejected draw offer: {(int) response.StatusCode} {TrimForClient(body)}");
                SendGameStateOnMainThread(
                    game,
                    DescribeLichessRejection("ничья", response, body));
                return;
            }

            game.StatusText = "Предложение ничьей отправлено. Соперник должен его принять.";
            SendGameStateOnMainThread(game, string.Empty);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Sawmill.Error($"Chess: failed to offer a draw: {e}");
            SendGameStateOnMainThread(game, "Не удалось отправить предложение ничьей.");
        }
    }

    private void ReportFinishedGame(ActiveChessGame game)
    {
        if (game.ResultReported)
            return;

        game.ResultReported = true;

        if (string.Equals(game.Status, "aborted", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(game.Status, "noStart", StringComparison.OrdinalIgnoreCase))
        {
            CompleteGame(game, string.Empty);
            return;
        }

        ChessGameOutcome outcome;
        if (string.Equals(game.Winner, "white", StringComparison.OrdinalIgnoreCase))
            outcome = ChessGameOutcome.WhiteWin;
        else if (string.Equals(game.Winner, "black", StringComparison.OrdinalIgnoreCase))
            outcome = ChessGameOutcome.BlackWin;
        else
            outcome = ChessGameOutcome.Draw;

        var us = game.Player;
        var them = ChessPlayerResult.LichessOnly(game.OpponentName);

        var white = string.Equals(game.Color, "black", StringComparison.OrdinalIgnoreCase) ? them : us;
        var black = string.Equals(game.Color, "black", StringComparison.OrdinalIgnoreCase) ? us : them;

        var url = string.IsNullOrEmpty(game.GameId) ? null : GameUrlBase + game.GameId;
        ReportGameResult(white, black, outcome, url, leaderboard: game.LeaderboardEligible);

        // The finished game is dropped right away, so reopening the PDA cannot resume it
        // and no one keeps control over the account that started it.
        CompleteGame(game, string.Empty);
    }

    private void FailGame(
        ActiveChessGame game,
        string error,
        string? statusText = null,
        HttpResponseMessage? response = null,
        string? body = null)
    {
        game.Searching = false;
        game.Status = "error";
        game.StatusText = statusText ?? "Не удалось начать партию.";
        Sawmill.Warning($"Chess: {error}");

        if (response != null && body != null)
            ApplyLichessFailure(game.UserId, response, body);

        // Refresh the account panel: a ban or a revoked token has to disable the buttons.
        if (Exists(game.Loader))
            SendLinkStatusOnMainThread(game.Loader, game.UserId);

        CompleteGame(game, error);
    }

    /// <summary>
    /// Sends the last snapshot to the PDA and then forgets the game.
    /// </summary>
    private void CompleteGame(ActiveChessGame game, string error)
    {
        _taskManager.RunOnMainThread(() =>
        {
            if (Exists(game.Loader))
                SendGameState(game.Loader, game.ViewerId == Guid.Empty ? game.UserId : game.ViewerId, error);

            lock (_gamesLock)
            {
                if (_games.TryGetValue(game.Loader, out var current) && ReferenceEquals(current, game))
                    _games.Remove(game.Loader);
            }

            // Stops the event and game streams that belong to this finished game.
            try
            {
                game.Cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        });
    }

    private void CancelActiveGame(EntityUid loader, EntityUid? notifyLoader = null)
    {
        ActiveChessGame? game;
        lock (_gamesLock)
        {
            if (!_games.TryGetValue(loader, out game))
                game = null;
            else
                _games.Remove(loader);
        }

        if (game != null)
        {
            // A challenge that has not been accepted yet would otherwise stay on the
            // account after the player leaves the PDA.
            _ = CancelChallengeAsync(game);

            // A friend challenge that is still waiting would otherwise be lost when the
            // account stream stops, so decline it explicitly.
            DeclineIncomingChallenge(game);

            try
            {
                game.Cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        if (notifyLoader is { } target && Exists(target))
            SendGameState(target, game?.UserId ?? Guid.Empty);
    }

    /// <summary>
    /// Drops every game started by this account. Used when the account is unlinked so a
    /// running match cannot keep acting with a token that no longer exists.
    /// </summary>
    private void CancelGamesForUser(Guid userId)
    {
        List<ActiveChessGame> removed = new();

        lock (_gamesLock)
        {
            foreach (var (loader, game) in _games)
            {
                if (game.UserId == userId)
                    removed.Add(game);
            }

            foreach (var game in removed)
                _games.Remove(game.Loader);
        }

        foreach (var game in removed)
        {
            try
            {
                game.Cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        foreach (var game in removed)
        {
            if (Exists(game.Loader))
                SendGameState(game.Loader, game.UserId);
        }
    }

    private ActiveChessGame? GetGame(EntityUid loader)
    {
        lock (_gamesLock)
            return _games.TryGetValue(loader, out var game) ? game : null;
    }

    #endregion

    #region UI state

    private void SendGameState(EntityUid loader, Guid requesterId, string error = "")
    {
        var game = GetGame(loader);

        if (game == null)
        {
            _cartridgeLoader.UpdateCartridgeUiState(
                loader,
                new ChessGameState(
                    false,
                    false,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    new string('.', 64),
                    "white",
                    string.Empty,
                    "idle",
                    string.Empty,
                    string.Empty,
                    0,
                    0,
                    error));
            return;
        }

        var inGame = !string.IsNullOrEmpty(game.GameId) || game.Searching;
        var canControl = string.Equals(game.Status, "started", StringComparison.OrdinalIgnoreCase);

        if (requesterId != Guid.Empty)
            game.ViewerId = requesterId;

        var delegated = requesterId != Guid.Empty && requesterId != game.UserId;

        _cartridgeLoader.UpdateCartridgeUiState(
            loader,
            new ChessGameState(
                game.Searching,
                inGame,
                game.GameId,
                game.LocalBot || string.IsNullOrEmpty(game.GameId)
                    ? string.Empty
                    : GameUrlBase + game.GameId,
                game.Color,
                game.OpponentName,
                game.Board,
                game.Turn,
                game.LastMove,
                game.Status,
                game.StatusText,
                game.Winner,
                game.WhiteTimeMs,
                game.BlackTimeMs,
                error,
                delegated,
                delegated ? game.Player.SessionName : string.Empty,
                canControl,
                game.PlayerName,
                game.TimeControl,
                game.Castling,
                game.EnPassant,
                game.Unlimited,
                game.IncomingChallengeId,
                game.IncomingChallenger,
                game.IncomingChallengeId.Length == 0 ? string.Empty : $"{game.IncomingMinutes}+{game.IncomingIncrement}",
                game.IncomingRated,
                game.IncomingColor));
    }

    private void SendGameStateOnMainThread(ActiveChessGame? game, string error)
    {
        if (game == null)
            return;

        _taskManager.RunOnMainThread(() =>
        {
            if (!Exists(game.Loader))
            {
                CancelActiveGame(game.Loader);
                return;
            }

            SendGameState(game.Loader, game.ViewerId == Guid.Empty ? game.UserId : game.ViewerId, error);
        });
    }

    private static bool IsFinishedStatus(string status)
    {
        return status switch
        {
            "mate" or "resign" or "stalemate" or "timeout" or "outoftime" or "draw" or
                "aborted" or "noStart" or "cheat" or "unknown" or "variantWin" or "variantDraw" => true,
            _ => false,
        };
    }

    private static string DescribeStatus(ActiveChessGame game, string status, string winner)
    {
        var weWon = !string.IsNullOrEmpty(winner) &&
                    string.Equals(winner, game.Color, StringComparison.OrdinalIgnoreCase);

        return status switch
        {
            "created" => $"Вызов отправлен игроку {game.OpponentName}. Ждём принятия...",
            "started" => string.Equals(game.Turn, game.Color, StringComparison.OrdinalIgnoreCase)
                ? "Партия идёт. Ваш ход."
                : "Партия идёт. Ход соперника.",
            "mate" => weWon ? "Мат! Вы победили." : "Мат. Вы проиграли.",
            "resign" => weWon ? "Соперник сдался. Вы победили." : "Вы сдались.",
            "timeout" or "outoftime" => weWon ? "У соперника истекло время. Вы победили." : "У вас истекло время.",
            "stalemate" => "Пат. Ничья.",
            "draw" => "Ничья.",
            "aborted" => "Партия отменена.",
            "noStart" => "Партия не началась.",
            "cheat" => "Партия завершена: зафиксировано нарушение.",
            _ => $"Партия завершена: {status}.",
        };
    }

    private static string TrimForClient(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Trim();
        return value.Length <= 200 ? value : value[..200] + "...";
    }

    #endregion
}
