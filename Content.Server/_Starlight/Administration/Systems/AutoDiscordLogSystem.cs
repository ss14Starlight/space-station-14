using System.Threading.Tasks;
using Content.Server.Discord;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Administration.Systems;

public sealed class AutoDiscordLogSystem : EntitySystem
{
    [Dependency] private readonly DiscordWebhook _discord = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private WebhookIdentifier? _webhookId = null;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(StarlightCCVars.DiscordAdminAutoLogWebhook,
            value =>
            {
                if (!string.IsNullOrWhiteSpace(value))
                    _discord.GetWebhook(value, data => _webhookId = data.ToIdentifier());
            }, true);
    }

    public void LogToDiscord(string info, string author = "AutoLog") =>
        SendToDiscordWebhook(author, info);

    private async Task SendToDiscordWebhook(string title, string description)
    {
        if (_webhookId is null)
            return;

        try
        {
            var embed = new WebhookEmbed
            {
                Title = title,
                Description = description,
                Footer = new WebhookEmbedFooter
                {
                    Text = Loc.GetString("autolog-discord-footer",
                        ("server", _cfg.GetCVar(CCVars.AdminLogsServerName)),
                        ("round", _ticker.RoundId),
                        ("roundtype", Loc.GetString(_ticker.CurrentPreset?.ModeTitle ?? "bug-report-report-unknown")),
                        ("time", _timing.CurTime.Subtract(_ticker.RoundStartTimeSpan).ToString("hh':'mm':'ss")))
                }
            };
            var payload = new WebhookPayload { Embeds = [embed] };
            await _discord.CreateMessage(_webhookId.Value, payload);
            Log.Info("Sent AutoLog to Discord webhook");
        }
        catch (Exception e)
        {
            Log.Error($"Error while sending discord AutoLog:\n{e}");
        }
    }
}
