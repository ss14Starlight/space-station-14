using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.Starlight.Chat.Systems;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Server.Player;
using Robust.Shared.Network;
using DbAdminRank = Content.Server.Database.AdminRank;
using static Content.Shared.Administration.PermissionsEuiMsg;
using static Content.Shared.Administration.AutoModEuiMsg;
using Content.Shared.Database;
using ServerAutoModRule = global::AutoModRule;
using ServerAutoModOffence = global::AutoModOffence;
using SharedAutoModRule = Content.Shared.Administration.AutoModRule;
using SharedAutoModOffence = Content.Shared.Administration.AutoModOffence;

namespace Content.Server.Administration.UI
{
    public sealed class AutoModEui : BaseEui
    {
        [Dependency] private readonly IServerDbManager _db = default!;
        [Dependency] private readonly Content.Server.Administration.Logs.IAdminLogManager _adminLogger = default!;
        private List<ServerAutoModRule> _rules = new();
        public AutoModEui()
        {
            IoCManager.InjectDependencies(this);
        }

        public override void Opened()
        {
            base.Opened();

            StateDirty();
            LoadFromDb();
        }

        public override void Closed()
        {
            base.Closed();
        }

        private async void LoadFromDb()
        {
            //get the automod rules
            _rules = await _db.GetAutoModRules();

            StateDirty();
        }

        private static ServerAutoModRule MapToServerRule(SharedAutoModRule sharedRule)
        {
            return new ServerAutoModRule
            {
                Id = sharedRule.Id,
                Regex = sharedRule.Regex,
                Enabled = sharedRule.Enabled,
                Offences = sharedRule.Offences?.Select(o => new ServerAutoModOffence
                {
                    Message = o.Message,
                    Action = (int)o.Action,
                    BanDurationMinutes = o.BanDurationMinutes,
                    DecaySeconds = o.DecaySeconds,
                    CancelSpeech = o.CancelSpeech
                }).ToList() ?? new List<ServerAutoModOffence>()
            };
        }

        private static SharedAutoModRule MapToSharedRule(ServerAutoModRule serverRule)
        {
            return new SharedAutoModRule
            {
                Id = serverRule.Id,
                Regex = serverRule.Regex,
                Enabled = serverRule.Enabled,
                Offences = serverRule.Offences?.Select(o => new SharedAutoModOffence
                {
                    Message = o.Message,
                    Action = (Content.Shared.Administration.AutoModOffenceAction)o.Action,
                    BanDurationMinutes = o.BanDurationMinutes,
                    DecaySeconds = o.DecaySeconds,
                    CancelSpeech = o.CancelSpeech
                }).ToList() ?? new List<SharedAutoModOffence>()
            };
        }

        public async void DeleteRule(SharedAutoModRule rule)
        {
            await _db.DeleteAutoModRule(rule.Id);
            var adminId = Player?.UserId.ToString() ?? "unknown";
            var adminName = Player?.Name ?? "unknown";
            var oldRule = _rules.FirstOrDefault(r => r.Id == rule.Id);
            if (oldRule != null)
                LogAdminAutoModAction("Deleted", adminName, adminId, oldRule, null);
            LoadFromDb();
            await RefreshAutomodCacheAsync();
        }

        public async void AddRule(SharedAutoModRule rule)
        {
            await _db.AddAutoModRule(MapToServerRule(rule));
            var adminId = Player?.UserId.ToString() ?? "unknown";
            var adminName = Player?.Name ?? "unknown";
            var newRule = MapToServerRule(rule);
            LogAdminAutoModAction("Created", adminName, adminId, null, newRule);
            LoadFromDb();
            await RefreshAutomodCacheAsync();
        }

        public async void UpdateRule(SharedAutoModRule rule)
        {
            var oldRule = _rules.FirstOrDefault(r => r.Id == rule.Id);
            var adminId = Player?.UserId.ToString() ?? "unknown";
            var adminName = Player?.Name ?? "unknown";
            var newRule = MapToServerRule(rule);
            if (oldRule != null && (
                oldRule.Regex != newRule.Regex ||
                oldRule.Enabled != newRule.Enabled ||
                !OffencesEqual(oldRule, newRule)))
            {
                LogAdminAutoModAction("Edited", adminName, adminId, oldRule, newRule);
            }
            await _db.UpdateAutoModRule(newRule);
            LoadFromDb();
            await RefreshAutomodCacheAsync();
        }

        public async void BulkUpdateRules(List<SharedAutoModRule> rules)
        {
            var adminId = Player?.UserId.ToString() ?? "unknown";
            var adminName = Player?.Name ?? "unknown";
            foreach (var rule in rules)
            {
                var oldRule = _rules.FirstOrDefault(r => r.Id == rule.Id);
                var newRule = MapToServerRule(rule);
                if (oldRule != null)
                {
                    if (
                        oldRule.Regex != newRule.Regex ||
                        oldRule.Enabled != newRule.Enabled ||
                        !OffencesEqual(oldRule, newRule))
                    {
                        LogAdminAutoModAction("Edited", adminName, adminId, oldRule, newRule);
                    }
                    await _db.UpdateAutoModRule(newRule);
                }
                else
                {
                    // New rule, add it
                    await _db.AddAutoModRule(newRule);
                    LogAdminAutoModAction("Created", adminName, adminId, null, newRule);
                }
            }

            // 2. Delete rules that are in the DB but not in the incoming list
            var incomingIds = rules.Select(r => r.Id).ToHashSet();
            foreach (var dbRule in _rules)
            {
                if (!incomingIds.Contains(dbRule.Id))
                {
                    await _db.DeleteAutoModRule(dbRule.Id);
                    LogAdminAutoModAction("Deleted", adminName, adminId, dbRule, null);
                }
            }

            LoadFromDb();
            await RefreshAutomodCacheAsync();
        }

        //message handler
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

        public override EuiStateBase GetNewState()
        {
            return new AutoModEuiState
            {
                Rules = _rules.Select(MapToSharedRule).ToList()
            };
        }

        private static async Task RefreshAutomodCacheAsync()
        {
            var sysMan = IoCManager.Resolve<IEntitySystemManager>();
            var automod = sysMan.GetEntitySystem<Content.Server.Starlight.Chat.Systems.AutoModSystem>();
            if (automod != null)
            {
                await automod.UpdateCache();
            }
        }

        // Helper to format offences for logging
        private static string FormatOffences(ServerAutoModRule rule)
        {
            if (rule.Offences == null || rule.Offences.Count == 0)
                return "    (none)";
            var lines = new List<string>();
            for (int i = 0; i < rule.Offences.Count; i++)
            {
                var o = rule.Offences[i];
                lines.Add($"    {i + 1}. [{o.Action}] {o.Message}");
            }
            return string.Join("\n", lines);
        }

        private static bool OffencesEqual(ServerAutoModRule a, ServerAutoModRule b)
        {
            if (a.Offences == null && b.Offences == null) return true;
            if (a.Offences == null || b.Offences == null) return false;
            if (a.Offences.Count != b.Offences.Count) return false;
            for (int i = 0; i < a.Offences.Count; i++)
            {
                if (a.Offences[i].Action != b.Offences[i].Action) return false;
                if (a.Offences[i].Message != b.Offences[i].Message) return false;
                if (a.Offences[i].BanDurationMinutes != b.Offences[i].BanDurationMinutes) return false;
                if (a.Offences[i].DecaySeconds != b.Offences[i].DecaySeconds) return false;
                if (a.Offences[i].CancelSpeech != b.Offences[i].CancelSpeech) return false;
            }
            return true;
        }

        // Helper to log admin actions for automod changes
        private void LogAdminAutoModAction(string action, string adminName, string adminId, ServerAutoModRule? before, ServerAutoModRule? after)
        {
            var beforeText = before != null ? $"Regex:         {before.Regex}\nEnabled:       {before.Enabled}\nOffences:\n{FormatOffences(before)}" : string.Empty;
            var afterText = after != null ? $"Regex:         {after.Regex}\nEnabled:       {after.Enabled}\nOffences:\n{FormatOffences(after)}" : string.Empty;
            switch (action)
            {
                case "Created":
                    _adminLogger.Add(LogType.AdminCommands, LogImpact.High, $"[AutoMod] Rule Created by {adminName} ({adminId})\n───────────────────────────────\n{afterText}");
                    break;
                case "Deleted":
                    _adminLogger.Add(LogType.AdminCommands, LogImpact.High, $"[AutoMod] Rule Deleted by {adminName} ({adminId})\n───────────────────────────────\n{beforeText}");
                    break;
                case "Edited":
                    _adminLogger.Add(LogType.AdminCommands, LogImpact.High, $"[AutoMod] Rule Edited by {adminName} ({adminId}) (ID: {after?.Id})\n────── Before ──────\n{beforeText}\n────── After ──────\n{afterText}");
                    break;
            }
        }
    }
}
