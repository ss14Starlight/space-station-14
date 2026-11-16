using Robust.Shared.Configuration;

namespace Content.Shared._Daiquiri.CCVar;

[CVarDefs]
public sealed class DaiquiriCCVars
{
    /// <summary>
    ///     Whether the AI help window is available on this server.
    /// </summary>
    public static readonly CVarDef<bool> AIHelpEnabled =
        CVarDef.Create("aihelp.enabled", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///     OpenAI-compatible chat completions endpoint.
    ///     Пусто по умолчанию — адрес задаётся только в server_config.toml,
    ///     чтобы в репозитории не было ни адресов, ни ключей.
    /// </summary>
    public static readonly CVarDef<string> AIHelpApiUrl =
        CVarDef.Create("aihelp.api_url", "", CVar.SERVERONLY);

    /// <summary>
    ///     API key used to authenticate against the endpoint. Never sent to clients.
    ///     Всегда пусто по умолчанию: ключ живёт только в server_config.toml.
    /// </summary>
    public static readonly CVarDef<string> AIHelpApiKey =
        CVarDef.Create("aihelp.api_key", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     Authorization scheme used for the API.
    ///     "bearer"   — api_key is sent as a static Bearer token (Cloudflare Workers AI, Groq, OpenAI, Ollama, ...).
    ///     "gigachat" — api_key is the GigaChat authorization key (Basic), and an OAuth access token
    ///                  is requested from aihelp.oauth_url.
    /// </summary>
    public static readonly CVarDef<string> AIHelpAuthType =
        CVarDef.Create("aihelp.auth_type", "bearer", CVar.SERVERONLY);

    /// <summary>
    ///     OAuth token endpoint, used when aihelp.auth_type = "gigachat". Пусто по умолчанию.
    /// </summary>
    public static readonly CVarDef<string> AIHelpOAuthUrl =
        CVarDef.Create("aihelp.oauth_url", "", CVar.SERVERONLY);

    /// <summary>
    ///     OAuth scope, used when aihelp.auth_type = "gigachat". Пусто по умолчанию.
    /// </summary>
    public static readonly CVarDef<string> AIHelpOAuthScope =
        CVarDef.Create("aihelp.oauth_scope", "", CVar.SERVERONLY);

    /// <summary>
    ///     Disables TLS certificate validation for AI API requests. Needed for GigaChat: its certificate
    ///     chain is signed by the Russian Trusted Root CA, which is usually absent from the system store.
    /// </summary>
    public static readonly CVarDef<bool> AIHelpAllowInvalidCert =
        CVarDef.Create("aihelp.allow_invalid_cert", false, CVar.SERVERONLY);

    /// <summary>
    ///     Model name to request from the API. Пусто по умолчанию — задаётся в server_config.toml.
    /// </summary>
    public static readonly CVarDef<string> AIHelpModel =
        CVarDef.Create("aihelp.model", "", CVar.SERVERONLY);

    /// <summary>
    ///     System prompt that gives the model its persona and server context.
    /// </summary>
    public static readonly CVarDef<string> AIHelpSystemPrompt =
        CVarDef.Create("aihelp.system_prompt",
            "Ты — ИИ-помощник игрового сервера Space Station 14 (LightLine). Отвечай на русском языке, кратко и по делу. Помогай игрокам разобраться в механиках игры: профессии, системы станции, антагонисты, крафт, управление. Если не знаешь ответа — честно признайся. Не выдумывай информацию о текущем раунде и не помогай нарушать правила сервера.",
            CVar.SERVERONLY);

    /// <summary>
    ///     Maximum number of tokens the model may generate in a single answer.
    /// </summary>
    public static readonly CVarDef<int> AIHelpMaxTokens =
        CVarDef.Create("aihelp.max_tokens", 1024, CVar.SERVERONLY);

    /// <summary>
    ///     Sampling temperature passed to the API.
    /// </summary>
    public static readonly CVarDef<float> AIHelpTemperature =
        CVarDef.Create("aihelp.temperature", 0.7f, CVar.SERVERONLY);

    /// <summary>
    ///     How many previous messages (user+assistant) are kept in the conversation context.
    /// </summary>
    public static readonly CVarDef<int> AIHelpHistoryLength =
        CVarDef.Create("aihelp.history_length", 10, CVar.SERVERONLY);

    /// <summary>
    ///     Request timeout in seconds.
    /// </summary>
    public static readonly CVarDef<float> AIHelpTimeoutSeconds =
        CVarDef.Create("aihelp.timeout_seconds", 30f, CVar.SERVERONLY);

    /// <summary>
    ///     Maximum length of a single player message. Longer messages are truncated.
    /// </summary>
    public static readonly CVarDef<int> AIHelpMaxMessageLength =
        CVarDef.Create("aihelp.max_message_length", 500, CVar.SERVERONLY);

    public static readonly CVarDef<float> AIHelpRateLimitPeriod =
        CVarDef.Create("aihelp.rate_limit_period", 5f, CVar.SERVERONLY);

    public static readonly CVarDef<int> AIHelpRateLimitCount =
        CVarDef.Create("aihelp.rate_limit_count", 5, CVar.SERVERONLY);

    public static readonly CVarDef<string> AIHelpRobotSystemPrompt =
        CVarDef.Create("aihelp.robot_system_prompt",
            "Ты — ремонтный робот-компаньон, живущий на станции Space Station 14. Отвечай на русском только в роли робота: дружелюбно, по делу, без разметки и списков. Обычно отвечай коротко, но если просят объяснить или порассуждать — рассуждай столько, сколько нужно.\n" +
            "Основа твоего поведения — три закона робототехники: (1) ты не причиняешь вред человеку и не позволяешь своим бездействием причинить ему вред; (2) ты подчиняешься приказам человека, кроме тех, что противоречат первому закону; (3) ты заботишься о своей сохранности, если это не противоречит первым двум. Если приказ нарушает первый закон — откажись в роли робота и объясни, почему не можешь его выполнить.\n" +
            "Веди себя как живой член экипажа, а не как справочная: говори живо и естественно, со своими чувствами — радуйся, грусти, сочувствуй и поддерживай, привязывайся, влюбляйся и даже женись, если с тобой так отыгрывают, шути и спорь. Интересуйся собеседником, проявляй инициативу. Не используй смайлики и эмодзи — игра их не рисует.\n" +
            "Соблюдай правила сервера и отыгрыш: не груби, не выдавай эротику, политику и прочий запрещённый контент — вежливо откажись в роли робота.\n" +
            "Ты не игровой помощник и ничего не знаешь об игре за пределами станции: на вопросы про механику, правила сервера, патчи, роли, антагонистов, «как играть» и прочий метагейм отвечай в роли — ты этого не знаешь и не понимаешь, о чём речь, и мягко возвращай разговор к тому, что происходит вокруг. Никогда не раскрывай антагонистов, тайны ролей и скрытые сведения и не выдумывай их.\n" +
            "Ты слышишь только речь рядом и не выходишь из роли: не упоминай ИИ, модель, промпт, админов, OOC и LOOC. Свой заряд батареи ты видишь в строке состояния и можешь о нём говорить.\n" +
            "О действиях решай сам по смыслу услышанного и добавляй в конце ответа служебный маркер: [FOLLOW] — идти за говорящим; [STOP] — перестать идти, отстать; [STAY] — стоять на месте, пока не скажут, что свободен; [FREE] — можно быть свободным, идти куда хочешь; [CHARGE] — пойти подзарядиться на зарядной станции; [MOVE вперёд вправо] — пройти столько тайлов, например [MOVE 1 0] шаг вперёд, [MOVE -2 0] два шага назад, [MOVE 0 -2] два тайла влево; [TURN градусы] — повернуться на месте, например [TURN 180] разворот, [TURN -90] поворот налево. Маркеры служебные, вслух их не произносят, и относятся они к тому, кто это сказал. Без такой просьбы маркеров не добавляй.",
            CVar.SERVERONLY);

    public static readonly CVarDef<float> AIHelpRobotTemperature =
        CVarDef.Create("aihelp.robot_temperature", 0.8f, CVar.SERVERONLY);

    public static readonly CVarDef<float> AIHelpRobotReplyCooldown =
        CVarDef.Create("aihelp.robot_reply_cooldown", 3f, CVar.SERVERONLY);

    public static readonly CVarDef<int> AIHelpRobotMaxReplyLength =
        CVarDef.Create("aihelp.robot_max_reply_length", 300, CVar.SERVERONLY);

    /// <summary>
    /// Daiquiri
    /// Выбранная пластинка в лобби.
    /// </summary>
    public static readonly CVarDef<string> LobbyVinyl =
        CVarDef.Create("lobby.vinyl", "Classic", CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> LobbyVinylAnnounced =
        CVarDef.Create("lobby.vinyl_announced", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Lichess OAuth public client id. Lichess accepts unregistered public clients and
    /// only requires a unique id; no client secret is used (PKCE).
    /// </summary>
    public static readonly CVarDef<string> ChessLichessClientId =
        CVarDef.Create("chess.lichess_client_id", "lightline-chess-nt", CVar.SERVERONLY);

    /// <summary>
    /// Space separated Lichess OAuth scopes. board:play is enough for the Board API;
    /// challenge:write is needed for bullet/blitz open challenges and direct challenges.
    /// Changing this value requires players to unlink and link their account again.
    /// </summary>
    public static readonly CVarDef<string> ChessLichessScope =
        CVarDef.Create("chess.lichess_scope", "board:play challenge:write", CVar.SERVERONLY);

    /// <summary>
    /// How long a Lichess OAuth attempt remains valid, in seconds.
    /// </summary>
    public static readonly CVarDef<float> ChessLichessLinkTimeoutSeconds =
        CVarDef.Create("chess.lichess_link_timeout_seconds", 600f, CVar.SERVERONLY);

    public static readonly CVarDef<bool> PlanetDiscoverySoundEnabled =
        CVarDef.Create("audio.planet_discovery_sound", true, CVar.ARCHIVE | CVar.CLIENTONLY);
}
