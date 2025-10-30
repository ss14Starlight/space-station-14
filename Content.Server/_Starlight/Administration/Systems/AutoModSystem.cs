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

namespace Content.Server.Starlight.Chat.Systems;
public sealed partial class AutoModSystem : SharedChatSystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IEntitySystemManager _manager = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    private readonly ISawmill _automodLog = Logger.GetSawmill("automod");

    public const string NotificationChannel = "automod_rules";

    //cache the rules list
    private List<AutoModRule> _rules = new();
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChatAttemptEvent>(OnChatAttempt);

        _db.SubscribeToNotifications(notification =>
        {
            //check if the notification is for the automod rules
            if (notification.Channel == NotificationChannel)
            {
                //update the cache
                _automodLog.Info($"AutoModSystem received notification. Updating cache.");
                UpdateCache();
            }
        });

        //TODO: Make our cache update automatically somehow. For now this works
        //but this will need to be fixed for runtime changes
        _automodLog.Info($"AutoModSystem initialized. Updating cache.");
        UpdateCache();
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

        //_automodLog.Info($"Checking message: {message} against {_rules.Count} rules.");
        //check if the message contains any of the rules
        foreach (var rule in _rules)
        {
            //check if the rule is even enabled
            if (!rule.Enabled)
                continue;

            //convert the rule to a regex
            var regex = new Regex(rule.Regex);

            //_automodLog.Info($"Checking against rule: {rule.Regex}");

            //check for match
            if (regex.IsMatch(message))
            {
                //_automodLog.Info($"Rule matched: {rule.Regex}");
                if (rule.CancelSpeech)
                {
                    //_automodLog.Info($"Rule cancelled speech: {rule.Regex}");
                    //cancel the speech if the rule is set to do so
                    args.Cancel();
                }

                switch (rule.Severity)
                {
                    case AutoModSeverity.None:
                        break;
                    case AutoModSeverity.Warning:
                        //send a warning to the user
                        _chat.ChatMessageToOne(ChatChannel.Server,
                            rule.Message,
                            rule.Message,
                            EntityUid.Invalid,
                            false,
                            args.Sender.Channel);
                        break;
                    case AutoModSeverity.Kick:
                        //kick the user from the server
                        _automodLog.Info($"Kicking user {args.Sender} for rule: {rule.Regex}");
                        //_player.Kick(args.Sender, rule.Message);
                        break;
                    case AutoModSeverity.Ban:
                        //ban the user from the server
                        _automodLog.Info($"Banning user {args.Sender} for rule: {rule.Regex}");
                        //_player.Ban(args.Sender, rule.Message);
                        break;
                }
            }
        }
    }
}
