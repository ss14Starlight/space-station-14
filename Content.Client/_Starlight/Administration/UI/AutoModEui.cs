using Content.Client.Eui;
using Content.Shared._Starlight.Administration;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Shared.Log;
using System.Linq;
using static Content.Shared._Starlight.Administration.AutoModEuiMsg;

namespace Content.Client._Starlight.Administration.UI;

[UsedImplicitly]
public sealed class AutoModEui : BaseEui
{
    private readonly AutoModWindow _window;

    public AutoModEui()
    {
        _window = new AutoModWindow(this);
    }

    public override void Closed()
    {
        base.Closed();
        SendMessage(new CloseEuiMessage());
        _window.Close();
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);
        
        if (state is not AutoModEuiState autoModState)
            return;

        _window.UpdateRules(autoModState.Rules);
    }

    public override void HandleMessage(EuiMessageBase message)
    {
        base.HandleMessage(message);

        switch (message)
        {
            case ValidationErrorResponse error:
                _window.ShowValidationError(error.BlacklistedWord, error.Pattern);
                break;
        }
    }

    public void SendRefreshRequest()
    {
        SendMessage(new RefreshRequest());
    }

    public void SendBulkUpdate(List<AutoModRule> rules)
    {
        var logger = Logger.GetSawmill("automod");
        logger.Info($"[AutoMod Debug Client] SendBulkUpdate called with {rules.Count} rules");
        
        foreach (var rule in rules)
        {
            logger.Info($"[AutoMod Debug Client] Rule ID {rule.Id}: Regex='{rule.Regex}', Severity={rule.Severity}");
            if (rule.Offences?.Count > 0)
            {
                var offenceDetails = string.Join("; ", rule.Offences.Select((o, i) => $"Offence{i+1}: Action={o.Action}({(int)o.Action}), Message='{o.Message}'"));
                logger.Info($"[AutoMod Debug Client] Rule offences: {offenceDetails}");
            }
        }
        
        SendMessage(new BulkUpdateRulesRequest(rules));
    }

    public void SendDeleteRequest(AutoModRule rule)
    {
        SendMessage(new DeleteRuleRequest(rule));
    }
}
