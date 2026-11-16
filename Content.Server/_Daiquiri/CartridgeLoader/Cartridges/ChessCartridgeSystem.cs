using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Content.Server.CartridgeLoader;
using Content.Server.Discord;
using Content.Shared._Daiquiri.CCVar;
using Content.Shared.CCVar;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared._Daiquiri.CartridgeLoader.Cartridges;
using Content.Shared.GameTicking;
using Content.Shared.PDA;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Daiquiri.CartridgeLoader.Cartridges;

/// <summary>
/// Server side of chess.nt: links a Lichess account to the player's game account with
/// OAuth2 + PKCE and keeps the token on the server.
///
/// Security rules:
/// - The link is keyed by the authenticated session's UserId, never by a client supplied
///   id, HWID, computer or client setting.
/// - The client only receives linked status and the public username. Access tokens never
///   leave this system.
/// - The OAuth state is random, single use, tied to the session that started the flow and
///   expires after a short timeout.
/// </summary>
public sealed partial class ChessCartridgeSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private ITaskManager _taskManager = default!;
    [Dependency] private DiscordWebhook _discord = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly ISawmill Sawmill = Logger.GetSawmill("chess");

    private const string LinkFileName = "lichess_links.json";
    private const string CallbackPath = "/lichess/callback";
    private const string AuthEndpoint = "https://lichess.org/oauth";
    private const string TokenEndpoint = "https://lichess.org/api/token";
    private const string AccountEndpoint = "https://lichess.org/api/account";
    private const string UserAgent = "ChessNT/1.0 (SS14 LightLine)";

    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(20);

    private static readonly Regex BanMinutesRegex =
        new(@"for\s+(\d+)\s+minutes", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http = new() { Timeout = HttpTimeout };
    private readonly Dictionary<string, PendingLink> _pending = new();
    private readonly Dictionary<Guid, LichessLinkRecord> _links = new();
    private readonly object _pendingLock = new();
    private readonly object _linksLock = new();
    private bool _linksLoaded;
    private string _resultWebhookUrl = string.Empty;
    private string _leaderboardWebhookUrl = string.Empty;
    private WebhookIdentifier _leaderboardWebhookIdentifier;
    private ulong _leaderboardMessageId;
    private int _leaderboardWebhookBusy;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChessCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<ChessCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<ChessCartridgeComponent, CartridgeUiClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEndMessage);
        _cfg.OnValueChanged(CCVars.DiscordChessWebhook, url =>
        {
            _resultWebhookUrl = url;
            ResetLeaderboardWebhook();
        }, true);
    }

    /// <summary>
    /// Sends the initial link/game state as soon as the cartridge UI is attached. This
    /// avoids a race where the client asks for the status before the server has finished
    /// activating the cartridge after the PDA was reopened.
    /// </summary>
    private void OnUiReady(EntityUid uid, ChessCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } actor)
                continue;

            if (!_ui.IsUiOpen(args.Loader, PdaUiKey.Key, actor))
                continue;

            Sawmill.Info($"UI ready for {session.Name}, sending chess status.");
            EnsureLinkForSession(session);
            SendLinkStatus(args.Loader, session);
            SendGameState(args.Loader, session.UserId.UserId);

            // The account stream delivers incoming friend challenges and the game id of
            // an accepted seek, so it has to stay open while the chess app is visible.
            if (TryGetAccessToken(session.UserId.UserId, out var token))
                EnsureAccountStream(args.Loader, session.UserId.UserId, token);

            return;
        }
    }

    /// <summary>
    /// Closing the PDA cancels a search that has not found an opponent yet. A match that is
    /// already running stays alive so the PDA can be handed to another player.
    /// </summary>
    private void OnUiClosed(EntityUid uid, ChessCartridgeComponent component, CartridgeUiClosedEvent args)
    {
        StopAccountStreamsForLoader(args.Loader);

        var game = GetGame(args.Loader);
        if (game == null)
            return;

        if (!string.IsNullOrEmpty(game.GameId))
            return;

        Sawmill.Info($"UI closed for {args.Loader}, cancelling the pending chess search.");
        CancelActiveGame(args.Loader);
    }

    public override void Shutdown()    {
        base.Shutdown();

        ShutdownGames();
        ShutdownAccountStreams();

        lock (_pendingLock)
        {
            foreach (var pending in _pending.Values)
            {
                pending.Listener?.Dispose();
            }

            _pending.Clear();
        }

        _http.Dispose();
    }

    private void OnUiMessage(EntityUid uid, ChessCartridgeComponent component, CartridgeMessageEvent args)
    {
        if (args is not ChessUiMessageEvent message)
            return;

        if (!_playerManager.TryGetSessionByEntity(args.Actor, out var session))
            return;

        if (!TryGetEntity(args.LoaderUid, out var loaderEntity) || loaderEntity is not { } loader)
            return;

        EnsureLinkForSession(session);

        switch (message.Action)
        {
            case ChessUiAction.RequestLinkStatus:
                Sawmill.Info($"Link status requested by {session.Name}.");
                SendLinkStatus(loader, session);
                break;

            case ChessUiAction.StartLink:
                StartLink(loader, session, message.RequestId);
                break;

            case ChessUiAction.CompleteLink:
                _ = CompleteLinkAsync(
                    loader,
                    session.UserId.UserId,
                    session.Name,
                    message.Code,
                    message.State,
                    message.Error,
                    message.ErrorDescription);
                break;

            case ChessUiAction.CancelLink:
                CancelPending(session.UserId.UserId);
                break;

            case ChessUiAction.UnlinkAccount:
                UnlinkAccount(loader, session);
                break;

            case ChessUiAction.RequestGameState:
            case ChessUiAction.StartSeek:
            case ChessUiAction.StartChallenge:
            case ChessUiAction.StartBot:
            case ChessUiAction.CancelSeek:
            case ChessUiAction.MakeMove:
            case ChessUiAction.Resign:
            case ChessUiAction.AbortGame:
            case ChessUiAction.OfferDraw:
            case ChessUiAction.AcceptChallenge:
            case ChessUiAction.DeclineChallenge:
                HandleGameAction(loader, session, message);
                break;
        }
    }

    private void SendLinkStatus(EntityUid loaderEntity, ICommonSession session, string error = "")
    {
        SendLinkStatus(loaderEntity, session.UserId.UserId, error);
    }

    private void SendLinkStatus(EntityUid loaderEntity, Guid userId, string error = "")
    {
        var info = GetLinkInfo(userId);
        var configured = !string.IsNullOrWhiteSpace(_cfg.GetCVar(DaiquiriCCVars.ChessLichessClientId));
        var expired = info?.ExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow;

        var now = DateTimeOffset.UtcNow;
        var blockedUntil = info?.BlockedUntil;
        var blocked = blockedUntil is { } until && until > now;
        var minutesLeft = blocked
            ? Math.Max(1, (int) Math.Ceiling((blockedUntil!.Value - now).TotalMinutes))
            : 0;
        var accessBroken = info?.AccessBroken == true;

        var blockReason = info?.BlockReason ?? string.Empty;
        if (blocked && string.IsNullOrEmpty(blockReason))
            blockReason = "Lichess временно запретил игру на этом аккаунте.";

        var state = new ChessLinkStatusState(
            configured,
            info.HasValue,
            info?.Username ?? string.Empty,
            info?.LichessUserId ?? string.Empty,
            expired,
            error,
            blocked,
            blockReason,
            minutesLeft,
            accessBroken);

        _cartridgeLoader.UpdateCartridgeUiState(loaderEntity, state);
    }

    /// <summary>
    /// Whether this account may start a public search. A Lichess play ban blocks the seek
    /// pool, but it does not block direct friend challenges, so those use
    /// <see cref="CanUseAccount"/> instead.
    /// </summary>
    private bool CanPlay(Guid userId, out string reason)
    {
        reason = string.Empty;

        lock (_linksLock)
        {
            EnsureLinksLoadedLocked();
            if (!_links.TryGetValue(userId, out var record))
            {
                reason = "Сначала привяжите аккаунт Lichess.";
                return false;
            }

            if (record.AccessBroken)
            {
                reason = string.IsNullOrEmpty(record.BlockReason)
                    ? "Lichess не принимает доступ к аккаунту. Привяжите аккаунт заново."
                    : record.BlockReason;
                return false;
            }

            if (record.BlockedUntil is { } until && until > DateTimeOffset.UtcNow)
            {
                var minutes = Math.Max(1, (int) Math.Ceiling((until - DateTimeOffset.UtcNow).TotalMinutes));
                reason = $"Lichess временно запретил игру на этом аккаунте (осталось ≈{minutes} мин). " +
                         "Это ограничение Lichess, а не chess.nt.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether the stored Lichess token can still be used at all. Friend challenges stay
    /// available during a play ban (Lichess only rejects public seeks), so a ban must not
    /// disable them; a revoked or broken token still has to.
    /// </summary>
    private bool CanUseAccount(Guid userId, out string reason)
    {
        reason = string.Empty;

        lock (_linksLock)
        {
            EnsureLinksLoadedLocked();
            if (!_links.TryGetValue(userId, out var record))
            {
                reason = "Сначала привяжите аккаунт Lichess.";
                return false;
            }

            if (record.AccessBroken)
            {
                reason = string.IsNullOrEmpty(record.BlockReason)
                    ? "Lichess не принимает доступ к аккаунту. Привяжите аккаунт заново."
                    : record.BlockReason;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Remembers a temporary Lichess play ban so the search buttons stay disabled until it
    /// expires, even after a server restart.
    /// </summary>
    private void SetBlocked(Guid userId, DateTimeOffset until, string reason)
    {
        lock (_linksLock)
        {
            EnsureLinksLoadedLocked();
            if (!_links.TryGetValue(userId, out var record))
                return;

            record.BlockedUntil = until;
            record.BlockReason = reason;
            SaveLinksLocked();
        }
    }

    /// <summary>
    /// Marks the stored token as unusable. The player has to link the account again before
    /// any new game can be started.
    /// </summary>
    private void MarkAccessBroken(Guid userId, string reason)
    {
        lock (_linksLock)
        {
            EnsureLinksLoadedLocked();
            if (!_links.TryGetValue(userId, out var record))
                return;

            record.AccessBroken = true;
            record.BlockReason = reason;
            SaveLinksLocked();
        }
    }

    /// <summary>
    /// Turns a failed Lichess request into a persistent block or a broken-access marker.
    /// </summary>
    private void ApplyLichessFailure(Guid userId, HttpResponseMessage response, string body)
    {
        if (body.Contains("Banned from playing", StringComparison.OrdinalIgnoreCase))
        {
            var match = BanMinutesRegex.Match(body);
            var minutes = match.Success && int.TryParse(match.Groups[1].Value, out var parsed)
                ? Math.Clamp(parsed, 1, 24 * 60)
                : 60;

            SetBlocked(
                userId,
                DateTimeOffset.UtcNow.AddMinutes(minutes),
                $"Lichess временно запретил игру на этом аккаунте (осталось ≈{minutes} мин).");
            return;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            MarkAccessBroken(userId, "Lichess больше не принимает токен. Привяжите аккаунт заново.");
            return;
        }

        if (body.Contains("challenge:write", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("Missing scope", StringComparison.OrdinalIgnoreCase))
        {
            MarkAccessBroken(
                userId,
                "У аккаунта нет доступа challenge:write. Выйдите из Lichess и привяжите аккаунт заново.");
        }
    }

    private void SendLinkStatusOnMainThread(EntityUid loaderEntity, ICommonSession session, string error = "")
    {
        SendLinkStatusOnMainThread(loaderEntity, session.UserId.UserId, error);
    }

    private void SendLinkStatusOnMainThread(EntityUid loaderEntity, Guid userId, string error = "")
    {
        _taskManager.RunOnMainThread(() => SendLinkStatus(loaderEntity, userId, error));
    }

    private void StartLink(EntityUid loaderEntity, ICommonSession session, string requestId)
    {
        var clientId = _cfg.GetCVar(DaiquiriCCVars.ChessLichessClientId).Trim();
        if (string.IsNullOrEmpty(clientId))
        {
            SendLinkStatus(loaderEntity, session, "Привязка Lichess не настроена на сервере.");
            return;
        }

        var timeoutSeconds = Math.Clamp(
            _cfg.GetCVar(DaiquiriCCVars.ChessLichessLinkTimeoutSeconds),
            30f,
            1800f);

        // Lichess accepts any absolute redirect URI for public PKCE clients. The server
        // listens on this loopback port so a local client links automatically. Remote
        // clients can paste the redirect URL into the PDA instead; the listener then
        // simply expires. Lichess redirects the player's browser to the same URI.
        var port = RandomNumberGenerator.GetInt32(49152, 65536);
        var redirectUri = $"http://127.0.0.1:{port}{CallbackPath}";
        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var pending = new PendingLink
        {
            UserId = session.UserId.UserId,
            UserName = session.Name,
            LoaderEntity = loaderEntity,
            ClientId = clientId,
            CodeVerifier = verifier,
            RedirectUri = redirectUri,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds),
        };

        lock (_pendingLock)
        {
            RemoveExpiredPendingLocked();
            RemovePendingForUserLocked(session.UserId.UserId);
            _pending[state] = pending;
        }

        // Binding may fail if the random port is taken. The flow still works: the
        // player can paste the redirect URL, which the client sends back to us.
        pending.Listener = TryStartCallbackListener(port, CallbackPath);
        if (pending.Listener != null)
            _ = WaitForServerCallbackAsync(state, pending.Listener, timeoutSeconds);

        var scope = _cfg.GetCVar(DaiquiriCCVars.ChessLichessScope).Trim();
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = challenge,
            ["state"] = state,
        };

        if (!string.IsNullOrEmpty(scope))
            query["scope"] = scope;

        var authUrl = AuthEndpoint + "?" + string.Join(
            "&",
            query.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        _cartridgeLoader.UpdateCartridgeUiState(
            loaderEntity,
            new ChessLinkStartState(authUrl, redirectUri, timeoutSeconds, requestId),
            session);

        Sawmill.Info($"Started Lichess OAuth for {session.Name} ({session.UserId}), redirect={redirectUri}");
    }

    private async Task CompleteLinkAsync(
        EntityUid loaderEntity,
        Guid userId,
        string userName,
        string code,
        string state,
        string error,
        string errorDescription,
        bool notifyIfMissing = true)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            SendLinkStatusOnMainThread(loaderEntity, userId, "Lichess не передал состояние авторизации.");
            return;
        }

        PendingLink? pending = null;
        var wrongUser = false;
        lock (_pendingLock)
        {
            if (_pending.TryGetValue(state, out var found))
            {
                if (found.UserId == userId)
                {
                    _pending.Remove(state);
                    pending = found;
                }
                else
                {
                    // Do not consume another player's pending state: a leaked state must
                    // not let an attacker cancel the legitimate link attempt.
                    wrongUser = true;
                }
            }
        }

        if (wrongUser)
        {
            Sawmill.Warning(
                $"Lichess OAuth state belongs to another user, but {userId} tried to use it.");
            SendLinkStatusOnMainThread(loaderEntity, userId, "Нельзя завершить привязку чужого аккаунта.");
            return;
        }

        if (pending == null)
        {
            if (notifyIfMissing)
            {
                SendLinkStatusOnMainThread(
                    loaderEntity,
                    userId,
                    "Сессия привязки Lichess не найдена или уже истекла.");
            }

            return;
        }

        // The flow is over, so the loopback listener (if it was started) is no longer
        // needed. Disposing it also stops a local browser callback that lost the race.
        pending.Listener?.Dispose();

        if (pending.ExpiresAt < DateTimeOffset.UtcNow)
        {
            SendLinkStatusOnMainThread(
                loaderEntity,
                userId,
                "Сессия привязки Lichess истекла. Попробуйте ещё раз.");
            return;
        }

        if (!string.IsNullOrEmpty(error))
        {
            Sawmill.Info(
                $"Lichess OAuth denied for {userName}: {error} {errorDescription}");
            var reason = string.IsNullOrWhiteSpace(errorDescription)
                ? error
                : errorDescription;
            SendLinkStatusOnMainThread(
                loaderEntity,
                userId,
                $"Lichess отклонил авторизацию: {Truncate(reason, 300)}");
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            SendLinkStatusOnMainThread(loaderEntity, userId, "Lichess не передал код авторизации.");
            return;
        }

        // Use the client id that was sent to Lichess when this flow started. If an admin
        // changes chess.lichess_client_id mid-flow, the authorization code still belongs
        // to the old client and would be rejected by the token endpoint.
        var clientId = pending.ClientId;
        if (string.IsNullOrEmpty(clientId))
        {
            SendLinkStatusOnMainThread(
                loaderEntity,
                userId,
                "Сессия привязки Lichess повреждена. Попробуйте ещё раз.");
            return;
        }

        try
        {
            var token = await ExchangeCodeAsync(
                clientId,
                code,
                pending.CodeVerifier,
                pending.RedirectUri);

            if (token == null)
            {
                SendLinkStatusOnMainThread(
                    loaderEntity,
                    userId,
                    "Не удалось получить токен Lichess. Попробуйте ещё раз.");
                return;
            }

            var account = await GetAccountAsync(token.AccessToken);
            if (account == null)
            {
                SendLinkStatusOnMainThread(
                    loaderEntity,
                    userId,
                    "Не удалось получить данные аккаунта Lichess.");
                return;
            }

            var record = new LichessLinkRecord
            {
                SS14UserName = userName,
                LichessUserId = account.Id,
                Username = account.Username,
                AccessToken = token.AccessToken,
                ExpiresAt = token.ExpiresIn is { } expiresIn && expiresIn > 0
                    ? DateTimeOffset.UtcNow.AddSeconds(expiresIn)
                    : null,
                LinkedAt = DateTimeOffset.UtcNow,
            };

            if (!SaveLink(userId, record))
            {
                Sawmill.Error(
                    $"Failed to persist Lichess link {account.Username} ({account.Id}) for {userName} ({userId})");
                SendLinkStatusOnMainThread(
                    loaderEntity,
                    userId,
                    "Не удалось сохранить привязку Lichess на сервере. Попробуйте ещё раз.");
                return;
            }

            Sawmill.Info(
                $"Linked Lichess account {account.Username} ({account.Id}) to {userName} ({userId})");

            SendLinkStatusOnMainThread(loaderEntity, userId);
        }
        catch (Exception e)
        {
            Sawmill.Error($"Lichess OAuth failed for {userName}: {e}");
            SendLinkStatusOnMainThread(
                loaderEntity,
                userId,
                "Ошибка привязки Lichess. Попробуйте ещё раз.");
        }
    }

    private LichessOAuthCallbackListener? TryStartCallbackListener(int port, string callbackPath)
    {
        var listener = new LichessOAuthCallbackListener(port, callbackPath);
        try
        {
            listener.Start();
            return listener;
        }
        catch (Exception e)
        {
            Sawmill.Warning($"Could not start Lichess OAuth loopback listener on port {port}: {e.Message}");
            listener.Dispose();
            return null;
        }
    }

    private async Task WaitForServerCallbackAsync(
        string state,
        LichessOAuthCallbackListener listener,
        float timeoutSeconds)
    {
        var callback = await listener.WaitForCallbackAsync(TimeSpan.FromSeconds(timeoutSeconds));
        if (callback == null)
            return;

        if (!string.Equals(callback.State, state, StringComparison.Ordinal))
        {
            Sawmill.Warning("Lichess OAuth callback state mismatch on the loopback listener.");
            return;
        }

        PendingLink? pending;
        lock (_pendingLock)
        {
            if (!_pending.TryGetValue(state, out pending))
                return;
        }

        await CompleteLinkAsync(
            pending.LoaderEntity,
            pending.UserId,
            pending.UserName,
            callback.Code,
            callback.State,
            callback.Error,
            callback.ErrorDescription,
            notifyIfMissing: false);
    }

    private async Task<LichessTokenResponse?> ExchangeCodeAsync(
        string clientId,
        string code,
        string verifier,
        string redirectUri)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = content,
        };
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await _http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Sawmill.Warning(
                $"Lichess token exchange failed: {(int)response.StatusCode} {Truncate(body, 300)}");
            return null;
        }

        var token = JsonSerializer.Deserialize<LichessTokenResponse>(body);
        if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            Sawmill.Warning("Lichess token exchange returned no access token.");
            return null;
        }

        return token;
    }

    private async Task<LichessAccountResponse?> GetAccountAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AccountEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await _http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Sawmill.Warning(
                $"Lichess account request failed: {(int)response.StatusCode} {Truncate(body, 300)}");
            return null;
        }

        var account = JsonSerializer.Deserialize<LichessAccountResponse>(body);
        if (account == null ||
            string.IsNullOrWhiteSpace(account.Id) ||
            string.IsNullOrWhiteSpace(account.Username))
        {
            Sawmill.Warning("Lichess account response is missing id/username.");
            return null;
        }

        return account;
    }

    /// <summary>
    /// Whether a chess result webhook is configured. Exposed so callers can warn when
    /// results would otherwise be silently dropped.
    /// </summary>
    public bool HasResultWebhook => !string.IsNullOrWhiteSpace(_resultWebhookUrl);

    /// <summary>
    /// Builds the public identity of a player for the result webhook. This never exposes
    /// the access token: only the Lichess username is read from the link store.
    /// </summary>
    public ChessPlayerResult GetPlayerResult(ICommonSession session)
    {
        var characterName = session.AttachedEntity is { } entity && Exists(entity)
            ? MetaData(entity).EntityName
            : session.Name;

        if (string.IsNullOrWhiteSpace(characterName))
            characterName = session.Name;

        var lichessUsername = GetLinkInfo(session.UserId.UserId)?.Username ?? string.Empty;
        return new ChessPlayerResult(session.Name, characterName, lichessUsername);
    }

    /// <summary>
    /// Same as <see cref="GetPlayerResult(ICommonSession)"/> but for events that arrive on a
    /// background stream and only carry the account id.
    /// </summary>
    private ChessPlayerResult GetPlayerResultForUser(Guid userId)
    {
        if (_playerManager.TryGetSessionById(new NetUserId(userId), out var session))
            return GetPlayerResult(session);

        return new ChessPlayerResult(string.Empty, string.Empty, GetLinkInfo(userId)?.Username ?? string.Empty);
    }

    /// <summary>
    /// Reports a finished game to the configured Discord webhook. Call this from the
    /// Lichess Board API bridge once a game reaches a terminal state.
    /// </summary>
    public void ReportGameResult(
        ICommonSession white,
        ICommonSession black,
        ChessGameOutcome outcome,
        string? gameUrl = null,
        bool leaderboard = true)
    {
        ReportGameResult(GetPlayerResult(white), GetPlayerResult(black), outcome, gameUrl, leaderboard);
    }

    /// <summary>
    /// Reports a finished game when the caller already has the public player identities.
    /// Useful when a player has disconnected before the game ended.
    /// </summary>
    public void ReportGameResult(
        ChessPlayerResult white,
        ChessPlayerResult black,
        ChessGameOutcome outcome,
        string? gameUrl = null,
        bool leaderboard = true)
    {
        if (leaderboard)
            RecordChessStats(white, black, outcome);

        _ = SendGameResultWebhookAsync(white, black, outcome, gameUrl);
    }

    private async Task SendGameResultWebhookAsync(
        ChessPlayerResult white,
        ChessPlayerResult black,
        ChessGameOutcome outcome,
        string? gameUrl)
    {
        if (string.IsNullOrWhiteSpace(_resultWebhookUrl))
            return;

        try
        {
            var webhook = await _discord.GetWebhook(_resultWebhookUrl);
            if (webhook == null)
                return;

            var (title, description) = outcome switch
            {
                ChessGameOutcome.WhiteWin => (
                    "Победа белых",
                    $"{DisplayName(white)} (белые) победил {DisplayName(black)} (чёрные)."),
                ChessGameOutcome.BlackWin => (
                    "Победа чёрных",
                    $"{DisplayName(black)} (чёрные) победил {DisplayName(white)} (белые)."),
                _ => (
                    "Ничья",
                    "Партия завершилась вничью."),
            };

            var score = outcome switch
            {
                ChessGameOutcome.WhiteWin => "1-0",
                ChessGameOutcome.BlackWin => "0-1",
                _ => "1/2-1/2",
            };

            var fields = new List<WebhookEmbedField>
            {
                new()
                {
                    Name = "Результат",
                    Value = score,
                    Inline = false,
                },
                new()
                {
                    Name = "Белые",
                    Value = FormatPlayer(white),
                    Inline = false,
                },
                new()
                {
                    Name = "Чёрные",
                    Value = FormatPlayer(black),
                    Inline = false,
                },
            };

            if (outcome != ChessGameOutcome.Draw)
            {
                var winner = outcome == ChessGameOutcome.WhiteWin ? white : black;
                var loser = outcome == ChessGameOutcome.WhiteWin ? black : white;

                fields.Add(new WebhookEmbedField
                {
                    Name = "Победитель",
                    Value = FormatPlayer(winner),
                    Inline = false,
                });
                fields.Add(new WebhookEmbedField
                {
                    Name = "Проигравший",
                    Value = FormatPlayer(loser),
                    Inline = false,
                });
            }

            if (!string.IsNullOrWhiteSpace(gameUrl))
            {
                fields.Add(new WebhookEmbedField
                {
                    Name = "Партия",
                    Value = $"[Открыть партию]({gameUrl})",
                    Inline = false,
                });
            }

            var embed = new WebhookEmbed
            {
                Title = $"Chess.NT — {title}",
                Description = description,
                Color = 0xC9A227,
                Fields = fields,
            };

            await _discord.CreateMessage(
                webhook.Value.ToIdentifier(),
                new WebhookPayload
                {
                    Username = "Chess.NT",
                    Embeds = [embed],
                });
        }
        catch (Exception e)
        {
            Sawmill.Error($"Failed to send chess result webhook: {e}");
        }
    }

    private static string FormatPlayer(ChessPlayerResult player)
    {
        var lichess = string.IsNullOrWhiteSpace(player.LichessUsername)
            ? "не привязан"
            : player.LichessUsername;

        return $"**SS14:** {Display(player.SessionName)}\n" +
               $"**Персонаж:** {Display(player.CharacterName)}\n" +
               $"**Lichess:** {lichess}";
    }

    private static string DisplayName(ChessPlayerResult player)
    {
        if (!string.IsNullOrWhiteSpace(player.SessionName))
            return player.SessionName;

        return string.IsNullOrWhiteSpace(player.LichessUsername)
            ? "—"
            : player.LichessUsername;
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    private void CancelPending(Guid userId)
    {
        lock (_pendingLock)
        {
            RemovePendingForUserLocked(userId);
        }
    }

    private void RemovePendingForUserLocked(Guid userId)
    {
        foreach (var state in _pending
                     .Where(pair => pair.Value.UserId == userId)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            _pending[state].Listener?.Dispose();
            _pending.Remove(state);
        }
    }

    private void RemoveExpiredPendingLocked()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var state in _pending
                     .Where(pair => pair.Value.ExpiresAt <= now)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            _pending[state].Listener?.Dispose();
            _pending.Remove(state);
        }
    }

    /// <summary>
    /// Guest/local connections receive a new random <see cref="NetUserId"/> every time they
    /// reconnect, so a link stored only under the GUID would look lost after a restart or a
    /// reconnect. Stamp the current record with the stable account name and, when the GUID
    /// changed, move the newest matching link onto the new GUID.
    /// </summary>
    private void EnsureLinkForSession(ICommonSession session)
    {
        var userId = session.UserId.UserId;
        var userName = session.Name;
        if (string.IsNullOrWhiteSpace(userName))
            return;

        lock (_linksLock)
        {
            EnsureLinksLoadedLocked();

            if (_links.TryGetValue(userId, out var current))
            {
                if (string.Equals(current.SS14UserName, userName, StringComparison.OrdinalIgnoreCase))
                    return;

                var previousName = current.SS14UserName;
                current.SS14UserName = userName;
                if (!SaveLinksLocked())
                    current.SS14UserName = previousName;
                return;
            }

            var candidates = _links
                .Where(pair =>
                    !string.IsNullOrEmpty(pair.Value.SS14UserName) &&
                    string.Equals(pair.Value.SS14UserName, userName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(pair => pair.Value.LinkedAt)
                .ToList();

            if (candidates.Count == 0)
            {
                // Links created before SS14UserName was stored cannot be matched by name.
                // If this is the only connected player and all legacy links point at one
                // Lichess account, adopt the newest one. This recovers the old GUID-keyed
                // record after a guest reconnect without guessing between different accounts.
                TryAdoptLegacyLinkLocked(userId, userName);
                return;
            }

            var chosen = candidates[0];
            var record = chosen.Value;
            record.SS14UserName = userName;

            _links.Remove(chosen.Key);
            _links[userId] = record;

            // Keep only the newest record for this SS14 account.
            foreach (var duplicate in candidates.Skip(1))
                _links.Remove(duplicate.Key);

            if (SaveLinksLocked())
            {
                Sawmill.Info(
                    $"Migrated Lichess link for {userName} from {chosen.Key} to {userId}.");
                return;
            }

            // Keep memory and disk in sync if the file could not be written.
            _links.Remove(userId);
            foreach (var candidate in candidates)
                _links[candidate.Key] = candidate.Value;
        }
    }

    /// <summary>
    /// One-time migration for links stored before <see cref="LichessLinkRecord.SS14UserName"/>
    /// existed. It is deliberately conservative: only the sole connected player can claim
    /// legacy records, and only when every legacy record belongs to the same Lichess account.
    /// </summary>
    private bool TryAdoptLegacyLinkLocked(Guid userId, string userName)
    {
        if (_playerManager.PlayerCount != 1)
            return false;

        var legacy = _links
            .Where(pair =>
                string.IsNullOrEmpty(pair.Value.SS14UserName) &&
                !string.IsNullOrWhiteSpace(pair.Value.LichessUserId))
            .OrderByDescending(pair => pair.Value.LinkedAt)
            .ToList();

        if (legacy.Count == 0)
            return false;

        var lichessUserId = legacy[0].Value.LichessUserId;
        if (legacy.Any(pair =>
                !string.Equals(pair.Value.LichessUserId, lichessUserId, StringComparison.Ordinal)))
        {
            return false;
        }

        var chosen = legacy[0];
        var record = chosen.Value;
        var previousName = record.SS14UserName;
        record.SS14UserName = userName;

        _links.Remove(chosen.Key);
        _links[userId] = record;

        foreach (var duplicate in legacy.Skip(1))
            _links.Remove(duplicate.Key);

        if (SaveLinksLocked())
        {
            Sawmill.Info(
                $"Adopted legacy Lichess link {record.Username} for {userName} ({userId}).");
            return true;
        }

        // Restore the exact in-memory state if persistence failed.
        record.SS14UserName = previousName;
        _links.Remove(userId);
        foreach (var pair in legacy)
            _links[pair.Key] = pair.Value;

        return false;
    }

    private LichessLinkInfo? GetLinkInfo(Guid userId)
    {
        lock (_linksLock)
        {
            EnsureLinksLoadedLocked();
            return _links.TryGetValue(userId, out var record)
                ? new LichessLinkInfo(
                    record.LichessUserId,
                    record.Username,
                    record.ExpiresAt,
                    record.LinkedAt,
                    record.BlockedUntil,
                    record.BlockReason,
                    record.AccessBroken)
                : null;
        }
    }

    /// <summary>
    /// Server-only accessor for the future Board API bridge. Never send the returned token
    /// to a client.
    /// </summary>
    public bool TryGetAccessToken(Guid userId, out string accessToken)
    {
        lock (_linksLock)
        {
            EnsureLinksLoadedLocked();
            if (_links.TryGetValue(userId, out var record) &&
                !string.IsNullOrWhiteSpace(record.AccessToken))
            {
                accessToken = record.AccessToken;
                return true;
            }
        }

        accessToken = string.Empty;
        return false;
    }

    private bool SaveLink(Guid userId, LichessLinkRecord record)
    {
        lock (_linksLock)
        {
            EnsureLinksLoadedLocked();
            var hadPrevious = _links.TryGetValue(userId, out var previous);

            var removedDuplicates = new List<KeyValuePair<Guid, LichessLinkRecord>>();
            if (!string.IsNullOrWhiteSpace(record.SS14UserName))
            {
                foreach (var pair in _links
                             .Where(pair =>
                                 pair.Key != userId &&
                                 string.Equals(
                                     pair.Value.SS14UserName,
                                     record.SS14UserName,
                                     StringComparison.OrdinalIgnoreCase))
                             .ToList())
                {
                    removedDuplicates.Add(pair);
                    _links.Remove(pair.Key);
                }
            }

            _links[userId] = record;

            if (SaveLinksLocked())
                return true;

            // Do not report a link that would vanish on the next restart. Restore the
            // previous record, if any, so the in-memory state still matches the disk.
            if (hadPrevious)
                _links[userId] = previous!;
            else
                _links.Remove(userId);

            foreach (var duplicate in removedDuplicates)
                _links[duplicate.Key] = duplicate.Value;

            return false;
        }
    }

    /// <summary>
    /// Removes the calling player's Lichess link, cancels a pending authorization and stops
    /// any game started with that token. The access token never existed on the client, so
    /// unlinking leaves no way to reach the account from this machine.
    /// </summary>
    private void UnlinkAccount(EntityUid loaderEntity, ICommonSession session)
    {
        var userId = session.UserId.UserId;

        CancelPending(userId);
        CancelGamesForUser(userId);

        if (!RemoveLink(userId))
        {
            SendLinkStatus(loaderEntity, userId, "Не удалось отвязать аккаунт: ошибка записи на диск.");
            return;
        }

        SendLinkStatus(loaderEntity, userId);
    }

    private bool RemoveLink(Guid userId)
    {
        lock (_linksLock)
        {
            EnsureLinksLoadedLocked();
            if (!_links.TryGetValue(userId, out var previous))
                return true;

            _links.Remove(userId);

            if (SaveLinksLocked())
                return true;

            // Keep memory and disk in sync if the file could not be written.
            _links[userId] = previous;
            return false;
        }
    }

    private void EnsureLinksLoadedLocked()
    {
        if (_linksLoaded)
            return;

        _linksLoaded = true;

        var path = LinkFilePath();
        if (path == null || !File.Exists(path))
            return;

        try
        {
            var json = File.ReadAllText(path);
            var saved = JsonSerializer.Deserialize<Dictionary<Guid, LichessLinkRecord>>(json);
            if (saved == null)
                return;

            foreach (var (userId, record) in saved)
            {
                if (string.IsNullOrWhiteSpace(record.LichessUserId) ||
                    string.IsNullOrWhiteSpace(record.Username) ||
                    string.IsNullOrWhiteSpace(record.AccessToken))
                {
                    Sawmill.Warning($"Skipping invalid Lichess link for {userId}.");
                    continue;
                }

                _links[userId] = record;
            }

            Sawmill.Info($"Loaded {_links.Count} Lichess link(s).");
        }
        catch (Exception e)
        {
            Sawmill.Error($"Failed to load Lichess links: {e}");
        }
    }

    private bool SaveLinksLocked()
    {
        var path = LinkFilePath();
        if (path == null)
            return false;

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(
                _links,
                new JsonSerializerOptions { WriteIndented = true });

            // Write to a sibling file and replace atomically so a crash cannot leave
            // the permanent link store half-written.
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            RestrictLinkFilePermissions(tempPath);
            File.Move(tempPath, path, overwrite: true);
            return true;
        }
        catch (Exception e)
        {
            Sawmill.Error($"Failed to save Lichess links: {e}");
            return false;
        }
    }

    private string? LinkFilePath()
    {
        var root = _resources.UserData.RootDir;
        return string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, LinkFileName);
    }

    private static void RestrictLinkFilePermissions(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // Best effort: the server data directory is already server-private.
        }
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    private sealed class PendingLink
    {
        public Guid UserId;
        public string UserName = string.Empty;
        public EntityUid LoaderEntity;
        public string ClientId = string.Empty;
        public string CodeVerifier = string.Empty;
        public string RedirectUri = string.Empty;
        public DateTimeOffset ExpiresAt;
        public LichessOAuthCallbackListener? Listener;
    }

    private sealed class LichessLinkRecord
    {
        /// <summary>
        /// Stable SS14 account name. Guest/local connections get a fresh random
        /// <see cref="NetUserId"/> on every reconnect, so the GUID alone cannot be used to
        /// remember a Lichess link across sessions.
        /// </summary>
        public string SS14UserName { get; set; } = string.Empty;

        public string LichessUserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public DateTimeOffset? ExpiresAt { get; set; }
        public DateTimeOffset LinkedAt { get; set; }

        /// <summary>Lichess play ban, persisted so a restart does not forget it.</summary>
        public DateTimeOffset? BlockedUntil { get; set; }

        /// <summary>Human readable reason for the current block or broken access.</summary>
        public string BlockReason { get; set; } = string.Empty;

        /// <summary>The stored token can no longer be used and the account must be re-linked.</summary>
        public bool AccessBroken { get; set; }
    }

    private readonly record struct LichessLinkInfo(
        string LichessUserId,
        string Username,
        DateTimeOffset? ExpiresAt,
        DateTimeOffset LinkedAt,
        DateTimeOffset? BlockedUntil,
        string BlockReason,
        bool AccessBroken);

    private sealed class LichessTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }
    }

    private sealed class LichessAccountResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;
    }
}
