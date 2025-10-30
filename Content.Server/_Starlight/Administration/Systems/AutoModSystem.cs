using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
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
    private readonly Dictionary<(NetUserId, int), int> _userOffenceCounts = new();
    // Tracks last offence time for each user/rule
    private readonly Dictionary<(NetUserId, int), DateTime> _userOffenceTimes = new();

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
            if (!regex.IsMatch(message))
                continue;

            // Track and increment offence count for this user/rule
            var key = (args.Sender.UserId, rule.Id);
            // Decay logic: if enough time has passed since last offence, decrement offenceIndex
            int offenceIndex = 0;
            if (_userOffenceCounts.TryGetValue(key, out var storedIndex))
            {
                offenceIndex = storedIndex;
                if (offenceIndex > 0 && rule.Offences != null && offenceIndex < rule.Offences.Count)
                {
                    // Only decay if not first offence and decay is set
                    var lastTime = _userOffenceTimes.TryGetValue(key, out var t) ? t : DateTime.MinValue;
                    var decaySeconds = rule.Offences[offenceIndex].DecaySeconds;
                    if (decaySeconds > 0 && (DateTime.UtcNow - lastTime).TotalSeconds >= decaySeconds)
                    {
                        offenceIndex--;
                    }
                }
                offenceIndex++;
            }
            // Always update last offence time
            _userOffenceCounts[key] = offenceIndex;
            _userOffenceTimes[key] = DateTime.UtcNow;

            // Pick the correct offence (if out of range, use last offence)
            AutoModOffence? offence = null;
            if (rule.Offences != null && rule.Offences.Count > 0)
            {
                offence = offenceIndex < rule.Offences.Count ? rule.Offences[offenceIndex] : rule.Offences.Last();
            }
            // Fallback if no offences defined
            if (offence == null)
            {
                offence = new AutoModOffence { Message = "", Action = (int)AutoModOffenceAction.Clear };
            }

            // Cancel speech if rule or offence says so
            if (offence.CancelSpeech || (AutoModOffenceAction)offence.Action == AutoModOffenceAction.Clear)
            {
                _adminLogger.Add(LogType.AdminCommands, LogImpact.High, $"[AutoMod] Cleared speech of user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} (Offence {offenceIndex + 1})");
                args.Cancel();
            }

            switch ((AutoModOffenceAction)offence.Action)
            {
                case AutoModOffenceAction.Clear:
                    // Already handled above
                    break;
                case AutoModOffenceAction.Warn:
                    _adminLogger.Add(LogType.AdminCommands, LogImpact.Medium, $"[AutoMod] Warned user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} (Offence {offenceIndex + 1}) - Reason: {offence.Message}");
                    _chat.ChatMessageToOne(ChatChannel.Server,
                        offence.Message,
                        offence.Message,
                        EntityUid.Invalid,
                        false,
                        args.Sender.Channel);
                    break;
                case AutoModOffenceAction.Kick:
                    string kickReason = string.IsNullOrWhiteSpace(offence.Message)
                        ? "Kicked by AutoMod"
                        : $"Kicked by AutoMod for: {offence.Message}";
                    _adminLogger.Add(LogType.AdminCommands, LogImpact.High, $"[AutoMod] Kicked user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} (Offence {offenceIndex + 1}) - Reason: {kickReason}");
                    _netManager.DisconnectChannel(args.Sender.Channel, kickReason);
                    break;
                case AutoModOffenceAction.Ban:
                    string banReason = string.IsNullOrWhiteSpace(offence.Message)
                        ? "Banned by AutoMod"
                        : $"Banned by AutoMod for: {offence.Message}";
                    uint? duration = null;
                    if (offence.BanDurationMinutes > 0)
                        duration = (uint)offence.BanDurationMinutes;
                    // If 0, treat as permanent ban (duration = null)
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
                    _adminLogger.Add(LogType.AdminCommands, LogImpact.Extreme, $"[AutoMod] Banned user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} (Offence {offenceIndex + 1}) - Reason: {banReason} - Duration: {(duration.HasValue ? duration + " minutes" : "permanent")}");
                    _netManager.DisconnectChannel(args.Sender.Channel, banReason);
                    break;
            }
        }
    }
}
