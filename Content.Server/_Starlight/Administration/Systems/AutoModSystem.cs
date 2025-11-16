using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Database;
using Content.Server.Administration.Notes;
using Content.Shared._Starlight.Administration;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Database;
using Robust.Shared.Network;
using Robust.Server.Player;
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
    [Dependency] private readonly Server.Administration.Logs.IAdminLogManager _adminLogger = default!;
    
    private readonly ISawmill _automodLog = Logger.GetSawmill("automod");
    public const string NotificationChannel = "automod_rules";
    
    private readonly Dictionary<(NetUserId, string), DateTime> _recentMessages = new();
    private List<AutoModRule> _rules = new();
    
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
        var messageKey = (args.Sender.UserId, message);
        var now = DateTime.UtcNow;
        
        if (_recentMessages.TryGetValue(messageKey, out var recentMessageTime) && (now - recentMessageTime).TotalSeconds < 2)
            return;
        
        _recentMessages[messageKey] = now;
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
            var ruleViolations = existingViolations.Where(v => v.RuleId == rule.Id).ToList();
            
            if (ruleViolations.Any())
            {
                var activeViolations = ruleViolations.Where(v => !v.DecayAfter.HasValue || DateTime.UtcNow <= v.DecayAfter.Value).ToList();
                if (activeViolations.Any())
                {
                    var mostRecentViolation = activeViolations.OrderByDescending(v => v.CreatedAt).First();
                    offenceIndex = mostRecentViolation.ViolationCount;
                }
            }
            
            AutoModOffence? offence = null;
            if (rule.Offences != null && rule.Offences.Count > 0)
                offence = offenceIndex < rule.Offences.Count ? rule.Offences[offenceIndex] : rule.Offences.Last();
            offence ??= new AutoModOffence { Message = "", Action = (int)AutoModOffenceAction.None };

            var nextIndex = offenceIndex + 1;
            if (rule.Offences != null && rule.Offences.Count > 0)
                nextIndex = Math.Min(nextIndex, rule.Offences.Count - 1);
            
            try
            {
                await AddOrUpdateAutoModNote(rule, args.Sender.UserId, nextIndex, message, GetChannelDisplayName(args.Channel), (AutoModOffenceAction)offence.Action, offence.DecaySeconds > 0 ? DateTime.UtcNow.AddSeconds(offence.DecaySeconds) : null);
            }
            catch (Exception ex)
            {
                _automodLog.Error($"Failed to create/update AutoMod admin note: {ex}");
            }

            _adminLogger.Add(LogType.AdminCommands, LogImpact.Low, 
                $"[AutoMod Debug] Processing offence with action: {offence.Action} ({(AutoModOffenceAction)offence.Action}) for rule: {rule.Regex}");

            bool messageCleared = offence.CancelSpeech;
            if (messageCleared)
            {
                _adminLogger.Add(LogType.AdminCommands, LogImpact.High, 
                    $"[AutoMod] Cleared speech of user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} (Offence {offenceIndex + 1}) - Message: \"{message}\"");
                args.Cancel();
            }

            var adminNotification = FormatAutoModBwoink(rule, offence, offenceIndex, message, args.Channel, messageCleared);
            SendAdminOnlyBwoink(args.Sender.UserId, adminNotification);
            HandleOffenceAction(args, rule, offence, offenceIndex, message);
        }
    }
    
    #endregion

    #region Action Handling

    /// <summary>
    /// Handles the specific action for an AutoMod offence
    /// </summary>
    private void HandleOffenceAction(ChatAttemptEvent args, AutoModRule rule, AutoModOffence offence, int offenceIndex, string message)
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
                    $"[AutoMod] Logged violation for user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} (Offence {offenceIndex + 1}) - Message: \"{message}\"");
                break;
 
            case AutoModOffenceAction.Warn:
                _adminLogger.Add(LogType.AdminCommands, logImpact, 
                    $"[AutoMod] Warned user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} (Offence {offenceIndex + 1}) - Reason: {offence.Message} - Message: \"{message}\"");
                _chat.ChatMessageToOne(ChatChannel.Server, offence.Message, offence.Message, EntityUid.Invalid, false, args.Sender.Channel);
                break;

            case AutoModOffenceAction.Kick:
                var kickReason = string.IsNullOrWhiteSpace(offence.Message) ? "Kicked by AutoMod" : $"Kicked by AutoMod for: {offence.Message}";
                _adminLogger.Add(LogType.AdminCommands, logImpact, 
                    $"[AutoMod] Kicked user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} (Offence {offenceIndex + 1}) - Reason: {kickReason} - Message: \"{message}\"");
                _netManager.DisconnectChannel(args.Sender.Channel, kickReason);
                break;

            case AutoModOffenceAction.Ban:
                var banReason = string.IsNullOrWhiteSpace(offence.Message) ? "Banned by AutoMod" : $"Banned by AutoMod for: {offence.Message}";
                banReason += "\n\nYou may appeal this ban in our discord at: https://discord.com/invite/ssJTANEa";
                uint? duration = offence.BanDurationMinutes > 0 ? (uint)offence.BanDurationMinutes : null;
                _banManager.CreateServerBan(args.Sender.UserId, args.Sender.Name, null, null, null, duration, NoteSeverity.High, banReason);
                _adminLogger.Add(LogType.AdminCommands, logImpact, 
                    $"[AutoMod] Banned user {args.Sender.Name} ({args.Sender.UserId}) for rule: {rule.Regex} (Offence {offenceIndex + 1}) - Reason: {banReason} - Duration: {(duration.HasValue ? duration + " minutes" : "permanent")} - Message: \"{message}\"");
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
        result += $"[color={actionColor}]Player was {actionText.ToLower()}[/color]\n";
        result += $"[color=gray]{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC[/color]";

        return result;
    }

    /// <summary>
    /// Gets a user-friendly display name for chat channels
    /// Note: Admin channels (Admin, AdminChat, AdminAlert) should never reach this method due to early filtering
    /// </summary>
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
    /// Sends an admin-only bwoink notification for AutoMod rule violations
    /// </summary>
    private void SendAdminOnlyBwoink(NetUserId violatorUserId, string message)
    {
        try
        {
            var bwoinkMessage = new SharedBwoinkSystem.BwoinkTextMessage(
                violatorUserId, 
                new NetUserId(Guid.Empty), // System user ID
                message, 
                playSound: true, 
                adminOnly: true
            );

            RaiseNetworkEvent(bwoinkMessage);
            _automodLog.Info($"[AutoMod] Sent bwoink: {message}");
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
        public string Type => "automod_violation";
        public string UniqueId { get; set; } = Guid.NewGuid().ToString(); // Unique identifier for edit protection
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
    private string FormatViolationMessage(AutoModRule rule, NetUserId playerId, int offenseLevel, string originalMessage, string channel, string action, List<ViolationIncident> incidents, DateTime? decayAfter = null)
    {
        var data = new AutoModViolationData
        {
            RulePlayerKey = $"automod_{rule.Id}_{playerId}",
            RuleId = rule.Id,
            RuleName = rule.Category ?? $"Rule #{rule.Id}",
            Category = rule.Category ?? "Uncategorized",
            ViolationCount = offenseLevel,
            CurrentAction = action,
            OriginalMessage = originalMessage,
            Channel = channel,
            RegexPattern = rule.Regex,
            LastUpdated = DateTime.UtcNow,
            CreatedAt = incidents.FirstOrDefault()?.Timestamp ?? DateTime.UtcNow,
            DecayAfter = decayAfter,
            Incidents = incidents
        };

        return $"[AutoMod] {data.RuleName} - Level {offenseLevel} - {action}\nMessage: \"{originalMessage}\" in {channel}\n\n{JsonSerializer.Serialize(data)}";
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
                if (note is AdminNoteRecord adminNote && adminNote.Message.Contains($"\"RulePlayerKey\": \"{rulePlayerKey}\""))
                {
                    return new AdminNote
                    {
                        Id = adminNote.Id,
                        Message = adminNote.Message,
                        CreatedAt = adminNote.CreatedAt.DateTime,
                        CreatedBy = adminNote.CreatedBy?.LastSeenUserName ?? "System",
                        Severity = adminNote.Severity,
                        ExpiryTime = adminNote.ExpirationTime?.DateTime,
                        Secret = adminNote.Secret
                    };
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
    /// Extracts AutoMod violation data from an existing admin note
    /// </summary>
    private AutoModViolationData? ExtractViolationData(AdminNote note)
    {
        try
        {
            // Find the JSON section in the note
            var jsonStart = note.Message.LastIndexOf('{');
            var jsonEnd = note.Message.LastIndexOf('}');
            
            if (jsonStart == -1 || jsonEnd == -1 || jsonEnd <= jsonStart)
                return null;

            var jsonSection = note.Message.Substring(jsonStart, jsonEnd - jsonStart + 1);
            return JsonSerializer.Deserialize<AutoModViolationData>(jsonSection);
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
    private async Task AddOrUpdateAutoModNote(AutoModRule rule, NetUserId playerId, int newOffenseLevel, string message, string channel, AutoModOffenceAction action, DateTime? decayAfter)
    {
        try
        {
            // Ensure the noted player exists in the DB to satisfy FK constraints
            if (await _db.GetPlayerRecordByUserId(playerId) is null)
            {
                _automodLog.Warning($"Skipping AutoMod note: no player record found for {playerId} (FK constraint).");
                return;
            }

            var existingNote = await FindExistingAutoModNote(rule.Id, playerId);
            var severity = action switch
            {
                AutoModOffenceAction.None => NoteSeverity.Minor,
                AutoModOffenceAction.Warn or AutoModOffenceAction.Kick or AutoModOffenceAction.Ban => NoteSeverity.High,
                _ => NoteSeverity.Medium
            };
            
            // Create system notes without requiring a valid player foreign key
            // We'll bypass the admin notes manager to avoid session requirements
            var roundId = (int?)null;
            var playtime = TimeSpan.Zero;

            if (existingNote != null)
            {
                var existingData = ExtractViolationData(existingNote);
                if (existingData != null)
                {
                    existingData.Incidents.Add(new ViolationIncident
                    {
                        Timestamp = DateTime.UtcNow,
                        Message = message,
                        Channel = channel,
                        ActionTaken = action.ToString(),
                        OffenseLevel = newOffenseLevel
                    });
                    
                    existingData.ViolationCount = newOffenseLevel;
                    existingData.CurrentAction = action.ToString();
                    existingData.OriginalMessage = message;
                    existingData.Channel = channel;
                    existingData.LastUpdated = DateTime.UtcNow;
                    if (decayAfter.HasValue) existingData.DecayAfter = decayAfter;
                    
                    var updatedMessage = FormatViolationMessage(rule, playerId, newOffenseLevel, message, channel, action.ToString(), existingData.Incidents, decayAfter);
                    await _db.EditAdminNote(existingNote.Id, updatedMessage, severity, false, Guid.Empty, DateTimeOffset.UtcNow, decayAfter?.ToUniversalTime());
                    return;
                }
            }
            
            var incidents = new List<ViolationIncident> { new() { Timestamp = DateTime.UtcNow, Message = message, Channel = channel, ActionTaken = action.ToString(), OffenseLevel = newOffenseLevel } };
            var noteMessage = FormatViolationMessage(rule, playerId, newOffenseLevel, message, channel, action.ToString(), incidents, decayAfter);
            await _db.AddAdminNote(roundId, playerId, playtime, noteMessage, severity, false, Guid.Empty, DateTimeOffset.UtcNow, decayAfter?.ToUniversalTime());
        }
        catch (Exception ex)
        {
            _automodLog.Error($"Failed to add/update AutoMod note for rule {rule.Id}: {ex}");
            throw;
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
                if (note is not AdminNoteRecord adminNote || !adminNote.Message.Contains("\"Type\": \"automod_violation\""))
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
                if (violationData?.DecayAfter.HasValue == true && now > violationData.DecayAfter.Value) continue;
                if (violationData != null) violations.Add(violationData);
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
