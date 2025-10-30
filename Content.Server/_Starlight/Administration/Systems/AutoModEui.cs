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
using SharedAutoModSeverity = Content.Shared.Administration.AutoModSeverity;


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
                Severity = (int)sharedRule.Severity,
                Count = sharedRule.Count,
                Enabled = sharedRule.Enabled,
                CancelSpeech = sharedRule.CancelSpeech,
                Offences = sharedRule.Offences?.Select(o => new ServerAutoModOffence
                {
                    Message = o.Message,
                    Action = (int)o.Action,
                    BanDurationSeconds = o.BanDurationSeconds,
                    DecaySeconds = o.DecaySeconds
                }).ToList() ?? new List<ServerAutoModOffence>()
            };
        }

        private static SharedAutoModRule MapToSharedRule(ServerAutoModRule serverRule)
        {
            return new SharedAutoModRule
            {
                Id = serverRule.Id,
                Regex = serverRule.Regex,
                Severity = (SharedAutoModSeverity)serverRule.Severity,
                Count = serverRule.Count,
                Enabled = serverRule.Enabled,
                CancelSpeech = serverRule.CancelSpeech,
                Offences = serverRule.Offences?.Select(o => new SharedAutoModOffence
                {
                    Message = o.Message,
                    Action = (Content.Shared.Administration.AutoModOffenceAction)o.Action,
                    BanDurationSeconds = o.BanDurationSeconds,
                    DecaySeconds = o.DecaySeconds
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
            {
                _adminLogger.Add(LogType.AdminCommands, LogImpact.High,
                    $"""
                    [AutoMod] Rule Deleted by {adminName} ({adminId})
                    ───────────────────────────────
                    Regex:         {oldRule.Regex}
                    Severity:      {oldRule.Severity}
                    Count:         {oldRule.Count}
                    Enabled:       {oldRule.Enabled}
                    CancelSpeech:  {oldRule.CancelSpeech}
                    Offences:
{FormatOffences(oldRule)}
""");
            }
            LoadFromDb();
            await RefreshAutomodCacheAsync();
        }

        public async void AddRule(SharedAutoModRule rule)
        {
            await _db.AddAutoModRule(MapToServerRule(rule));
            var adminId = Player?.UserId.ToString() ?? "unknown";
            var adminName = Player?.Name ?? "unknown";
            var newRule = MapToServerRule(rule);
            _adminLogger.Add(LogType.AdminCommands, LogImpact.High,
                $"""
                [AutoMod] Rule Created by {adminName} ({adminId})
                ───────────────────────────────
                Regex:         {newRule.Regex}
                Severity:      {newRule.Severity}
                Count:         {newRule.Count}
                Enabled:       {newRule.Enabled}
                CancelSpeech:  {newRule.CancelSpeech}
                Offences:
{FormatOffences(newRule)}
""");
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
                oldRule.Severity != newRule.Severity ||
                oldRule.Count != newRule.Count ||
                oldRule.Enabled != newRule.Enabled ||
                oldRule.CancelSpeech != newRule.CancelSpeech ||
                !OffencesEqual(oldRule, newRule)))
            {
                _adminLogger.Add(LogType.AdminCommands, LogImpact.High,
                    $"""
                    [AutoMod] Rule Edited by {adminName} ({adminId}) (ID: {newRule.Id})
                    ────── Before ──────
                    Regex:         {oldRule.Regex}
                    Severity:      {oldRule.Severity}
                    Count:         {oldRule.Count}
                    Enabled:       {oldRule.Enabled}
                    CancelSpeech:  {oldRule.CancelSpeech}
                    Offences:
{FormatOffences(oldRule)}
                    ────── After ──────
                    Regex:         {newRule.Regex}
                    Severity:      {newRule.Severity}
                    Count:         {newRule.Count}
                    Enabled:       {newRule.Enabled}
                    CancelSpeech:  {newRule.CancelSpeech}
                    Offences:
{FormatOffences(newRule)}
""");
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
                        oldRule.Severity != newRule.Severity ||
                        oldRule.Count != newRule.Count ||
                        oldRule.Enabled != newRule.Enabled ||
                        oldRule.CancelSpeech != newRule.CancelSpeech ||
                        !OffencesEqual(oldRule, newRule))
                    {
                        _adminLogger.Add(LogType.AdminCommands, LogImpact.High,
                            $"""
                            [AutoMod] Rule Edited by {adminName} ({adminId}) (ID: {newRule.Id})
                            ────── Before ──────
                            Regex:         {oldRule.Regex}
                            Severity:      {oldRule.Severity}
                            Count:         {oldRule.Count}
                            Enabled:       {oldRule.Enabled}
                            CancelSpeech:  {oldRule.CancelSpeech}
                            Offences:
{FormatOffences(oldRule)}
                            ────── After ──────
                            Regex:         {newRule.Regex}
                            Severity:      {newRule.Severity}
                            Count:         {newRule.Count}
                            Enabled:       {newRule.Enabled}
                            CancelSpeech:  {newRule.CancelSpeech}
                            Offences:
{FormatOffences(newRule)}
""");
                    }
                }
                await _db.UpdateAutoModRule(newRule);
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
            return new AutoModEuiState()
            {
                Rules = _rules.Select(MapToSharedRule).ToList(),
            };
        }

        private static async Task RefreshAutomodCacheAsync()
        {
            var sysMan = IoCManager.Resolve<IEntitySystemManager>();
            var automod = sysMan.GetEntitySystem<AutoModSystem>();
            // await automod.UpdateCache(); // TODO: Implement or fix UpdateCache on AutoModSystem
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
            }
            return true;
        }
    }
}
