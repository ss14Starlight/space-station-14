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


namespace Content.Server.Administration.UI
{
    public sealed class AutoModEui : BaseEui
    {
        [Dependency] private readonly IServerDbManager _db = default!;
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

            LoadFromDb();
            // Ensure the runtime cache is refreshed even on SQLite (no notifications there)
            await RefreshAutomodCacheAsync();
        }

        public async void AddRule(AutoModRule rule)
        {
            //add the rule to the database
            await _db.AddAutoModRule(rule);

            LoadFromDb();
            await RefreshAutomodCacheAsync();
        }

        public async void UpdateRule(AutoModRule rule)
        {
            //update the rule in the database
            await _db.UpdateAutoModRule(rule);

            LoadFromDb();
            await RefreshAutomodCacheAsync();
        }

        public async void BulkUpdateRules(List<AutoModRule> rules)
        {
            //update all rules in the database
            foreach (var rule in rules)
            {
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
