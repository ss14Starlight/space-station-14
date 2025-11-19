using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.Administration.Notes;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Shared._Starlight.Administration;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Content.Server.Administration.Managers;
using AutoModRule = Content.Server.Database.AutoModRule;
using AutoModOffence = Content.Server.Database.AutoModOffence;

namespace Content.Server.Starlight.Chat.Systems;
public sealed partial class AutoModSystem : SharedChatSystem
{
    #region Dependencies and Fields
    
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly IAdminNotesManager _adminNotesManager = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly Server.Administration.Logs.IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    
    private readonly ISawmill _automodLog = Logger.GetSawmill("automod");
    public const string NotificationChannel = "automod_rules";
    
    private readonly Dictionary<(NetUserId, string), DateTime> _recentMessages = new();
    private List<AutoModRule> _rules = new();
    
    // Compiled regex patterns for parsing AutoMod notes
    private static readonly Regex _offenseLevelRegex = new(@"Offense Level:\[\/color\]\s*\[bold\]\[color=[^\]]+\](\d+)\[\/color\]\[\/bold\]", RegexOptions.Compiled);
    private static readonly Regex _actionTakenRegex = new(@"Action Taken:\[\/color\]\s*\[bold\]\[color=[^\]]+\]([^\[]+)\[\/color\]\[\/bold\]", RegexOptions.Compiled);
    private static readonly Regex _channelRegex = new(@"Channel:\[\/color\]\s*\[color=[^\]]+\]([^\[]+)\[\/color\]", RegexOptions.Compiled);
    private static readonly Regex _categoryRegex = new(@"Category:\[\/color\]\s*\[color=[^\]]+\]([^\[]+)\[\/color\]", RegexOptions.Compiled);
    private static readonly Regex _violatingMessageRegex = new(@"Violating Message:\[\/color\]\[\/bold\]\s*\[color=[^\]]+\]""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex _ruleNameRegex = new(@"╔══ AUTOMOD VIOLATION ══╗\[\/color\]\[\/bold\]\s*\[bold\]\[color=[^\]]+\]([^\[]+)\[\/color\]\[\/bold\]", RegexOptions.Compiled);
    private static readonly Regex _ruleIdRegex = new(@"Rule ID: (\d+)\[\/color\]", RegexOptions.Compiled);
    private static readonly Regex _historySectionRegex = new(@"── Recent History \((\d+) total\) ──\[\/color\](.*?)(?=\[color=#ff4444\]╚|$)", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex _incidentRegex = new(@"Lvl (\d+) \[([^\]]+)\](?:\s*\[color=[^\]]+\]\[DECAYED\]\[\/color\])?\[\/color\] - \[color=[^\]]+\]""([^""]+)""", RegexOptions.Compiled);
    
    #endregion

    #region System Initialization
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChatAttemptEvent>(OnChatAttempt);

        _db.SubscribeToNotifications(async notification =>
        {
            if (notification.Channel == NotificationChannel)
            {
                _automodLog.Info("AutoModSystem received notification. Updating cache.");
                await UpdateCache();
            }
        });

        _automodLog.Info("AutoModSystem initialized. Updating cache.");
        _ = UpdateCache();
        
        // Subscribe to notes retrieval for automatic decay processing
        _adminNotesManager.NotesRetrieved += OnNotesRetrieved;
    }

    public async Task UpdateCache()
    {
        try
        {
            _rules = await _db.GetAutoModRules();
            _automodLog.Info($"AutoMod cache updated successfully. Loaded {_rules.Count} rules.");
        }
        catch (Exception ex)
        {
            _automodLog.Error($"Failed to update AutoMod cache: {ex}");
        }
    }
    
    #endregion

    #region Message Processing

    //watch for chat messages
    private async void OnChatAttempt(ChatAttemptEvent args)
    {
        if (args.Channel is ChatChannel.Admin or ChatChannel.AdminChat or ChatChannel.AdminAlert)
            return;

        string message = args.Message;
        var userKey = args.Sender.UserId;
        var now = DateTime.UtcNow;
        if (_recentMessages.TryGetValue((userKey, "*"), out var recentMessageTime) && (now - recentMessageTime).TotalSeconds < 0.5)
            return;
        _recentMessages[(userKey, "*")] = now;
        var cutoff = now.AddSeconds(-10);
        var keysToRemove = _recentMessages.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();
        foreach (var key in keysToRemove)
            _recentMessages.Remove(key);

        foreach (var rule in _rules)
        {
            if (!rule.Enabled) continue;

            var regex = new Regex(rule.Regex);
            if (!regex.IsMatch(message)) continue;

            int offenceIndex = 0;
            var existingViolations = await GetPlayerAutoModViolations(args.Sender.UserId);
            _automodLog.Debug($"[AutoMod] GetPlayerAutoModViolations returned {existingViolations.Count} total violations for user {args.Sender.UserId}");
            var ruleViolations = existingViolations.Where(v => v.RuleId == rule.Id).ToList();
            _automodLog.Debug($"[AutoMod] Filtered to {ruleViolations.Count} violations for rule {rule.Id}");
            
            if (ruleViolations.Any())
            {
                _automodLog.Debug($"[AutoMod] Found {ruleViolations.Count} total violations for rule {rule.Id}");
                foreach (var rv in ruleViolations)
                {
                    _automodLog.Debug($"[AutoMod] Violation: ViolationCount={rv.ViolationCount}, DecayAfter={rv.DecayAfter}, CreatedAt={rv.CreatedAt}");
                }
                
                var activeViolations = ruleViolations.Where(v => !v.DecayAfter.HasValue || DateTime.UtcNow <= v.DecayAfter.Value).ToList();
                _automodLog.Debug($"[AutoMod] After decay filter: {activeViolations.Count} active violations");
                
                if (activeViolations.Any())
                {
                    var mostRecentViolation = activeViolations.OrderByDescending(v => v.CreatedAt).First();
                    offenceIndex = mostRecentViolation.ViolationCount;
                    _automodLog.Debug($"[AutoMod] Found existing violation for rule {rule.Id}, offense count so far: {mostRecentViolation.ViolationCount}");
                }
            }
            
            _automodLog.Debug($"[AutoMod] Rule {rule.Id} triggered for user {args.Sender.UserId}, offense count so far: {offenceIndex}, next offense index to apply: {offenceIndex}");
            
            int displayLevel;
            AutoModOffence? offence = null;
            bool hasOffences = rule.Offences != null && rule.Offences.Count > 0;
            if (hasOffences)
            {
                offence = (rule.Offences != null && offenceIndex < rule.Offences.Count) ? rule.Offences[offenceIndex] : rule.Offences?.Last();
                displayLevel = offenceIndex + 1;
                _automodLog.Debug($"[AutoMod] Rule {rule.Id}: Applying offense at index {offenceIndex} (will be stored as display level {displayLevel}), action: {offence?.Action}");
            }
            else
            {
                offence = new AutoModOffence { Message = "", Action = (int)AutoModOffenceAction.None };
                displayLevel = 0;
                _automodLog.Debug($"[AutoMod] Rule {rule.Id}: No offences configured, display level 0");
            }

            // Store the next offence level for escalation (never less than 1 if offences exist)
            var storeLevel = hasOffences ? displayLevel : 0;
            if (hasOffences && rule.Offences != null)
                storeLevel = Math.Min(storeLevel, rule.Offences.Count);
            
            try
            {
                await AddOrUpdateAutoModNote(
                    rule,
                    args.Sender.UserId,
                    storeLevel,
                    message,
                    GetChannelDisplayName(args.Channel),
                    (AutoModOffenceAction)(offence?.Action ?? (int)AutoModOffenceAction.None),
                    (offence != null && offence.DecaySeconds > 0) ? DateTime.UtcNow.AddSeconds(offence.DecaySeconds) : null,
                    offence?.DecaySeconds ?? 0,
                    1, // Decay level is always 1 per offense
                    ruleViolations.FirstOrDefault() // Pass existing violation to avoid duplicate DB call
                );
            }
            catch (Exception ex)
            {
                _automodLog.Error($"Failed to create/update AutoMod admin note: {ex}");
            }

            _adminLogger.Add(LogType.AdminCommands, LogImpact.Low, 
                $"[AutoMod Debug] Processing offence with action: {(offence != null ? offence.Action.ToString() : "null")} ({(offence != null ? ((AutoModOffenceAction)offence.Action).ToString() : "null")}) for rule: {rule.Regex}");

            bool messageCleared = offence != null && offence.CancelSpeech;
            if (messageCleared)
            {
                _adminLogger.Add(LogType.AdminCommands, LogImpact.High, 
                    $"[AutoMod] Cleared speech of user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} (Offence {displayLevel}) - Message: \"{message}\"");
                args.Cancel();
            }

            // Pass displayLevel = 0 if no offences, otherwise offenceIndex+1
            var safeOffence = offence ?? new AutoModOffence { Message = "", Action = (int)AutoModOffenceAction.None };
            var adminNotification = FormatAutoModBwoink(rule, safeOffence, displayLevel, message, args.Channel, messageCleared);
            SendAdminOnlyBwoink(args.Sender.UserId, adminNotification);
            HandleOffenceAction(args, rule, safeOffence, displayLevel, message);
        }
    }
    
    #endregion

    #region Action Handling

    /// <summary>
    /// Handles the specific action for an AutoMod offence
    /// </summary>
    private void HandleOffenceAction(ChatAttemptEvent args, AutoModRule rule, AutoModOffence offence, int displayLevel, string message)
    {
        var action = (AutoModOffenceAction)offence.Action;
        var logImpact = action switch
        {
            AutoModOffenceAction.None => LogImpact.Low,
            AutoModOffenceAction.Warn => LogImpact.Medium,
            AutoModOffenceAction.Kick or AutoModOffenceAction.Ban => LogImpact.High,
            _ => LogImpact.Low
        };

        switch (action)
        {
            case AutoModOffenceAction.None:
                _adminLogger.Add(LogType.AdminCommands, logImpact, 
                    $"[AutoMod] Logged violation for user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} (Offence {displayLevel}) - Message: \"{message}\"");
                break;
 
            case AutoModOffenceAction.Warn:
                _adminLogger.Add(LogType.AdminCommands, logImpact, 
                    $"[AutoMod] Warned user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} (Offence {displayLevel}) - Reason: {offence.Message} - Message: \"{message}\"");
                // Show big red warning popup to the player
                if (args.Sender.AttachedEntity != null)
                    _popup.PopupEntity(offence.Message, args.Sender.AttachedEntity.Value, args.Sender.AttachedEntity.Value, PopupType.LargeCaution);
                // Also send a red chat message
                var warnMessage = $"[color=red][bold]AUTOMOD WARNING[/bold][/color]\n[color=orange]{offence.Message}[/color]";
                _chat.ChatMessageToOne(ChatChannel.Server, warnMessage, warnMessage, EntityUid.Invalid, false, args.Sender.Channel);
                break;

            case AutoModOffenceAction.Kick:
                var kickReason = string.IsNullOrWhiteSpace(offence.Message) ? "Kicked by AutoMod" : $"Kicked by AutoMod for: {offence.Message}";
                _adminLogger.Add(LogType.AdminCommands, logImpact, 
                    $"[AutoMod] Kicked user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} (Offence {displayLevel}) - Reason: {kickReason} - Message: \"{message}\"");
                _netManager.DisconnectChannel(args.Sender.Channel, kickReason);
                break;

            case AutoModOffenceAction.Ban:
                var banReason = string.IsNullOrWhiteSpace(offence.Message) ? "Banned by AutoMod" : $"Banned by AutoMod for: {offence.Message}";
                banReason += "\n\nYou may appeal this ban in our discord at: https://discord.com/invite/ssJTANEa";
                uint? duration = offence.BanDurationMinutes > 0 ? (uint)offence.BanDurationMinutes : null;
                _banManager.CreateServerBan(args.Sender.UserId, args.Sender.Name, null, null, null, duration, NoteSeverity.High, banReason);
                _adminLogger.Add(LogType.AdminCommands, logImpact, 
                    $"[AutoMod] Banned user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} (Offence {displayLevel}) - Reason: {banReason} - Duration: {(duration.HasValue ? duration + " minutes" : "permanent")} - Message: \"{message}\"");
                _netManager.DisconnectChannel(args.Sender.Channel, banReason);
                break;
        }
    }
    
    #endregion

    #region Ahelp Notification

    /// <summary>
    /// Formats a concise AutoMod violation bwoink message
    /// </summary>
    private string FormatAutoModBwoink(AutoModRule rule, AutoModOffence offence, int offenceLevel, string violatingMessage, ChatChannel? channel, bool messageCleared)
    {
        var action = (AutoModOffenceAction)offence.Action;
        var channelName = GetChannelDisplayName(channel);
        
        // Determine action color and text
        var (actionText, actionColor) = action switch
        {
            AutoModOffenceAction.None => ("Logged", "white"),
            AutoModOffenceAction.Warn => ("Warn", "yellow"), 
            AutoModOffenceAction.Kick => ("Kick", "orange"),
            AutoModOffenceAction.Ban => (offence.BanDurationMinutes > 0 ? $"Ban ({offence.BanDurationMinutes}m)" : "Ban (Permanent)", "red"),
            _ => ("Unknown", "gray")
        };

        // Determine severity color
        var severityColor = rule.Severity switch
        {
            1 => "yellow",   // Low
            2 => "orange",   // Medium  
            3 => "red",      // High
            4 => "darkred",  // Critical
            _ => "gray"
        };

        var severityText = rule.Severity switch
        {
            1 => "Low",
            2 => "Medium", 
            3 => "High",
            4 => "Critical",
            _ => "Unknown"
        };

        // Determine offence level color
        var levelColor = offenceLevel switch
        {
            0 => "yellow",
            1 => "orange", 
            _ => "red"
        };

        // Build the formatted message
        var result = $"[color=red][bold]AUTOMOD VIOLATION[/bold][/color]\n";
        result += "[color=gray]══════════════════════════════════════[/color]\n";
        result += $"[color=cyan]Channel:[/color] [color=white]{channelName}[/color]\n";
        result += $"[color=cyan]Action Taken:[/color] [color={actionColor}]{actionText}[/color]\n";
        result += $"[color=cyan]Severity:[/color] [color={severityColor}]{severityText}[/color]\n";
        result += $"[color=cyan]Offence Level:[/color] [color={levelColor}]{offenceLevel + 1}[/color]\n";
        result += $"[color=cyan]Category:[/color] [color=yellow]{rule.Category ?? "Uncategorized"}[/color]\n";
        result += $"[color=cyan]Rule Pattern:[/color] [color=gray]{rule.Regex}[/color]\n";
        
        if (!string.IsNullOrEmpty(offence.Message))
            result += $"[color=cyan]Reason:[/color] [color=orange]{offence.Message}[/color]\n";
        
        result += "[color=gray]──────────────────────────────────────[/color]\n";
        result += "[color=cyan][bold]Message:[/bold][/color]\n";
        result += $"[color=white]\"{violatingMessage}\"[/color]";
        
        if (messageCleared)
            result += " [color=red][CLEARED][/color]";
        
        result += "\n[color=gray]──────────────────────────────────────[/color]\n";
        result += $"[color=gray]{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC[/color]";

        return result;
    }

    // Grabs channel name
    // Note: Admin channels should never reach this method due to early filtering
    private static string GetChannelDisplayName(ChatChannel? channel) => channel switch
    {
        ChatChannel.Local => "IC (Local)",
        ChatChannel.Whisper => "IC (Whisper)",
        ChatChannel.Emotes => "IC (Emotes)",
        ChatChannel.Radio => "IC (Radio)",
        ChatChannel.LOOC => "LOOC",
        ChatChannel.OOC => "OOC", 
        ChatChannel.Dead => "Dead",
        // Admin channels should never be processed by AutoMod
        ChatChannel.Admin => "ERROR: Admin channel should not be processed! Please report this.",
        ChatChannel.AdminChat => "ERROR: Admin chat should not be processed! Please report this.",
        ChatChannel.AdminAlert => "ERROR: Admin alert should not be processed! Please report this.",
        null => "Unknown",
        _ => channel.ToString() ?? "Unknown"
    };

    /// <summary>
    /// Sends an admin-only bwoink for rule violations
    /// </summary>
    private void SendAdminOnlyBwoink(NetUserId violatorUserId, string message)
    {
        try
        {
            var bwoinkMessage = new SharedBwoinkSystem.BwoinkTextMessage(
                violatorUserId, 
                new NetUserId(Guid.Empty), // System user ID
                message, 
                playSound: false, 
                adminOnly: true
            );

            RaiseNetworkEvent(bwoinkMessage);
        }
        catch (Exception ex)
        {
            _automodLog.Error($"[AutoMod] Failed to send bwoink: {ex}");
        }
    }
    
    #endregion

    /// <summary>
    /// Represents AutoMod violation data stored in admin notes
    /// </summary>
    private sealed class AutoModViolationData
    {
        public int NoteId { get; set; } // Database note ID for updates
        public string Type => "automod_violation";
        public string UniqueId { get; set; } = Guid.NewGuid().ToString(); // Unique identifier
        public string RulePlayerKey { get; set; } = ""; // Unique key for this rule+player combination
        public int RuleId { get; set; }
        public string RuleName { get; set; } = "";
        public string Category { get; set; } = "";
        public int ViolationCount { get; set; }
        public string CurrentAction { get; set; } = "";
        public string OriginalMessage { get; set; } = "";
        public string Channel { get; set; } = "";
        public string RegexPattern { get; set; } = "";
        public DateTime LastUpdated { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DecayAfter { get; set; }
        public List<ViolationIncident> Incidents { get; set; } = new();
    }

    /// <summary>
    /// Represents a single violation incident within a rule
    /// </summary>
    private sealed class ViolationIncident
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; } = "";
        public string Channel { get; set; } = "";
        public string ActionTaken { get; set; } = "";
        public int OffenseLevel { get; set; }
        public int DecaySeconds { get; set; }
        public int DecayLevel { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsDecayed { get; set; }
    }

    /// <summary>
    /// Simple AdminNote class for internal use
    /// </summary>
    private sealed class AdminNote
    {
        public int Id { get; set; }
        public string Message { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = "";
        public NoteSeverity Severity { get; set; }
        public DateTime? ExpiryTime { get; set; }
        public bool Secret { get; set; }
    }

    #region Admin Notes

    /// <summary>
    /// Helper method to format AutoMod violation data for admin notes
    /// </summary>
    private string FormatViolationMessage(AutoModRule rule, NetUserId playerId, int offenseLevel, string originalMessage, string channel, string action, List<ViolationIncident> incidents, string? existingId = null)
    // Helper to balance BBCode tags for color and bold
    {
        string BalanceTags(string input)
        {
            // Count [color and [/color]
            int openColor = Regex.Matches(input, "\\[color[=\"]?").Count;
            int closeColor = Regex.Matches(input, "\\[/color\\]").Count;
            // Count [bold] and [/bold]
            int openBold = Regex.Matches(input, "\\[bold\\]").Count;
            int closeBold = Regex.Matches(input, "\\[/bold\\]").Count;
            var sb = new StringBuilder(input);
            for (int i = 0; i < openColor - closeColor; i++) sb.Append("[/color]");
            for (int i = 0; i < openBold - closeBold; i++) sb.Append("[/bold]");
            for (int i = 0; i < closeColor - openColor; i++) sb.Insert(0, "[color=white]");
            for (int i = 0; i < closeBold - openBold; i++) sb.Insert(0, "[bold]");
            return sb.ToString();
        }
        var rulePlayerKey = $"automod_{rule.Id}_{playerId}";
        var uniqueId = existingId ?? Guid.NewGuid().ToString();
        // Escape all dynamic fields for BBCode safety
        static string EscapeBB(string s) => s.Replace("[", "\\[").Replace("]", "\\]");
        var ruleName = EscapeBB(rule.Category ?? $"Rule #{rule.Id}");
        var category = EscapeBB(rule.Category ?? "Uncategorized");
        var channelEsc = EscapeBB(channel);
        var actionEsc = EscapeBB(action);
        var regexEsc = EscapeBB(rule.Regex);
        var originalMessageEsc = EscapeBB(originalMessage);
        
        _automodLog.Debug($"[AutoMod] Creating note with RulePlayerKey={rulePlayerKey}, UniqueId={uniqueId}");
        
        // Format note with BBCode for rich formatting - ALL data is editable by admins
        var formattedNote = new StringBuilder();
        
        // Header with rule name
        formattedNote.AppendLine($"[bold][color=#ff4444]╔══ AUTOMOD VIOLATION ══╗[/color][/bold]");
        formattedNote.AppendLine($"[bold][color=#ffaa00]{ruleName}[/color][/bold]");
        formattedNote.AppendLine($"[color=#666666]Rule ID: {rule.Id}[/color]");
        formattedNote.AppendLine($"[color=#888888]────────────────────────[/color]");
        
        // Key details
        var levelColor = offenseLevel switch
        {
            1 => "#00ff00",
            2 => "#ffff00", 
            3 => "#ff8800",
            _ => "#ff0000"
        };
        var actionColor = GetColorForAction(action);
        
        formattedNote.AppendLine($"[color=#00ddff]Offense Level:[/color] [bold][color={levelColor}]{offenseLevel}[/color][/bold]");
        formattedNote.AppendLine($"[color=#00ddff]Action Taken:[/color] [bold][color={actionColor}]{actionEsc}[/color][/bold]");
        formattedNote.AppendLine($"[color=#00ddff]Channel:[/color] [color=#ffffff]{channelEsc}[/color]");
        formattedNote.AppendLine($"[color=#00ddff]Category:[/color] [color=#bb88ff]{category}[/color]");
        
        // Message content
        formattedNote.AppendLine();
        formattedNote.AppendLine($"[bold][color=#00ddff]Violating Message:[/color][/bold]");
        formattedNote.AppendLine($"[color=#ffcccc]\"{originalMessageEsc}\"[/color]");
        
        // Incident history
        if (incidents.Count > 0)
        {
            formattedNote.AppendLine();
            formattedNote.AppendLine($"[color=#888888]── Recent History ({incidents.Count} total) ──[/color]");
            foreach (var incident in incidents.OrderByDescending(i => i.Timestamp).Take(5))
            {
                // Check if this incident has decayed
                var isDecayed = incident.IsDecayed || (incident.ExpiresAt.HasValue && DateTime.UtcNow > incident.ExpiresAt.Value);
                
                // Color based on action severity (dimmed if decayed)
                var incidentColor = GetColorForAction(incident.ActionTaken, isDecayed);
                
                var msgPreview = incident.Message.Length > 40 
                    ? incident.Message[..40] + "..." 
                    : incident.Message;
                
                var escapedPreview = EscapeBB(msgPreview);
                var escapedAction = EscapeBB(incident.ActionTaken);
                
                // Build decay status tag
                var decayTag = "";
                if (isDecayed)
                {
                    decayTag = " [color=#666666]\\[DECAYED\\][/color]";
                }
                else if (incident.ExpiresAt.HasValue)
                {
                    var timeRemaining = incident.ExpiresAt.Value - DateTime.UtcNow;
                    if (timeRemaining.TotalSeconds > 0)
                    {
                        var timeStr = timeRemaining.TotalHours >= 1 
                            ? $"{timeRemaining.TotalHours:F1}h"
                            : timeRemaining.TotalMinutes >= 1
                            ? $"{timeRemaining.TotalMinutes:F0}m"
                            : $"{timeRemaining.TotalSeconds:F0}s";
                        decayTag = $" [color=#888888]\\[Decays by: {incident.DecayLevel} on {timeStr}\\][/color]";
                    }
                }
                    
                formattedNote.AppendLine($"[color={incidentColor}]  • Lvl {incident.OffenseLevel} \\[{escapedAction}\\]{decayTag}[/color] - [color=#cccccc]\"{escapedPreview}\"[/color]");
            }
            if (incidents.Count > 5)
                formattedNote.AppendLine($"[color=#666666]  ... and {incidents.Count - 5} more incidents[/color]");
        }
        
        // Footer with unique identifier ("DO NOT EDIT" - used to track this specific note)
        formattedNote.AppendLine();
        formattedNote.AppendLine($"[color=#ff4444]╚══════════════════════╝[/color]");
        formattedNote.AppendLine($"[color=#444444]────────────────────────[/color]");
        formattedNote.Append($"[color=#333333]AUTOMOD_ID:{rulePlayerKey}:{uniqueId}[/color]");
        
        // Ensure BBCode tags are balanced before returning
        return BalanceTags(formattedNote.ToString());
    }

    // Add a dictionary to centralize color mappings
    private static readonly Dictionary<string, string> _actionColorMap = new()
    {
        { "ban", "#ff0000" },      // Red for bans
        { "kick", "#ff8800" },     // Orange for kicks
        { "warn", "#ffff00" },     // Yellow for warnings
        { "none", "#00ff00" },     // Green for no action
        { "default", "#88aaff" }   // Blue for other/unknown
    };

    // Add a method to get color from the dictionary
    private static string GetColorForAction(string action, bool isDecayed = false)
    {
        if (isDecayed)
            return "#444444"; // Dimmed color for decayed incidents

        return _actionColorMap.TryGetValue(action.ToLower(), out var color) ? color : _actionColorMap["default"];
    }

    /// <summary>
    /// Finds an existing AutoMod violation note for the given rule and player
    /// </summary>
    private async Task<AdminNote?> FindExistingAutoModNote(int ruleId, NetUserId playerId)
    {
        try
        {
            var notes = await _adminNotesManager.GetAllAdminRemarks(playerId.UserId);
            var rulePlayerKey = $"automod_{ruleId}_{playerId}";
            _automodLog.Debug($"[AutoMod] Searching for RulePlayerKey={rulePlayerKey}");
            foreach (var note in notes)
            {
                if (note is AdminNoteRecord adminNote)
                {
                    var simpleNote = new AdminNote
                    {
                        Id = adminNote.Id,
                        Message = adminNote.Message,
                        CreatedAt = adminNote.CreatedAt.DateTime,
                        CreatedBy = adminNote.CreatedBy?.LastSeenUserName ?? "System",
                        Severity = adminNote.Severity,
                        ExpiryTime = adminNote.ExpirationTime?.DateTime,
                        Secret = adminNote.Secret
                    };
                    var data = ExtractViolationData(simpleNote);
                    if (data != null)
                    {
                        _automodLog.Debug($"[AutoMod] Note {adminNote.Id} has RulePlayerKey={data.RulePlayerKey}");
                        if (data.RulePlayerKey == rulePlayerKey)
                        {
                            _automodLog.Debug($"[AutoMod] Found existing note for RulePlayerKey={rulePlayerKey} (noteId={adminNote.Id})");
                            return simpleNote;
                        }
                    }
                }
            }
            _automodLog.Debug($"[AutoMod] No existing note found for RulePlayerKey={rulePlayerKey}");
            return null;
        }
        catch (Exception ex)
        {
            _automodLog.Error($"Failed to find existing AutoMod note for rule {ruleId}: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Extracts AutoMod violation data from an existing admin note by parsing the formatted text
    /// </summary>
    private AutoModViolationData? ExtractViolationData(AdminNote note)
    {
        try
        {
            var msg = note.Message;
            const string Marker = "AUTOMOD_ID:";
            var idx = msg.IndexOf(Marker);
            if (idx == -1)
                return null;
            
            var afterMarker = msg[(idx + Marker.Length)..].Trim();
            var idParts = afterMarker.Split(':');
            if (idParts.Length < 2)
                return null;
                
            var rulePlayerKey = idParts[0];
            var uniqueId = idParts[1];
            
            // Parse offense level (displayed as 1-based, stored as 0-based index)
            var offenseLevel = 1; // Default display level
            var offenseLevelMatch = _offenseLevelRegex.Match(msg);
            if (offenseLevelMatch.Success)
                int.TryParse(offenseLevelMatch.Groups[1].Value, out offenseLevel);
            
            _automodLog.Debug($"[AutoMod] ExtractViolationData: Parsed offense level (display): {offenseLevel}");
            
            // Parse action
            var action = "Unknown";
            var actionMatch = _actionTakenRegex.Match(msg);
            if (actionMatch.Success)
                action = actionMatch.Groups[1].Value.Trim();
            
            // Parse channel
            var channel = "Unknown";
            var channelMatch = _channelRegex.Match(msg);
            if (channelMatch.Success)
                channel = channelMatch.Groups[1].Value.Trim();
            
            // Parse category
            var category = "Uncategorized";
            var categoryMatch = _categoryRegex.Match(msg);
            if (categoryMatch.Success)
                category = categoryMatch.Groups[1].Value.Trim();
            
            // Parse violating message
            var originalMessage = "";
            var messageMatch = _violatingMessageRegex.Match(msg);
            if (messageMatch.Success)
                originalMessage = messageMatch.Groups[1].Value;
            
            // Parse rule name
            var ruleName = category;
            var ruleNameMatch = _ruleNameRegex.Match(msg);
            if (ruleNameMatch.Success)
                ruleName = ruleNameMatch.Groups[1].Value.Trim();
            
            // Extract rule ID from rulePlayerKey (format: automod_RULEID_PLAYERID)
            var ruleId = 0;
            var keyParts = rulePlayerKey.Split('_');
            if (keyParts.Length >= 2)
                int.TryParse(keyParts[1], out ruleId);
            
            // Parse Rule ID from note text (if available)
            var ruleIdMatch = _ruleIdRegex.Match(msg);
            if (ruleIdMatch.Success && int.TryParse(ruleIdMatch.Groups[1].Value, out var parsedRuleId))
                ruleId = parsedRuleId;
            
            _automodLog.Debug($"[AutoMod] ExtractViolationData: Parsed Rule ID: {ruleId}");
            
            // Parse incidents
            var incidents = new List<ViolationIncident>();
            var historySection = _historySectionRegex.Match(msg);
            if (historySection.Success)
            {
                var historyText = historySection.Groups[2].Value;
                var incidentMatches = _incidentRegex.Matches(historyText);
                foreach (Match incidentMatch in incidentMatches)
                {
                    if (int.TryParse(incidentMatch.Groups[1].Value, out var level))
                    {
                        // Check if this incident line contains [DECAYED]
                        var incidentLine = incidentMatch.Value;
                        var isDecayed = incidentLine.Contains("[DECAYED]");
                        
                        incidents.Add(new ViolationIncident
                        {
                            OffenseLevel = level,
                            ActionTaken = incidentMatch.Groups[2].Value.Trim(),
                            Message = incidentMatch.Groups[3].Value,
                            Timestamp = DateTime.UtcNow,
                            Channel = channel,
                            DecaySeconds = 0,
                            DecayLevel = 1,
                            ExpiresAt = note.ExpiryTime,
                            IsDecayed = isDecayed
                        });
                    }
                }
            }
            
            var violationData = new AutoModViolationData
            {
                NoteId = note.Id,
                UniqueId = uniqueId,
                RulePlayerKey = rulePlayerKey,
                RuleId = ruleId,
                RuleName = ruleName,
                Category = category,
                ViolationCount = offenseLevel,
                CurrentAction = action,
                OriginalMessage = originalMessage,
                Channel = channel,
                RegexPattern = "",
                LastUpdated = DateTime.UtcNow,
                CreatedAt = note.CreatedAt,
                DecayAfter = note.ExpiryTime,
                Incidents = incidents
            };
            
            _automodLog.Debug($"[AutoMod] ExtractViolationData: Parsed offense level (display): {offenseLevel}, stored as ViolationCount: {violationData.ViolationCount}");
            
            return violationData;
        }
        catch (Exception ex)
        {
            _automodLog.Warning($"Failed to extract AutoMod violation data from note {note.Id}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Adds a new AutoMod violation note or updates an existing one for this rule+player combination
    /// </summary>
    private async Task AddOrUpdateAutoModNote(AutoModRule rule, NetUserId playerId, int newOffenseLevel, string message, string channel, AutoModOffenceAction action, DateTime? decayAfter, int decaySeconds, int decayLevel, AutoModViolationData? existingViolation = null)
    {
        int retryCount = 0;
        retry:
        try
        {
            // Ensure the noted player exists in the DB to satisfy FK constraints
            if (await _db.GetPlayerRecordByUserId(playerId) is null)
            {
                _automodLog.Warning($"Skipping AutoMod note: no player record found for {playerId} (FK constraint).");
                return;
            }

            var severity = action switch
            {
                AutoModOffenceAction.None => NoteSeverity.Minor,
                AutoModOffenceAction.Warn or AutoModOffenceAction.Kick or AutoModOffenceAction.Ban => NoteSeverity.High,
                _ => NoteSeverity.Medium
            };
            
            // Get current round ID and player playtime
            int? roundId = _gameTicker == null || _gameTicker.RoundId == 0 ? null : _gameTicker.RoundId;
            var playtime = (await _db.GetPlayTimes(playerId)).Find(p => p.Tracker == PlayTimeTrackingShared.TrackerOverall)?.TimeSpent ?? TimeSpan.Zero;

            // If we have cached violation data, we know there's an existing note to update
            if (existingViolation != null)
            {
                var existingData = existingViolation;
                if (existingData != null)
                {
                    _automodLog.Debug($"[AutoMod] UPDATING existing note {existingData.NoteId}, old ViolationCount={existingData.ViolationCount}, new ViolationCount={newOffenseLevel}");
                    
                    // Mark expired incidents as decayed
                    var currentTime = DateTime.UtcNow;
                    foreach (var incident in existingData.Incidents)
                    {
                        if (incident.ExpiresAt.HasValue && currentTime > incident.ExpiresAt.Value && !incident.IsDecayed)
                        {
                            incident.IsDecayed = true;
                            _automodLog.Debug($"[AutoMod] Marking incident (Lvl {incident.OffenseLevel}) as DECAYED");
                        }
                    }
                    
                    existingData.Incidents.Add(new ViolationIncident
                    {
                        Timestamp = DateTime.UtcNow,
                        Message = message,
                        Channel = channel,
                        ActionTaken = action.ToString(),
                        OffenseLevel = newOffenseLevel,
                        DecaySeconds = decaySeconds,
                        DecayLevel = decayLevel,
                        ExpiresAt = decaySeconds > 0 ? DateTime.UtcNow.AddSeconds(decaySeconds) : null
                    });
                    // Use the newOffenseLevel that was calculated based on the previous note
                    existingData.ViolationCount = newOffenseLevel;
                    existingData.CurrentAction = action.ToString();
                    existingData.OriginalMessage = message;
                    existingData.Channel = channel;
                    existingData.LastUpdated = DateTime.UtcNow;
                    if (decayAfter.HasValue) existingData.DecayAfter = decayAfter;
                    
                    var updatedMessage = FormatViolationMessage(rule, playerId, existingData.ViolationCount, message, channel, action.ToString(), existingData.Incidents, existingData.UniqueId);
                    _automodLog.Debug($"[AutoMod] Calling EditAdminNote for note {existingData.NoteId} with updated offense level {existingData.ViolationCount}");
                    _automodLog.Debug($"[AutoMod] Updated message preview (first 500 chars): {updatedMessage.Substring(0, Math.Min(500, updatedMessage.Length))}");
                    _automodLog.Debug($"[AutoMod] Updated message length: {updatedMessage.Length} chars, contains 'Offense Level:' at position {updatedMessage.IndexOf("Offense Level:")}");
                    await _db.EditAdminNote(existingData.NoteId, updatedMessage, severity, rule.Secret, Guid.Empty, DateTimeOffset.UtcNow, decayAfter?.ToUniversalTime());
                    _automodLog.Debug($"[AutoMod] Successfully updated note {existingData.NoteId}, now re-querying to verify...");
                    
                    // Verify the update by re-reading the note
                    var verifyNote = await _db.GetAdminNote(existingData.NoteId);
                    if (verifyNote != null)
                    {
                        _automodLog.Debug($"[AutoMod] Verification: Note {existingData.NoteId} message length: {verifyNote.Message.Length}, contains 'Offense Level:' at position {verifyNote.Message.IndexOf("Offense Level:")}");
                        var verifyData = ExtractViolationData(new AdminNote { Id = verifyNote.Id, Message = verifyNote.Message, CreatedAt = verifyNote.CreatedAt.DateTime, CreatedBy = "System", Severity = verifyNote.Severity, ExpiryTime = verifyNote.ExpirationTime?.DateTime, Secret = verifyNote.Secret });
                        if (verifyData != null)
                        {
                            _automodLog.Debug($"[AutoMod] Verification: Parsed ViolationCount = {verifyData.ViolationCount}");
                        }
                    }
                    return;
                }
            }
            
            _automodLog.Debug($"[AutoMod] CREATING new note with offense level {newOffenseLevel}");
            var now = DateTime.UtcNow;
            var incidents = new List<ViolationIncident> { new() { 
                Timestamp = now, 
                Message = message, 
                Channel = channel, 
                ActionTaken = action.ToString(), 
                OffenseLevel = newOffenseLevel,
                DecaySeconds = decaySeconds,
                DecayLevel = decayLevel,
                ExpiresAt = decaySeconds > 0 ? now.AddSeconds(decaySeconds) : null
            } };
            var noteMessage = FormatViolationMessage(rule, playerId, newOffenseLevel, message, channel, action.ToString(), incidents, null);
            await _db.AddAdminNote(roundId, playerId, playtime, noteMessage, severity, rule.Secret, Guid.Empty, DateTimeOffset.UtcNow, decayAfter?.ToUniversalTime());
        }
        catch (Exception ex)
        {
            // If we hit a UNIQUE constraint error, retry as an update
            if (ex is Microsoft.EntityFrameworkCore.DbUpdateException dbEx && dbEx.InnerException is Microsoft.Data.Sqlite.SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19 && retryCount < 2)
            {
                retryCount++;
                _automodLog.Warning($"[AutoMod] AddAdminNote hit UNIQUE constraint, retrying as update (attempt {retryCount})");
                await Task.Delay(30); // Small delay to allow DB to update
                goto retry;
            }
            _automodLog.Error($"Failed to add/update AutoMod note for rule {rule.Id}: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Process AutoMod decay when notes are retrieved (integrated into admin notes system)
    /// </summary>
    private async Task OnNotesRetrieved(Guid playerId, List<IAdminRemarksRecord> notes)
    {
        var now = DateTime.UtcNow;
        try
        {
            foreach (var note in notes)
            {
                if (note is not AdminNoteRecord adminNote || !adminNote.Message.Contains("AUTOMOD_ID:"))
                    continue;
                
                var simpleNote = new AdminNote
                {
                    Id = adminNote.Id,
                    Message = adminNote.Message,
                    CreatedAt = adminNote.CreatedAt.DateTime,
                    CreatedBy = adminNote.CreatedBy?.LastSeenUserName ?? "System",
                    Severity = adminNote.Severity,
                    ExpiryTime = adminNote.ExpirationTime?.DateTime,
                    Secret = adminNote.Secret
                };
                
                var violationData = ExtractViolationData(simpleNote);
                if (violationData == null)
                    continue;
                
                // Find expired incidents
                var expiredIncidents = violationData.Incidents
                    .Where(i => i.ExpiresAt.HasValue && now >= i.ExpiresAt.Value)
                    .ToList();
                
                if (expiredIncidents.Count == 0)
                    continue;
                
                // Calculate total decay level from expired incidents
                var totalDecayLevel = expiredIncidents.Sum(i => i.DecayLevel);
                
                // Remove expired incidents
                violationData.Incidents = violationData.Incidents
                    .Where(i => !i.ExpiresAt.HasValue || now < i.ExpiresAt.Value)
                    .ToList();
                
                // Update violation count
                violationData.ViolationCount = Math.Max(0, violationData.ViolationCount - totalDecayLevel);
                violationData.LastUpdated = now;
                
                _automodLog.Info($"[AutoMod] Decayed {totalDecayLevel} offense(s) from note {adminNote.Id} for player {new NetUserId(playerId)}. New count: {violationData.ViolationCount}");
                
                // If no incidents left, mark note as expired/deleted
                if (violationData.Incidents.Count == 0)
                {
                    await _db.DeleteAdminNote(adminNote.Id, Guid.Empty, DateTimeOffset.UtcNow);
                    _automodLog.Info($"[AutoMod] Deleted empty note {adminNote.Id} for player {new NetUserId(playerId)}");
                    continue;
                }
                
                // Update the note with new data
                var updatedMessage = FormatViolationMessage(
                    new AutoModRule { Id = violationData.RuleId, Category = violationData.RuleName, Regex = violationData.RegexPattern },
                    new NetUserId(playerId),
                    violationData.ViolationCount,
                    violationData.OriginalMessage,
                    violationData.Channel,
                    violationData.CurrentAction,
                    violationData.Incidents,
                    violationData.UniqueId
                );
                
                await _db.EditAdminNote(
                    adminNote.Id,
                    updatedMessage,
                    adminNote.Severity,
                    adminNote.Secret,
                    Guid.Empty,
                    DateTimeOffset.UtcNow,
                    violationData.DecayAfter?.ToUniversalTime()
                );
            }
        }
        catch (Exception ex)
        {
            _automodLog.Error($"[AutoMod] Failed to process decay for player {new NetUserId(playerId)}: {ex}");
        }
    }

    #endregion

    /// <summary>
    /// Gets all AutoMod violations for a player from admin notes
    /// </summary>
    private async Task<List<AutoModViolationData>> GetPlayerAutoModViolations(NetUserId userId)
    {
        var violations = new List<AutoModViolationData>();
        try
        {
            var notes = await _adminNotesManager.GetAllAdminRemarks(userId.UserId);
            var now = DateTime.UtcNow;
            
            foreach (var note in notes)
            {
                if (note is not AdminNoteRecord adminNote || !adminNote.Message.Contains("AUTOMOD_ID:"))
                    continue;
                
                var simpleNote = new AdminNote
                {
                    Id = adminNote.Id,
                    Message = adminNote.Message,
                    CreatedAt = adminNote.CreatedAt.DateTime,
                    CreatedBy = adminNote.CreatedBy?.LastSeenUserName ?? "System",
                    Severity = adminNote.Severity,
                    ExpiryTime = adminNote.ExpirationTime?.DateTime,
                    Secret = adminNote.Secret
                };
                
                var violationData = ExtractViolationData(simpleNote);
                if (violationData != null)
                {
                    _automodLog.Debug($"[AutoMod] GetPlayerAutoModViolations: Found violation for RuleId={violationData.RuleId}, ViolationCount={violationData.ViolationCount}, DecayAfter={violationData.DecayAfter}");
                    if (violationData.DecayAfter.HasValue && now > violationData.DecayAfter.Value)
                    {
                        _automodLog.Debug($"[AutoMod] GetPlayerAutoModViolations: Violation has decayed (DecayAfter={violationData.DecayAfter.Value}, now={now})");
                        continue;
                    }
                    violations.Add(violationData);
                }
            }
        }
        catch (Exception ex)
        {
            _automodLog.Error($"Failed to get AutoMod violations for user {userId}: {ex}");
        }
        return violations;
    }

    #region Statistics

    /// <summary>
    /// Gets AutoMod violation statistics for display in admin panels
    /// </summary>
    public async Task<(int rulesBroken, int totalOffences)> GetAutoModStatistics(NetUserId userId)
    {
        try
        {
            var violations = await GetPlayerAutoModViolations(userId);
            
            // Number of distinct rules that have active violations
            var rulesBroken = violations.Select(v => v.RuleId).Distinct().Count();
            
            // Sum of all incidents across all violation notes
            var totalOffences = violations.Sum(v => v.Incidents?.Count ?? 1);
            
            return (rulesBroken, totalOffences);
        }
        catch (Exception ex)
        {
            _automodLog.Error($"Failed to get AutoMod statistics for user {userId}: {ex}");
            return (0, 0);
        }
    }
    
    #endregion
}
