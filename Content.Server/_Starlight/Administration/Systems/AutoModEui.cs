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


namespace Content.Server.Administration.UI
{
    public sealed class AutoModEui : BaseEui
    {
        [Dependency] private readonly IServerDbManager _db = default!;
        [Dependency] private readonly Content.Server.Administration.Logs.IAdminLogManager _adminLogger = default!;
        private List<AutoModRule> _rules = new();
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

        public async void DeleteRule(AutoModRule rule)
        {
            //delete the rule from the database
            await _db.DeleteAutoModRule(rule.Id);
            var adminId = Player?.UserId.ToString() ?? "unknown";
            var adminName = Player?.Name ?? "unknown";
            _adminLogger.Add(LogType.AdminCommands, LogImpact.High,
                $"""
                [AutoMod] Rule Deleted by {adminName} ({adminId})
                ───────────────────────────────
                Regex:         {rule.Regex}
                Severity:      {rule.Severity}
                Message:       {rule.Message}
                Count:         {rule.Count}
                Enabled:       {rule.Enabled}
                CancelSpeech:  {rule.CancelSpeech}
                """);
            LoadFromDb();
            // Ensure the runtime cache is refreshed even on SQLite (no notifications there)
            await RefreshAutomodCacheAsync();
        }

        public async void AddRule(AutoModRule rule)
        {
            //add the rule to the database
            await _db.AddAutoModRule(rule);
            var adminId = Player?.UserId.ToString() ?? "unknown";
            var adminName = Player?.Name ?? "unknown";
            _adminLogger.Add(LogType.AdminCommands, LogImpact.High,
                $"""
                [AutoMod] Rule Created by {adminName} ({adminId})
                ───────────────────────────────
                Regex:         {rule.Regex}
                Severity:      {rule.Severity}
                Message:       {rule.Message}
                Count:         {rule.Count}
                Enabled:       {rule.Enabled}
                CancelSpeech:  {rule.CancelSpeech}
                """);
            LoadFromDb();
            await RefreshAutomodCacheAsync();
        }

        public async void UpdateRule(AutoModRule rule)
        {
            //update the rule in the database
            var oldRule = _rules.FirstOrDefault(r => r.Id == rule.Id);
            var adminId = Player?.UserId.ToString() ?? "unknown";
            var adminName = Player?.Name ?? "unknown";
            if (oldRule != null && (
                oldRule.Regex != rule.Regex ||
                oldRule.Severity != rule.Severity ||
                oldRule.Message != rule.Message ||
                oldRule.Count != rule.Count ||
                oldRule.Enabled != rule.Enabled ||
                oldRule.CancelSpeech != rule.CancelSpeech))
            {
                _adminLogger.Add(LogType.AdminCommands, LogImpact.High,
                    $"""
                    [AutoMod] Rule Edited by {adminName} ({adminId}) (ID: {rule.Id})
                    ────── Before ──────
                    Regex:         {oldRule.Regex}
                    Severity:      {oldRule.Severity}
                    Message:       {oldRule.Message}
                    Count:         {oldRule.Count}
                    Enabled:       {oldRule.Enabled}
                    CancelSpeech:  {oldRule.CancelSpeech}
                    ────── After ──────
                    Regex:         {rule.Regex}
                    Severity:      {rule.Severity}
                    Message:       {rule.Message}
                    Count:         {rule.Count}
                    Enabled:       {rule.Enabled}
                    CancelSpeech:  {rule.CancelSpeech}
                    """);
            }
            await _db.UpdateAutoModRule(rule);
            LoadFromDb();
            await RefreshAutomodCacheAsync();
        }

        public async void BulkUpdateRules(List<AutoModRule> rules)
        {
            //update all rules in the database and log edits
            var adminId = Player?.UserId.ToString() ?? "unknown";
            var adminName = Player?.Name ?? "unknown";
            foreach (var rule in rules)
            {
                var oldRule = _rules.FirstOrDefault(r => r.Id == rule.Id);
                if (oldRule != null && (
                    oldRule.Regex != rule.Regex ||
                    oldRule.Severity != rule.Severity ||
                    oldRule.Message != rule.Message ||
                    oldRule.Count != rule.Count ||
                    oldRule.Enabled != rule.Enabled ||
                    oldRule.CancelSpeech != rule.CancelSpeech))
                {
                    _adminLogger.Add(LogType.AdminCommands, LogImpact.High,
                        $"""
                        [AutoMod] Rule Edited by {adminName} ({adminId}) (ID: {rule.Id})
                        ────── Before ──────
                        Regex:         {oldRule.Regex}
                        Severity:      {oldRule.Severity}
                        Message:       {oldRule.Message}
                        Count:         {oldRule.Count}
                        Enabled:       {oldRule.Enabled}
                        CancelSpeech:  {oldRule.CancelSpeech}
                        ────── After ──────
                        Regex:         {rule.Regex}
                        Severity:      {rule.Severity}
                        Message:       {rule.Message}
                        Count:         {rule.Count}
                        Enabled:       {rule.Enabled}
                        CancelSpeech:  {rule.CancelSpeech}
                        """);
                }
                await _db.UpdateAutoModRule(rule);
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
                Rules = _rules,
            };
        }

        private static async Task RefreshAutomodCacheAsync()
        {
            var sysMan = IoCManager.Resolve<IEntitySystemManager>();
            var automod = sysMan.GetEntitySystem<AutoModSystem>();
            await automod.UpdateCache();
        }
    }
}
