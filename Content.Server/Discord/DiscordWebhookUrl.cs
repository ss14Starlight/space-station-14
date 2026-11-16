using System;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server.Discord;

/// <summary>
///     Rewrites Discord API URLs to the configured proxy prefix (<c>discord.proxy_prefix</c>).
///     Used everywhere the server talks to Discord, so that a host in a region where
///     discord.com is unreachable can still deliver webhooks.
/// </summary>
public static class DiscordWebhookUrl
{
    public const string DiscordOrigin = "https://discord.com";

    public static string Apply(IConfigurationManager cfg, string url)
    {
        var prefix = cfg.GetCVar(CCVars.DiscordProxyPrefix);
        if (string.IsNullOrWhiteSpace(prefix))
            return url;

        return url.StartsWith(DiscordOrigin, StringComparison.OrdinalIgnoreCase)
            ? prefix.TrimEnd('/') + url[DiscordOrigin.Length..]
            : url;
    }
}
