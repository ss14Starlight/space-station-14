using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Chat.V2.Repository;
using Content.Server.Database;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Chat.V2;
using Content.Shared.Chat.V2.Repository;
using Content.Shared.Emoting;
using Content.Shared.Speech;
using Robust.Server.Player;
using Robust.Shared.Network;
using Content.Server.Administration.Managers;
using Content.Shared.Database;

namespace Content.Server.Starlight.Chat.Systems;
public sealed partial class AutoModSystem : SharedChatSystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IEntitySystemManager _manager = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;
    [Dependency] private readonly IBanManager _banManager = default!;
    private readonly ISawmill _automodLog = Logger.GetSawmill("automod");
    [Dependency] private readonly Content.Server.Administration.Logs.IAdminLogManager _adminLogger = default!;

    public const string NotificationChannel = "automod_rules";

    //cache the rules list
    private List<AutoModRule> _rules = new();
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChatAttemptEvent>(OnChatAttempt);

        _db.SubscribeToNotifications(async notification =>
        {
            //check if the notification is for the automod rules
            if (notification.Channel == NotificationChannel)
            {
                //update the cache
                _automodLog.Info($"AutoModSystem received notification. Updating cache.");
                await UpdateCache();
            }
        });

        //TODO: Make our cache update automatically somehow. For now this works
        //but this will need to be fixed for runtime changes
        _automodLog.Info($"AutoModSystem initialized. Updating cache.");
        _ = UpdateCache(); // fire and forget
    }

    //task to update cache
    public async Task UpdateCache()
    {
        //get the rules from the database
        _rules = await _db.GetAutoModRules();
    }

    //watch for chat messages
    private void OnChatAttempt(ChatAttemptEvent args)
    {
        //set the message to nothing
        string message = args.Message;

        //check if the message contains any of the rules
        foreach (var rule in _rules)
        {
            //check if the rule is even enabled
            if (!rule.Enabled)
                continue;

            //convert the rule to a regex
            var regex = new Regex(rule.Regex);

            //check for match
            if (regex.IsMatch(message))
            {
                if (rule.CancelSpeech)
                {
                    _adminLogger.Add(LogType.AdminCommands, LogImpact.High, $"[AutoMod] Cleared speech of user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex}");
                    //cancel the speech if the rule is set to do so
                    args.Cancel();
                }

                switch (rule.Severity)
                {
                    case AutoModSeverity.None:
                        break;
                    case AutoModSeverity.Warning:
                        //send a warning to the user
                        _adminLogger.Add(LogType.AdminCommands, LogImpact.Medium, $"[AutoMod] Warned user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} - Reason: {rule.Message}");
                        _chat.ChatMessageToOne(ChatChannel.Server,
                            rule.Message,
                            rule.Message,
                            EntityUid.Invalid,
                            false,
                            args.Sender.Channel);
                        break;
                    case AutoModSeverity.Kick:
                        //kick the user from the server
                        string kickReason = string.IsNullOrWhiteSpace(rule.Message)
                            ? "Kicked by AutoMod"
                            : $"Kicked by AutoMod for: {rule.Message}";
                        _adminLogger.Add(LogType.AdminCommands, LogImpact.High, $"[AutoMod] Kicked user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} - Reason: {kickReason}");
                        _netManager.DisconnectChannel(args.Sender.Channel, kickReason);
                        break;
                    case AutoModSeverity.Ban:
                        //ban the user from the server
                        string banReason = string.IsNullOrWhiteSpace(rule.Message)
                            ? "Banned by AutoMod"
                            : $"Banned by AutoMod for: {rule.Message}";
                        uint? duration = 60 * 24 * 7;
                        _banManager.CreateServerBan(
                            args.Sender.UserId,
                            args.Sender.Name,
                            null,
                            null,
                            null,
                            duration,
                            NoteSeverity.High,
                            banReason
                        );
                        _adminLogger.Add(LogType.AdminCommands, LogImpact.Extreme, $"[AutoMod] Banned user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} - Reason: {banReason} - Duration: {duration} minutes");
                        _netManager.DisconnectChannel(args.Sender.Channel, banReason);
                        break;
                }
            }
        }
    }
}
