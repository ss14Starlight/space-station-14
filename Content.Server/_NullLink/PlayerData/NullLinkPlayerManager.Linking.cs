using System.Security.Cryptography;
using System.Text;
using Content.Shared._Starlight.CCVar;

namespace Content.Server._NullLink.PlayerData;
public sealed partial class NullLinkPlayerManager
{
    private const string _scope = "identify+guilds+guilds.members.read";
    private string? _discordKey;
    private string? _discordCallback;
    private string? _steamAuth;
    private string? _steamSecret;
    private string? _secret;

    public void InitializeLinking()
    {
        _discordKey = _cfg.GetCVar(StarlightCCVars.DiscordKey).Trim();
        _discordCallback = _cfg.GetCVar(StarlightCCVars.DiscordCallback).Trim();
        _secret = _cfg.GetCVar(StarlightCCVars.Secret);
        _steamAuth = _cfg.GetCVar(StarlightCCVars.SteamBaseUrl);
        _steamSecret = _cfg.GetCVar(StarlightCCVars.SteamSecret);

        if (!string.IsNullOrEmpty(_discordCallback)
            && (!Uri.TryCreate(_discordCallback, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            _sawmill.Error($"discord.callback is not a valid absolute http(s) url: '{_discordCallback}'. discord linking will fail. set it to the exact redirect registered in the discord app, e.g. https://your.host/api/auth");

        if (!string.IsNullOrEmpty(_steamAuth)
            && (!Uri.TryCreate(_steamAuth, UriKind.Absolute, out var steamUri)
                || steamUri.Scheme != Uri.UriSchemeHttp && steamUri.Scheme != Uri.UriSchemeHttps))
            _sawmill.Error($"steam.baseurl is not a valid absolute http(s) url: '{_steamAuth}'. steam linking will fail. set it to the exact redirect registered in api, e.g. https://your.host/api/auth/steam");
    }
    public string GetDiscordAuthUrl(string customState)
    {
        if (string.IsNullOrEmpty(_discordCallback) || string.IsNullOrEmpty(_discordKey) || string.IsNullOrEmpty(_secret))
            return "";

        var secretKeyBytes = Encoding.UTF8.GetBytes(_secret);
        using var hmac = new HMACSHA256(secretKeyBytes);

        var dataBytes = Encoding.UTF8.GetBytes(customState);
        var hashBytes = hmac.ComputeHash(dataBytes);
        var state = $"{customState}.{BitConverter.ToString(hashBytes).Replace("-", "").ToLower()}";
        var encodedState = Uri.EscapeDataString(state);

        return $"https://discord.com/api/oauth2/authorize?client_id={_discordKey}&redirect_uri={Uri.EscapeDataString(_discordCallback)}&response_type=code&scope={_scope}&state={encodedState}";
    }

    public string GetSteamAuthUrl(string customState)
    {
        if (string.IsNullOrEmpty(_steamAuth) || string.IsNullOrEmpty(_steamSecret))
            return "";

        var secretKeyBytes = Encoding.UTF8.GetBytes(_steamSecret);
        using var hmac = new HMACSHA256(secretKeyBytes);

        var dataBytes = Encoding.UTF8.GetBytes(customState);
        var hashBytes = hmac.ComputeHash(dataBytes);
        var state = $"{customState}.{BitConverter.ToString(hashBytes).Replace("-", "").ToLower()}";
        var encodedState = Uri.EscapeDataString(state);

        return $"{_steamAuth}?state={encodedState}";
    }
}
