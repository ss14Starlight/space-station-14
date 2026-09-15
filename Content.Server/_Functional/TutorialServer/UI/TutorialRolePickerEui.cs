using Content.Server.EUI;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Eui;
using Robust.Shared.Player;

namespace Content.Server._Functional.TutorialServer.UI;

public sealed class TutorialRolePickerEui : BaseEui
{
    private readonly TutorialServerRuleSystem _tutorial;
    private readonly List<TutorialRolePickerEntry> _roles;

    public TutorialRolePickerEui(TutorialServerRuleSystem tutorial, List<TutorialRolePickerEntry> roles)
    {
        _tutorial = tutorial;
        _roles = roles;
    }

    public override EuiStateBase GetNewState()
    {
        return new TutorialRolePickerEuiState(_roles);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is TutorialSelectRoleMessage select)
            _tutorial.TrySelectRole(Player, select.RoleId, select.ConfirmedStub);
        else if (msg is TutorialQuitPickerMessage)
            _tutorial.OnPickerQuit(Player);
    }

    public override void Closed()
    {
        base.Closed();
        _tutorial.OnPickerClosed(Player);
    }
}
