using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.Starlight.Chat.Systems;
using Content.Server.EUI;
using Content.Shared._Starlight.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using static Content.Shared._Starlight.Administration.AutoModEuiMsg;
using ServerAutoModRule = Content.Server.Database.AutoModRule;
using ServerAutoModOffence = Content.Server.Database.AutoModOffence;
using SharedAutoModRule = Content.Shared._Starlight.Administration.AutoModRule;
using SharedAutoModOffence = Content.Shared._Starlight.Administration.AutoModOffence;

namespace Content.Server.Administration.UI;

public sealed class AutoModEui : BaseEui
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly Content.Server.Administration.Logs.IAdminLogManager _adminLogger = default!;
    
    private List<ServerAutoModRule> _rules = new();
    
    // Yeah this is a horrible way to do this, what you gonna do about it?
    private readonly HashSet<string> _blacklistedWords = new()
    {
        "space", "station", "fourteen", "ss14", "ss13", "nanotrasen", "syndicate", "starlight", "centralcommand",
        "the", "and", "for", "with", "from", "about", "into", "through", "after", "before",
        "get", "got", "have", "has", "had", "use", "used", "using", "make", "made", "making",
        "take", "took", "taken", "give", "gave", "given", "put", "set", "go", "went", "come",
        "see", "saw", "look", "looked", "find", "found", "tell", "told", "ask", "asked",
        "work", "worked", "working", "need", "needed", "want", "wanted", "try", "tried",
        "help", "helped", "talk", "talked", "know", "knew", "think", "thought",
        "you", "your", "yours", "me", "my", "mine", "he", "him", "his", "she", "her", "hers",
        "we", "us", "our", "ours", "they", "them", "their", "theirs", "it", "its",
        "this", "that", "these", "those", "who", "what", "where", "when", "why", "how",
        "in", "on", "at", "by", "to", "of", "or", "but", "if", "as", "so", "up", "out", "off",
        "captain", "hop", "hos", "ce", "cmo", "rd", "qm", "warden", "detective", "officer",
        "engineer", "atmos", "atmospheric", "doctor", "chemist", "paramedic", "scientist",
        "roboticist", "botanist", "chef", "cook", "bartender", "janitor", "clown", "mime",
        "assistant", "passenger", "cargo", "salvage", "medical", "security", "service",
        "lawyer", "chaplain", "librarian", "musician", "reporter", "boxer", "zookeeper",
        "medbay", "bridge", "engineering", "atmos", "atmospherics", "cargo", "science",
        "research", "security", "brig", "arrivals", "departures", "dorms", "hallway",
        "maintenance", "maint", "bar", "kitchen", "botany", "hydroponics", "chapel",
        "library", "courtroom", "boxing", "holodeck", "eva", "armory", "vault", "ai",
        "door", "doors", "wall", "walls", "floor", "floors", "window", "windows", "table",
        "chair", "light", "lights", "computer", "console", "machine", "device", "tool", "tools",
        "wrench", "screwdriver", "crowbar", "welder", "multitool", "wirecutters", "cable",
        "power", "energy", "battery", "cell", "wire", "cable", "sheet", "metal", "glass",
        "plasteel", "plasma", "oxygen", "nitrogen", "carbon", "gas", "pipe", "pipes",
        "health", "damage", "injury", "injured", "hurt", "heal", "healed", "healing",
        "medicine", "medical", "bandage", "ointment", "pill", "syringe", "surgery",
        "brute", "burn", "toxin", "airloss", "genetic", "clone", "cloning",
        "round", "shift", "job", "role", "spawn", "respawn", "dead", "death", "kill",
        "ghost", "antag", "antagonist", "traitor", "nukie", "nukies", "revolutionary",
        "changeling", "heretic", "wizard", "blob", "dragon", "zombie", "rat", "king",
        "objective", "objectives", "greentext", "shuttle", "escape", "evac", "evacuation",
        "radio", "comms", "common", "command", "say", "saying", "said", "chat", "talking",
        "announce", "announcement", "alert", "warning", "emergency", "pda", "message",
        "open", "opened", "close", "closed", "lock", "locked", "unlock", "unlocked",
        "break", "broke", "broken", "fix", "fixed", "repair", "repaired", "build", "built",
        "destroy", "destroyed", "damage", "damaged", "wear", "wearing", "hold", "holding",
        "carry", "carrying", "drop", "dropped", "throw", "threw", "thrown", "pull", "pulled",
        "time", "minute", "minutes", "second", "seconds", "hour", "hours", "now", "then",
        "soon", "later", "one", "two", "three", "four", "five", "some", "many", "few",
        "good", "bad", "new", "old", "big", "small", "long", "short", "high", "low",
        "hot", "cold", "fast", "slow", "hard", "soft", "heavy", "light", "safe", "dangerous",
        "clean", "dirty", "empty", "full", "ready", "busy", "free", "right", "wrong", "left",
        "please", "thanks", "thank", "sorry", "yes", "yeah", "yep", "no", "nope", "ok",
        "okay", "sure", "fine", "alright", "hello", "hi", "hey", "bye", "goodbye", "welcome",
        "greytide", "shitsec", "validhunting", "robust", "stun", "stunned", "crit",
        "critical", "gibbed", "spaced", "spacing", "toolbox", "stunbaton", "baton",
        "disabler", "taser", "laser", "gun", "weapon", "bomb", "explosive", "emag",
    };

    public AutoModEui() => IoCManager.InjectDependencies(this);

    public override void Opened()
    {
        base.Opened();
        StateDirty();
        LoadFromDb();
    }

    public override void Closed() => base.Closed();

    private async void LoadFromDb()
    {
        try
        {
            _rules = await _db.GetAutoModRules();
            StateDirty();
        }
        catch (Exception ex)
        {
            Logger.GetSawmill("automod").Error($"Error loading AutoMod rules from database: {ex}");
        }
    }

    private static ServerAutoModRule MapToServerRule(SharedAutoModRule shared) => new()
    {
        Id = shared.Id,
        Category = shared.Category,
        Severity = shared.Severity,
        Regex = shared.Regex,
        Enabled = shared.Enabled,
        Secret = shared.Secret,
        WatchOOC = shared.WatchOOC,
        CreatedBy = shared.CreatedBy,
        CreatedAt = shared.CreatedAt,
        LastModifiedBy = shared.LastModifiedBy,
        LastModifiedAt = shared.LastModifiedAt,
        Offences = shared.Offences?.Select(o => new ServerAutoModOffence
        {
            Message = o.Message,
            Action = (int)o.Action,
            BanDurationMinutes = o.BanDurationMinutes,
            DecaySeconds = o.DecaySeconds,
            DecayLevels = o.DecayLevels,
            Persistent = o.Persistent,
            CancelSpeech = o.CancelSpeech
        }).ToList() ?? new List<ServerAutoModOffence>()
    };

    private static SharedAutoModRule MapToSharedRule(ServerAutoModRule server) => new()
    {
        Id = server.Id,
        Category = server.Category,
        Severity = server.Severity,
        Regex = server.Regex,
        Enabled = server.Enabled,
        WatchOOC = server.WatchOOC,
        Secret = server.Secret,
        CreatedBy = server.CreatedBy,
        CreatedAt = server.CreatedAt,
        LastModifiedBy = server.LastModifiedBy,
        LastModifiedAt = server.LastModifiedAt,
        Offences = server.Offences?.Select(o => new SharedAutoModOffence
        {
            Message = o.Message,
            Action = (AutoModOffenceAction)o.Action,
            BanDurationMinutes = o.BanDurationMinutes,
            DecaySeconds = o.DecaySeconds,
            DecayLevels = o.DecayLevels,
            Persistent = o.Persistent,
            CancelSpeech = o.CancelSpeech
        }).ToList() ?? new List<SharedAutoModOffence>()
    };
    
    // Helper properties for admin tracking
    private string _adminName => Player?.Name ?? "unknown";
    private string _adminId => Player?.UserId.ToString() ?? "unknown";
    private Guid _adminGuid => Player?.UserId ?? Guid.Empty;

    // Helper method for database operations with common error handling and refresh
    private async Task ExecuteDbOperation(Func<Task> operation, string operationName)
    {
        try
        {
            await operation();
            LoadFromDb();
            await RefreshAutomodCacheAsync();
        }
        catch (Exception ex)
        {
            Logger.GetSawmill("automod").Error($"Error {operationName} AutoMod rule: {ex}");
        }
    }

    /// <summary>
    /// Validates that a rule's regex pattern doesn't match any blacklisted words
    /// </summary>
    private bool ValidateRule(SharedAutoModRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Regex)) return true;
        
        try
        {
            var regex = new System.Text.RegularExpressions.Regex(rule.Regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Test if the regex matches any blacklisted words
            var matchedWord = _blacklistedWords.FirstOrDefault(word => regex.IsMatch(word));
            
            if (matchedWord == null) return true;

            SendMessage(new ValidationErrorResponse("Rule would match blacklisted word", matchedWord, rule.Regex));
            return false;
        }
        catch (System.ArgumentException ex)
        {
            // Invalid regex pattern
            Logger.GetSawmill("automod").Warning($"Admin {_adminName} ({_adminId}) attempted to create rule with invalid regex: {rule.Regex} - {ex.Message}");
            SendMessage(new ValidationErrorResponse("Invalid regex pattern", "", rule.Regex));
            return false;
        }
    }

    public async void DeleteRule(SharedAutoModRule rule) => await ExecuteDbOperation(async () =>
    {
        await _db.DeleteAutoModRule(rule.Id);
        var oldRule = _rules.FirstOrDefault(r => r.Id == rule.Id);
        if (oldRule != null)
            LogAdminAutoModAction("Deleted", _adminName, _adminId, oldRule, null);
    }, "deleting");

    public async void AddRule(SharedAutoModRule rule)
    {
        if (!ValidateRule(rule)) return;
        
        await ExecuteDbOperation(async () =>
        {
            var now = DateTime.UtcNow;
            rule.CreatedBy = rule.LastModifiedBy = _adminGuid;
            rule.CreatedAt = rule.LastModifiedAt = now;
            
            var newRule = MapToServerRule(rule);
            await _db.AddAutoModRule(newRule);
            LogAdminAutoModAction("Created", _adminName, _adminId, null, newRule);
        }, "adding");
    }

    // Helper to check if rule has meaningful changes
    private bool HasRuleChanged(ServerAutoModRule oldRule, ServerAutoModRule newRule) =>
        oldRule.Category != newRule.Category ||
        oldRule.Severity != newRule.Severity ||
        oldRule.Regex != newRule.Regex ||
        oldRule.Enabled != newRule.Enabled ||
        oldRule.WatchOOC != newRule.WatchOOC ||
        oldRule.Secret != newRule.Secret ||
        !OffencesEqual(oldRule, newRule);

    public async void UpdateRule(SharedAutoModRule rule)
    {
        if (!ValidateRule(rule)) return;
            
        await ExecuteDbOperation(async () =>
        {
            var oldRule = _rules.FirstOrDefault(r => r.Id == rule.Id);
            rule.LastModifiedBy = _adminGuid;
            rule.LastModifiedAt = DateTime.UtcNow;
            
            var newRule = MapToServerRule(rule);
            
            // Debug log the rule update
            Logger.GetSawmill("automod").Info($"[AutoMod Debug] Updating rule ID {rule.Id}: Regex='{rule.Regex}', Severity={rule.Severity}");
            if (rule.Offences?.Count > 0)
            {
                var offenceDetails = string.Join("; ", rule.Offences.Select((o, i) => $"Offence{i+1}: Action={o.Action}({(int)o.Action}), Message='{o.Message}'"));
                Logger.GetSawmill("automod").Info($"[AutoMod Debug] Rule offences: {offenceDetails}");
            }
            
            if (oldRule != null && HasRuleChanged(oldRule, newRule))
                LogAdminAutoModAction("Edited", _adminName, _adminId, oldRule, newRule);
            
            await _db.UpdateAutoModRule(newRule);
        }, "updating");
    }

    public async void BulkUpdateRules(List<SharedAutoModRule> rules)
    {
        if (rules.Any(rule => !ValidateRule(rule))) return; // Reject if any rule is invalid
        
        Logger.GetSawmill("automod").Info($"[AutoMod Debug] BulkUpdateRules called with {rules.Count} rules");
            
        await ExecuteDbOperation(async () =>
        {
            var now = DateTime.UtcNow;
            var operationsSummary = new List<string>();
            
            // Process incoming rules (update existing, add new)
            foreach (var rule in rules)
            {
                var oldRule = _rules.FirstOrDefault(r => r.Id == rule.Id);
                rule.LastModifiedBy = _adminGuid;
                rule.LastModifiedAt = now;
                
                var newRule = MapToServerRule(rule);
                
                // Debug log each rule being processed
                Logger.GetSawmill("automod").Info($"[AutoMod Debug] Processing rule ID {rule.Id}: Regex='{rule.Regex}', Severity={rule.Severity}");
                if (rule.Offences?.Count > 0)
                {
                    var offenceDetails = string.Join("; ", rule.Offences.Select((o, i) => $"Offence{i+1}: Action={o.Action}({(int)o.Action}), Message='{o.Message}'"));
                    Logger.GetSawmill("automod").Info($"[AutoMod Debug] Rule offences: {offenceDetails}");
                }
                
                if (oldRule != null)
                {
                    if (HasRuleChanged(oldRule, newRule))
                    {
                        LogAdminAutoModAction("Edited", _adminName, _adminId, oldRule, newRule);
                        operationsSummary.Add($"Updated rule ID {rule.Id} ({rule.Regex})");
                    }
                    await _db.UpdateAutoModRule(newRule);
                }
                else
                {
                    rule.CreatedBy = _adminGuid;
                    rule.CreatedAt = now;
                    newRule = MapToServerRule(rule);
                    await _db.AddAutoModRule(newRule);
                    LogAdminAutoModAction("Created", _adminName, _adminId, null, newRule);
                    operationsSummary.Add($"Created rule ({rule.Regex})");
                }
            }

            // Delete rules not in incoming list
            var incomingIds = rules.Select(r => r.Id).ToHashSet();
            foreach (var dbRule in _rules.Where(r => !incomingIds.Contains(r.Id)))
            {
                await _db.DeleteAutoModRule(dbRule.Id);
                LogAdminAutoModAction("Deleted", _adminName, _adminId, dbRule, null);
                operationsSummary.Add($"Deleted rule ID {dbRule.Id} ({dbRule.Regex})");
            }
            
            // Log bulk operation summary
            if (operationsSummary.Count > 0)
            {
                _adminLogger.Add(LogType.AdminCommands, LogImpact.High, 
                    $"[AutoMod] Bulk Update by {_adminName} ({_adminId}) - {operationsSummary.Count} operations:\n{string.Join("\n", operationsSummary.Select(op => $"  • {op}"))}");
            }
        }, "bulk updating");
    }

    public override void HandleMessage(EuiMessageBase message)
    {
        base.HandleMessage(message);

        switch (message)
        {
            case DeleteRuleRequest msg:
                DeleteRule(msg.Rule);
                break;
            case AddRuleRequest msg:
                AddRule(msg.Rule);
                break;
            case UpdateRuleRequest msg:
                UpdateRule(msg.Rule);
                break;
            case RefreshRequest msg:
                LoadFromDb();
                break;
            case BulkUpdateRulesRequest msg:
                BulkUpdateRules(msg.Rules);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(message), message, null);
        }
    }

    // Helper methods
    public override EuiStateBase GetNewState() => new AutoModEuiState
    {
        Rules = _rules.Select(MapToSharedRule).ToList()
    };

    private static async Task RefreshAutomodCacheAsync()
    {
        var automod = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<AutoModSystem>();
        if (automod != null) await automod.UpdateCache();
    }

    private static string FormatOffences(ServerAutoModRule rule) =>
        rule.Offences?.Count > 0 
            ? string.Join("\n", rule.Offences.Select((o, i) => $"    {i + 1}. [{o.Action}] {o.Message}"))
            : "    (none)";

    private static bool OffencesEqual(ServerAutoModRule a, ServerAutoModRule b)
    {
        if (a.Offences == null && b.Offences == null) return true;
        if (a.Offences?.Count != b.Offences?.Count) return false;
        
        return a.Offences!.Zip(b.Offences!).All(pair => 
            pair.First.Action == pair.Second.Action &&
            pair.First.Message == pair.Second.Message &&
            pair.First.BanDurationMinutes == pair.Second.BanDurationMinutes &&
            pair.First.DecaySeconds == pair.Second.DecaySeconds &&
            pair.First.DecayLevels == pair.Second.DecayLevels &&
            pair.First.Persistent == pair.Second.Persistent &&
            pair.First.CancelSpeech == pair.Second.CancelSpeech);
    }

    private string FormatRuleDetails(ServerAutoModRule rule) =>
        $"Category:      {rule.Category ?? "None"}\n" +
        $"Severity:      {(AutoModSeverity)rule.Severity}\n" +
        $"Regex:         {rule.Regex}\n" +
        $"Enabled:       {rule.Enabled}\n" +
        $"Watch OOC:     {rule.WatchOOC}\n" +
        $"Offences:\n{FormatOffences(rule)}";

    /// <summary>
    /// Logs admin actions for AutoMod rule changes
    /// </summary>
    private void LogAdminAutoModAction(string action, string adminName, string adminId, ServerAutoModRule? before, ServerAutoModRule? after)
    {
        switch (action)
        {
            case "Created":
                _adminLogger.Add(LogType.AdminCommands, LogImpact.High, $"[AutoMod] Rule Created by {adminName} ({adminId})\n───────────────────────────────\n{FormatRuleDetails(after!)}");
                break;
            case "Deleted":
                _adminLogger.Add(LogType.AdminCommands, LogImpact.High, $"[AutoMod] Rule Deleted by {adminName} ({adminId})\n───────────────────────────────\n{FormatRuleDetails(before!)}");
                break;
            case "Edited":
                _adminLogger.Add(LogType.AdminCommands, LogImpact.High, $"[AutoMod] Rule Edited by {adminName} ({adminId}) (ID: {after?.Id})\n────── Before ──────\n{FormatRuleDetails(before!)}\n────── After ──────\n{FormatRuleDetails(after!)}");
                break;
            default:
                throw new ArgumentException($"Unknown action: {action}");
        }
    }
}