using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.Administration.Notes;
using Content.Server.GameTicking;
using Content.Shared._Starlight.Administration;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Server.Administration.Managers;
using AutoModRule = Content.Server.Database.AutoModRule;
using AutoModOffence = Content.Server.Database.AutoModOffence;

namespace Content.Server.Starlight.Chat.Systems;
public sealed partial class AutoModSystem : SharedChatSystem
{
    #region Dependencys + Fields
    
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly IAdminNotesManager _adminNotesManager = default!;
    [Dependency] private readonly Server.Administration.Logs.IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    
    private readonly ISawmill _automodLog = Logger.GetSawmill("automod");
    public const string NotificationChannel = "automod_rules";
    
    private readonly Dictionary<(NetUserId, string), DateTime> _recentMessages = new();
    private List<AutoModRule> _rules = new();
    private readonly Dictionary<int, Regex> _compiledRegexCache = new();
    private readonly Dictionary<int, DateTime> _lastDecayProcessed = new();
    private static readonly TimeSpan _decayCheckInterval = TimeSpan.FromSeconds(20);
    
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
        
        // Start background decay timer to process active players
        Robust.Shared.Timing.Timer.SpawnRepeating(_decayCheckInterval, ProcessBackgroundDecay, CancellationToken.None);
        _automodLog.Info($"AutoMod background decay timer started (interval: {_decayCheckInterval.TotalSeconds}s)");
    }

    public async Task UpdateCache()
    {
        try
        {
            _rules = await _db.GetAutoModRules();
            _automodLog.Info($"AutoMod cache updated successfully. Loaded {_rules.Count} rules.");
            
            // Clear and rebuild regex cache when rules update
            _compiledRegexCache.Clear();
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
        
        // Only cleanup every 10th message to reduce overhead
        if (_recentMessages.Count > 100)
        {
            var cutoff = now.AddSeconds(-10);
            // Use List to avoid LINQ allocations
            var keysToRemove = new List<(NetUserId, string)>();
            foreach (var kvp in _recentMessages)
            {
                if (kvp.Value < cutoff)
                    keysToRemove.Add(kvp.Key);
            }
            foreach (var key in keysToRemove)
                _recentMessages.Remove(key);
        }

        foreach (var rule in _rules)
        {
            if (!rule.Enabled) continue;

            // Use cached compiled regex for performance
            if (!_compiledRegexCache.TryGetValue(rule.Id, out var regex))
            {
                regex = new Regex(rule.Regex, RegexOptions.Compiled);
                _compiledRegexCache[rule.Id] = regex;
            }
            if (!regex.IsMatch(message)) continue;

            int offenceIndex = 0;
            var existingViolations = await GetPlayerAutoModViolations(args.Sender.UserId);
            
            AutoModViolationData? mostRecentViolation = null;
            foreach (var v in existingViolations)
            {
                if (v.RuleId == rule.Id && (mostRecentViolation == null || v.CreatedAt > mostRecentViolation.CreatedAt))
                    mostRecentViolation = v;
            }
            
            if (mostRecentViolation != null)
                offenceIndex = mostRecentViolation.ViolationCount;
            
            int offenseConfigLevel;
            AutoModOffence? offence;
            bool hasOffences = rule.Offences != null && rule.Offences.Count > 0;
            if (hasOffences)
            {
                var cappedOffenceIndex = Math.Min(offenceIndex, rule.Offences!.Count - 1);
                offence = rule.Offences[cappedOffenceIndex];
                offenseConfigLevel = cappedOffenceIndex + 1;
            }
            else
            {
                offence = new AutoModOffence { Message = "", Action = (int)AutoModOffenceAction.None };
                offenseConfigLevel = 0;
            }

            // Store which offense config level was applied (e.g., Level 1 = Warn, Level 2 = Ban)
            var storeLevel = hasOffences ? offenseConfigLevel : 0;
            
            string cleanedMessage = message; // Default to raw message if note fails
            try
            {
                cleanedMessage = await AddOrUpdateAutoModNote(
                    rule,
                    args.Sender.UserId,
                    storeLevel,
                    message,
                    GetChannelDisplayName(args.Channel),
                    (AutoModOffenceAction)(offence?.Action ?? (int)AutoModOffenceAction.None),
                    offence?.DecaySeconds ?? 0,
                    offence?.DecayLevels ?? 1, // Use configured decay level from offense
                    mostRecentViolation
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
                    $"[AutoMod] Cleared speech of user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} (Offence {offenseConfigLevel}) - Message: \"{message}\"");
                args.Cancel();
            }

            // Pass the offense config level (which configured offense was applied)
            var safeOffence = offence ?? new AutoModOffence { Message = "", Action = (int)AutoModOffenceAction.None };
            var adminNotification = FormatAutoModBwoink(rule, safeOffence, offenseConfigLevel, cleanedMessage, args.Channel, messageCleared);
            SendAdminOnlyBwoink(args.Sender.UserId, adminNotification);
            HandleOffenceAction(args, rule, safeOffence, offenseConfigLevel, message);
        }
    }
    
    #endregion

    #region Action Handling

    /// <summary>
    /// Handles the specific action for an AutoMod offence
    /// </summary>
    private void HandleOffenceAction(ChatAttemptEvent args, AutoModRule rule, AutoModOffence offence, int offenseConfigLevel, string message)
    {
        var action = (AutoModOffenceAction)offence.Action;
        var logImpact = action is AutoModOffenceAction.Kick or AutoModOffenceAction.Ban ? LogImpact.High : 
                       action is AutoModOffenceAction.Warn ? LogImpact.Medium : LogImpact.Low;
        var userName = $"{args.Sender.Name} ({args.Sender.UserId})";
        var ruleInfo = $"rule: {rule.Regex} (Offence {offenseConfigLevel})";

        switch (action)
        {
            case AutoModOffenceAction.None:
                _adminLogger.Add(LogType.AdminCommands, logImpact, $"[AutoMod] Logged violation for {userName} for {ruleInfo} - Message: \"{message}\"");
                break;
 
            case AutoModOffenceAction.Warn:
                _adminLogger.Add(LogType.AdminCommands, logImpact, $"[AutoMod] Warned {userName} for {ruleInfo} - Reason: {offence.Message} - Message: \"{message}\"");;
                _chat.ChatMessageToOne(ChatChannel.Server, $"[color=red][bold]AUTOMOD WARNING[/bold][/color]\n[color=orange]{offence.Message}[/color]", 
                    $"[color=red][bold]AUTOMOD WARNING[/bold][/color]\n[color=orange]{offence.Message}[/color]", EntityUid.Invalid, false, args.Sender.Channel);
                break;

            case AutoModOffenceAction.Kick:
                var kickReason = string.IsNullOrWhiteSpace(offence.Message) ? "Kicked by AutoMod" : $"Kicked by AutoMod for: {offence.Message}";
                _adminLogger.Add(LogType.AdminCommands, logImpact, $"[AutoMod] Kicked {userName} for {ruleInfo} - Reason: {kickReason} - Message: \"{message}\"");;
                _netManager.DisconnectChannel(args.Sender.Channel, kickReason);
                break;

            case AutoModOffenceAction.Ban:
                var banReason = (string.IsNullOrWhiteSpace(offence.Message) ? "Banned by AutoMod" : $"Banned by AutoMod for: {offence.Message}") + 
                    "\n\nYou may appeal this ban in our discord at: https://discord.com/invite/ssJTANEa";
                uint? duration = offence.BanDurationMinutes > 0 ? (uint)offence.BanDurationMinutes : null;
                _banManager.CreateServerBan(args.Sender.UserId, args.Sender.Name, null, null, null, duration, NoteSeverity.High, banReason);
                _adminLogger.Add(LogType.AdminCommands, logImpact, 
                    $"[AutoMod] Banned {userName} for {ruleInfo} - Duration: {(duration.HasValue ? duration + " minutes" : "permanent")} - Message: \"{message}\"");
                _netManager.DisconnectChannel(args.Sender.Channel, banReason);
                break;
        }
    }
    
    #endregion

    #region Ahelp Notification

    /// <summary>
    /// Formats a concise AutoMod violation bwoink message
    /// </summary>
    private string FormatAutoModBwoink(AutoModRule rule, AutoModOffence offence, int offenceLevel, string cleanedMessage, ChatChannel? channel, bool messageCleared)
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
        var (severityText, severityColor) = rule.Severity switch
        {
            1 => ("Low", "yellow"),
            2 => ("Medium", "orange"), 
            3 => ("High", "red"),
            4 => ("Critical", "darkred"),
            _ => ("Unknown", "gray")
        };
        var levelColor = offenceLevel switch { 0 => "yellow", 1 => "orange", _ => "red" };

        // Build formatted message
        var result = $"[color=red][bold]AUTOMOD VIOLATION[/bold][/color]\n";
        result += "[color=gray]══════════════════════════════════════[/color]\n";
        result += $"[color=cyan]Channel:[/color] [color=white]{channelName}[/color]\n";
        result += $"[color=cyan]Action Taken:[/color] [color={actionColor}]{actionText}[/color]\n";
        result += $"[color=cyan]Severity:[/color] [color={severityColor}]{severityText}[/color]\n";
        result += $"[color=cyan]Offence Level:[/color] [color={levelColor}]{offenceLevel}[/color]\n";
        result += $"[color=cyan]Category:[/color] [color=yellow]{rule.Category ?? "Uncategorized"}[/color]\n";
        result += $"[color=cyan]Rule Pattern:[/color] [color=gray]{rule.Regex}[/color]\n";
        
        if (!string.IsNullOrEmpty(offence.Message))
            result += $"[color=cyan]Reason:[/color] [color=orange]{offence.Message}[/color]\n";
        
        result += "[color=gray]──────────────────────────────────────[/color]\n";
        result += "[color=cyan][bold]Message:[/bold][/color]\n";
        result += $"[color=white]\"{cleanedMessage}\"[/color]";
        
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
    /// Parses a human-readable decay time string (e.g., "1.5h", "30m", "60s") into seconds
    /// </summary>
    private static bool TryParseDecayTime(string decayStr, out int seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(decayStr))
            return false;

        decayStr = decayStr.Trim();
        
        // Check for unit suffix
        char unit = decayStr[^1];
        string numberPart = decayStr[..^1];
        
        if (!double.TryParse(numberPart, out var value))
            return false;

        seconds = unit switch
        {
            'd' => (int)(value * 86400), // days to seconds
            'h' => (int)(value * 3600),  // hours to seconds
            'm' => (int)(value * 60),    // minutes to seconds
            's' => (int)value,           // already in seconds
            _ => 0
        };

        return seconds > 0;
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
    /// Helper method to format AutoMod violation data for admin notes as plain text
    /// </summary>
    private string FormatViolationMessage(AutoModRule rule, NetUserId playerId, int offenseLevel, string channel, string action, List<ViolationIncident> incidents, string? existingId = null)
    {
        var rulePlayerKey = $"automod_{rule.Id}_{playerId}";
        var uniqueId = existingId ?? Guid.NewGuid().ToString();
        var ruleName = rule.Category ?? $"Rule #{rule.Id}";
        
        // Format note as plain text
        var note = new StringBuilder();
        
        // Header
        note.AppendLine("╔══ AUTOMOD VIOLATION ══╗");
        note.AppendLine($"Rule: {ruleName} (ID: {rule.Id})");
        note.AppendLine();
        
        // Key details (plain text, UI will colorize based on values)
        note.AppendLine($"Offense Level: {offenseLevel}");
        note.AppendLine($"Action Taken: {action}");
        note.AppendLine($"Channel: {channel}");
        
        // Incident history
        if (incidents.Count > 0)
        {
            note.AppendLine();
            var activeCount = incidents.Count(i => !i.IsDecayed);
            var decayedCount = incidents.Count - activeCount;
            note.AppendLine($"── Offense History ({activeCount} active, {decayedCount} decayed, {incidents.Count} total) ──");
            
            // Show ALL incidents - admins need to see full history
            var orderedIncidents = incidents.OrderByDescending(i => i.Timestamp).ToList();
            
            for (int i = 0; i < orderedIncidents.Count; i++)
            {
                var incident = orderedIncidents[i];
                var incidentNum = incidents.Count - i;
                
                note.Append($"#{incidentNum}: ");
                note.Append($"Level {incident.OffenseLevel} | {incident.ActionTaken}");
                
                if (incident.DecaySeconds > 0)
                {
                    // Convert decay seconds to human-readable format
                    var seconds = incident.DecaySeconds;
                    string decayStr;
                    if (seconds >= 86400) // 1 day or more
                        decayStr = $"{seconds / 86400.0:F1}d";
                    else if (seconds >= 3600) // 1 hour or more
                        decayStr = $"{seconds / 3600.0:F1}h";
                    else if (seconds >= 60) // 1 minute or more
                        decayStr = $"{seconds / 60.0:F0}m";
                    else
                        decayStr = $"{seconds}s";
                    
                    note.Append($" | Decay: {decayStr}, by {incident.DecayLevel}");
                }
                
                if (incident.IsDecayed)
                    note.AppendLine(" | [DECAYED]");
                else
                    note.AppendLine(" | [ACTIVE]");
                    
                // Message already cleaned when stored - just wrap in quotes
                note.AppendLine($"  \"{incident.Message}\"");
            }
        }
        
        // Footer with metadata
        note.AppendLine();
        note.AppendLine("╚══════════════════════╝");
        note.Append($"Metadata: RulePlayerKey={rulePlayerKey}, UniqueId={uniqueId}");
        
        return note.ToString();
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
                    
                    if (ExtractViolationData(simpleNote)?.RulePlayerKey == rulePlayerKey)
                        return simpleNote;
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            _automodLog.Error($"Failed to find existing AutoMod note for rule {ruleId}: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Extracts AutoMod violation data from an existing admin note by parsing plain text format
    /// </summary>
    private AutoModViolationData? ExtractViolationData(AdminNote note)
    {
        try
        {
            var msg = note.Message;
            
            // Check if this is an AutoMod note by looking for metadata line
            var metadataLineIndex = msg.IndexOf("Metadata:");
            if (metadataLineIndex == -1)
                return null;
            
            // Parse metadata line: "Metadata: RulePlayerKey=X, UniqueId=Y"
            var metadataLine = msg[metadataLineIndex..].Split('\n')[0];
            var metadataParts = metadataLine.Replace("Metadata:", "").Trim().Split(',');
            
            string? rulePlayerKey = null;
            string? uniqueId = null;
            
            foreach (var part in metadataParts)
            {
                var keyValue = part.Trim().Split('=', 2);
                if (keyValue.Length == 2)
                {
                    if (keyValue[0].Trim() == "RulePlayerKey")
                        rulePlayerKey = keyValue[1].Trim();
                    else if (keyValue[0].Trim() == "UniqueId")
                        uniqueId = keyValue[1].Trim();
                }
            }
            
            if (string.IsNullOrEmpty(rulePlayerKey) || string.IsNullOrEmpty(uniqueId))
            {
                _automodLog.Warning($"[AutoMod] Note {note.Id} has malformed metadata line");
                return null;
            }
            
            // Parse simple key: value lines
            var lines = msg.Split('\n');
            int offenseLevel = 1;
            string action = "Unknown";
            string channel = "Unknown";
            string originalMessage = "";
            string ruleName = "";
            int ruleId = 0;
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                // Skip lines that contain user input (wrapped in quotes)
                // Base64 cannot produce " so user input is physically isolated
                if (trimmed.Contains("\""))
                    continue;
                
                if (trimmed.StartsWith("Rule:"))
                {
                    // Format: "Rule: Name (ID: 123)"
                    var ruleInfo = trimmed.Replace("Rule:", "").Trim();
                    var idIndex = ruleInfo.IndexOf("(ID:");
                    if (idIndex != -1)
                    {
                        ruleName = ruleInfo[..idIndex].Trim();
                        var idPart = ruleInfo[(idIndex + 4)..].Replace(")", "").Trim();
                        int.TryParse(idPart, out ruleId);
                    }
                    else
                    {
                        ruleName = ruleInfo;
                    }
                }
                else if (trimmed.StartsWith("Offense Level:"))
                {
                    if (!int.TryParse(trimmed.Replace("Offense Level:", "").Trim(), out offenseLevel))
                    {
                        _automodLog.Warning($"[AutoMod] Note {note.Id} has invalid offense level");
                        offenseLevel = 1;
                    }
                }
                else if (trimmed.StartsWith("Action Taken:"))
                    action = trimmed.Replace("Action Taken:", "").Trim();
                else if (trimmed.StartsWith("Channel:"))
                    channel = trimmed.Replace("Channel:", "").Trim();
            }
            
            // Extract rule ID from rulePlayerKey if not found (format: automod_RULEID_PLAYERID)
            if (ruleId == 0)
            {
                var keyParts = rulePlayerKey.Split('_');
                if (keyParts.Length >= 2)
                    int.TryParse(keyParts[1], out ruleId);
            }
            
            // Parse incidents from format
            var incidents = new List<ViolationIncident>();
            bool inIncidentSection = false;
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                // Skip lines that contain user input (wrapped in quotes)
                // Base64 cannot produce " so user input is physically isolated
                if (trimmed.Contains("\""))
                    continue;
                
                if (trimmed.StartsWith("── Offense History"))
                {
                    inIncidentSection = true;
                    continue;
                }
                
                if (inIncidentSection && trimmed.StartsWith("╚"))
                {
                    break;
                }
                
                if (inIncidentSection && trimmed.StartsWith("#") && trimmed.Contains("|"))
                {
                    var colonIndex = trimmed.IndexOf(':');
                    if (colonIndex <= 0) continue;
                    
                    var incident = new ViolationIncident { DecayLevel = 1, DecaySeconds = 0, Timestamp = note.CreatedAt };
                    
                    foreach (var part in trimmed[(colonIndex + 1)..].Trim().Split('|'))
                    {
                        var p = part.Trim();
                        if (p.StartsWith("Level ") && int.TryParse(p.Replace("Level ", ""), out var level))
                            incident.OffenseLevel = level;
                        else if (p is "Warn" or "Kick" or "Ban" or "None")
                            incident.ActionTaken = p;
                        else if (p.StartsWith("Decay:"))
                        {
                            var decayStr = p.Replace("Decay:", "").Trim();
                            var byIndex = decayStr.IndexOf(", by ");
                            if (byIndex >= 0)
                            {
                                if (int.TryParse(decayStr[(byIndex + 5)..].Trim(), out var lvl))
                                    incident.DecayLevel = lvl;
                                decayStr = decayStr[..byIndex].Trim();
                            }
                            
                            if (TryParseDecayTime(decayStr, out var seconds))
                                incident.DecaySeconds = seconds;
                        }
                        else if (p == "[ACTIVE]")
                            incident.IsDecayed = false;
                        else if (p == "[DECAYED]")
                            incident.IsDecayed = true;
                    }
                    
                    var nextLineIndex = Array.IndexOf(lines, line) + 1;
                    if (nextLineIndex < lines.Length)
                    {
                        var messageLine = lines[nextLineIndex].Trim();
                        if (messageLine.StartsWith("\"") && messageLine.EndsWith("\""))
                        {
                            // Extract cleaned plaintext
                            incident.Message = messageLine[1..^1]; // Remove quotes
                        }
                    }
                    
                    incident.Channel = channel;
                    incidents.Add(incident);
                }
            }
            
            // Calculate ExpiresAt for all incidents based on their decay time
            // The note's ExpiryTime tells us when the NEXT decay will happen
            if (note.ExpiryTime.HasValue && incidents.Any(i => !i.IsDecayed && i.DecaySeconds > 0))
            {
                var now = DateTime.UtcNow;
                var nextDecayTime = DateTime.SpecifyKind(note.ExpiryTime.Value, DateTimeKind.Utc);
                
                // Find the newest active incident - it should expire at note.ExpiryTime
                var newestActive = incidents
                    .Where(i => !i.IsDecayed && i.DecaySeconds > 0)
                    .OrderByDescending(i => i.Timestamp)
                    .FirstOrDefault();
                
                if (newestActive != null)
                {
                    newestActive.ExpiresAt = nextDecayTime;
                    
                    // Calculate ExpiresAt for older incidents working backwards
                    // Each older incident expires earlier based on its decay time
                    var olderIncidents = incidents
                        .Where(i => !i.IsDecayed && i.DecaySeconds > 0 && i != newestActive)
                        .OrderByDescending(i => i.Timestamp)
                        .ToList();
                    
                    foreach (var incident in olderIncidents)
                    {
                        // Older incidents would have expired in the past
                        // Calculate based on when they were created + their decay time
                        incident.ExpiresAt = DateTime.SpecifyKind(incident.Timestamp.AddSeconds(incident.DecaySeconds), DateTimeKind.Utc);
                    }
                }
            }
            
            // If no incidents found, create fallback from current data
            if (incidents.Count == 0)
            {
                _automodLog.Warning($"[AutoMod] Note {note.Id} has no parseable incidents - creating fallback");
                incidents.Add(new ViolationIncident
                {
                    OffenseLevel = offenseLevel,
                    ActionTaken = action,
                    Message = originalMessage,
                    Timestamp = note.CreatedAt,
                    Channel = channel,
                    DecaySeconds = 0,
                    DecayLevel = 1,
                    ExpiresAt = note.ExpiryTime,
                    IsDecayed = false
                });
            }
            
            // Validate offense level matches active incidents
            var activeIncidents = incidents.Count(i => !i.IsDecayed);
            if (activeIncidents != offenseLevel)
            {
                _automodLog.Warning($"[AutoMod] Note {note.Id} has offense level {offenseLevel} but {activeIncidents} active incidents - using incident count");
                offenseLevel = activeIncidents;
            }
            
            var violationData = new AutoModViolationData
            {
                NoteId = note.Id,
                UniqueId = uniqueId,
                RulePlayerKey = rulePlayerKey,
                RuleId = ruleId,
                RuleName = ruleName,
                Category = ruleName,
                ViolationCount = offenseLevel,
                CurrentAction = action,
                OriginalMessage = originalMessage,
                Channel = channel,
                RegexPattern = "",
                LastUpdated = DateTime.UtcNow,
                CreatedAt = note.CreatedAt,
                Incidents = incidents
            };
            
            return violationData;
        }
        catch (Exception ex)
        {
            _automodLog.Warning($"Failed to extract AutoMod violation data from note {note.Id}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Adds or updates AutoMod notes
    /// </summary>
    private async Task<string> AddOrUpdateAutoModNote(AutoModRule rule, NetUserId playerId, int newOffenseLevel, string message, string channel, AutoModOffenceAction action, int decaySeconds, int decayLevel, AutoModViolationData? existingViolation = null)
    {
        int retryCount = 0;
        
        // Clean message once: only keep alphanumeric, whitespace, +, /, = (strips all markup)
        var cleanedMessage = new string(message
            .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '+' || c == '/' || c == '=')
            .ToArray());
        
        retry:
        try
        {
            // Ensure the noted player exists in the DB
            if (await _db.GetPlayerRecordByUserId(playerId) is null)
            {
                _automodLog.Warning($"Skipping AutoMod note: no player record found for {playerId} (FK constraint).");
                return cleanedMessage;
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
                // Mark expired incidents as decayed
                var currentTime = DateTime.UtcNow;
                foreach (var incident in existingViolation.Incidents)
                {
                    if (incident.ExpiresAt.HasValue && currentTime > incident.ExpiresAt.Value && !incident.IsDecayed)
                        incident.IsDecayed = true;
                }
                
                existingViolation.Incidents.Add(new ViolationIncident
                    {
                        Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                        Message = cleanedMessage,
                        Channel = channel,
                        ActionTaken = action.ToString(),
                        OffenseLevel = newOffenseLevel, // Store the actual offense level (1, 2, 3, etc.)
                        DecaySeconds = decaySeconds,
                        DecayLevel = decayLevel,
                        ExpiresAt = decaySeconds > 0 ? DateTime.SpecifyKind(DateTime.UtcNow.AddSeconds(decaySeconds), DateTimeKind.Utc) : null,
                        IsDecayed = false
                });
                
                existingViolation.ViolationCount = newOffenseLevel;
                existingViolation.CurrentAction = action.ToString();
                existingViolation.OriginalMessage = cleanedMessage;
                existingViolation.Channel = channel;
                existingViolation.LastUpdated = DateTime.UtcNow;
                
                var nextIncidentDecayTime = existingViolation.Incidents
                    .Where(i => !i.IsDecayed && i.ExpiresAt.HasValue)
                    .OrderByDescending(i => i.Timestamp)
                    .FirstOrDefault()?.ExpiresAt;
                
                var updatedMessage = FormatViolationMessage(rule, playerId, existingViolation.ViolationCount, channel, action.ToString(), existingViolation.Incidents, existingViolation.UniqueId);
                    
                await _db.EditAdminNote(existingViolation.NoteId, updatedMessage, severity, rule.Secret, 
                    Guid.Empty, DateTimeOffset.UtcNow, 
                    nextIncidentDecayTime.HasValue ? new DateTimeOffset(nextIncidentDecayTime.Value) : null);
                return cleanedMessage;
            }
            
            // Create new note
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        var incidents = new List<ViolationIncident> { new() { 
            Timestamp = now, Message = cleanedMessage, Channel = channel, ActionTaken = action.ToString(), 
            OffenseLevel = newOffenseLevel, DecaySeconds = decaySeconds, DecayLevel = decayLevel,
            ExpiresAt = decaySeconds > 0 ? DateTime.SpecifyKind(now.AddSeconds(decaySeconds), DateTimeKind.Utc) : null
        } };
        
        var noteMessage = FormatViolationMessage(rule, playerId, newOffenseLevel, channel, action.ToString(), incidents, null);
        var firstIncidentDecayTime = incidents.FirstOrDefault(i => i.ExpiresAt.HasValue)?.ExpiresAt;
        
        await _db.AddAdminNote(roundId, playerId, playtime, noteMessage, severity, rule.Secret, 
            Guid.Empty, DateTimeOffset.UtcNow, 
            firstIncidentDecayTime.HasValue ? new DateTimeOffset(firstIncidentDecayTime.Value) : null);
        
        return cleanedMessage;
    }
    catch (Exception ex)
    {
        // If we hit a UNIQUE constraint error, retry by querying for existing note
        if (ex is Microsoft.EntityFrameworkCore.DbUpdateException dbEx && dbEx.InnerException is Microsoft.Data.Sqlite.SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19 && retryCount < 3)
        {
            retryCount++;
            _automodLog.Warning($"[AutoMod] AddAdminNote hit UNIQUE constraint (race condition), retrying (attempt {retryCount})");
            await Task.Delay(100 * retryCount);
            
            var freshViolations = await GetPlayerAutoModViolations(playerId);
            existingViolation = freshViolations.FirstOrDefault(v => v.RuleId == rule.Id);
            
            goto retry;
        }
        _automodLog.Error($"Failed to add/update AutoMod note for rule {rule.Id}: {ex}");
        return cleanedMessage; // Return cleaned message even on error
    }
}

/// <summary>
/// Process AutoMod decay when notes are retrieved (integrated into admin notes system)
/// This provides immediate decay processing when admins view notes, supplementing the background timer
/// </summary>
    private async Task OnNotesRetrieved(Guid playerId, List<IAdminRemarksRecord> notes)
    {
        var now = DateTime.UtcNow;
        // Use shared decay processing logic
        await ProcessPlayerDecay(playerId, notes, now);
    }

    #endregion

    /// <summary>
    /// Background task that processes decay for all AutoMod notes automatically
    /// </summary>
    private async void ProcessBackgroundDecay()
    {
        try
        {
            var players = _playerManager.Sessions;
            if (!players.Any()) return;
            
            var now = DateTime.UtcNow;
            foreach (var player in players)
            {
                try
                {
                    var playerNotes = await _adminNotesManager.GetAllAdminRemarks(player.UserId.UserId);
                    if (!playerNotes.Any(n => n is AdminNoteRecord note && note.Message.Contains("Metadata:")))
                        continue;
                    
                    await ProcessPlayerDecay(player.UserId.UserId, playerNotes, now);
                }
                catch (Exception ex)
                {
                    _automodLog.Error($"[AutoMod] Failed to process decay for player {player.UserId}: {ex}");
                }
            }
        }
        catch (Exception ex)
        {
            _automodLog.Error($"[AutoMod] Background decay processing failed: {ex}");
        }
    }
    
    /// <summary>
    /// Processes decay for a specific player's AutoMod notes
    /// Shared logic used by both background timer and event-driven processing
    /// </summary>
    private async Task ProcessPlayerDecay(Guid playerId, List<IAdminRemarksRecord> notes, DateTime now)
    {
        try
        {
            foreach (var note in notes)
            {
                if (note is not AdminNoteRecord adminNote || !adminNote.Message.Contains("Metadata:"))
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
                
                // Check cooldown: don't process decay more than once per second per note
                if (_lastDecayProcessed.TryGetValue(adminNote.Id, out var lastProcessed))
                {
                    if ((now - lastProcessed).TotalSeconds < 1.0)
                        continue; // Skip if processed within last second
                }
                
                // Find expired incidents that haven't been processed yet
                var expiredIncidents = violationData.Incidents
                    .Where(i => i.ExpiresAt.HasValue && now >= i.ExpiresAt.Value && !i.IsDecayed)
                    .OrderBy(i => i.ExpiresAt) // Process oldest expiry first
                    .ToList();
                
                if (expiredIncidents.Count == 0)
                    continue;
                
                // Take only the OLDEST expired incident to process
                // Decay happens one incident at a time, not multiple at once
                var oldestExpired = expiredIncidents.First();
                var decayLevel = oldestExpired.DecayLevel;
                
                // Mark the oldest N active incidents as decayed (where N = DecayLevel)
                var allIncidents = violationData.Incidents.OrderBy(i => i.Timestamp).ToList();
                var activeIncidents = allIncidents.Where(i => !i.IsDecayed).ToList();
                var toDecay = activeIncidents.Take(decayLevel).ToList();
                
                foreach (var incident in toDecay)
                {
                    incident.IsDecayed = true;
                }
                
                // Recalculate violation count (only count non-decayed incidents)
                var newViolationCount = allIncidents.Count(i => !i.IsDecayed);
                violationData.ViolationCount = newViolationCount;
                
                // Calculate next decay time (find oldest non-decayed incident with expiry)
                var nextDecayTime = allIncidents
                    .Where(i => !i.IsDecayed && i.ExpiresAt.HasValue)
                    .OrderBy(i => i.ExpiresAt)
                    .FirstOrDefault()?.ExpiresAt;
                
                // Format updated note with new violation data
                var rule = _rules.FirstOrDefault(r => r.Id == violationData.RuleId) ?? new AutoModRule { Id = violationData.RuleId };
                var updatedMessage = FormatViolationMessage(
                    rule,
                    new NetUserId(playerId),
                    newViolationCount,
                    violationData.Channel,
                    violationData.CurrentAction,
                    allIncidents,
                    violationData.RulePlayerKey
                );
                
                // Update the note in the database
                await _db.EditAdminNote(
                    adminNote.Id,
                    updatedMessage,
                    adminNote.Severity,
                    adminNote.Secret,
                    Guid.Empty,
                    DateTimeOffset.UtcNow,
                    nextDecayTime.HasValue ? new DateTimeOffset(nextDecayTime.Value) : null
                );
                
                _lastDecayProcessed[adminNote.Id] = now;
            }
        }
        catch (Exception ex)
        {
            _automodLog.Error($"[AutoMod] Failed to process decay for player {playerId}: {ex}");
        }
    }

    /// <summary>
    /// Gets all AutoMod violations for a player from admin notes (read-only, no decay processing)
    /// </summary>
    private async Task<List<AutoModViolationData>> GetPlayerAutoModViolations(NetUserId userId)
    {
        var violations = new List<AutoModViolationData>();
        try
        {
            var notes = await _adminNotesManager.GetAllAdminRemarks(userId.UserId);
            
            foreach (var note in notes)
            {
                if (note is not AdminNoteRecord adminNote || !adminNote.Message.Contains("Metadata:"))
                    continue;
                
                var violationData = ExtractViolationData(new AdminNote
                {
                    Id = adminNote.Id,
                    Message = adminNote.Message,
                    CreatedAt = adminNote.CreatedAt.DateTime,
                    CreatedBy = adminNote.CreatedBy?.LastSeenUserName ?? "System",
                    Severity = adminNote.Severity,
                    ExpiryTime = adminNote.ExpirationTime?.DateTime,
                    Secret = adminNote.Secret
                });
                
                if (violationData != null)
                    violations.Add(violationData);
            }
        }
        catch (Exception ex)
        {
            _automodLog.Error($"Failed to get AutoMod violations for user {userId}: {ex}");
        }
        return violations;
    }

    /// <summary>
    /// Gets AutoMod violation statistics for display in admin panels
    /// </summary>
    public async Task<(int rulesBroken, int totalOffences)> GetAutoModStatistics(NetUserId userId)
    {
        try
        {
            var violations = await GetPlayerAutoModViolations(userId);
            
            // Only count rules with active violations (ViolationCount > 0)
            var activeViolations = violations.Where(v => v.ViolationCount > 0).ToList();
            
            // Number of distinct rules that have active violations
            var rulesBroken = activeViolations.Select(v => v.RuleId).Distinct().Count();
            
            // Count only non-decayed incidents across all violation notes
            var totalOffences = activeViolations.Sum(v => v.Incidents?.Count(i => !i.IsDecayed) ?? 0);
            
            return (rulesBroken, totalOffences);
        }
        catch (Exception ex)
        {
            _automodLog.Error($"Failed to get AutoMod statistics for user {userId}: {ex}");
            return (0, 0);
        }
    }
}
