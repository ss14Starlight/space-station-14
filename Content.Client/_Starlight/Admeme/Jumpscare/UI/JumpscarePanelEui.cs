using Content.Client.Eui;
using Content.Shared._Starlight.Admeme.Jumpscare;
using Content.Shared.Eui;

namespace Content.Client._Starlight.Admeme.Jumpscare.UI;

public sealed partial class JumpscarePanelEui : BaseEui
{
    [Dependency] private IEntityManager _ent = null!;

    private readonly JumpscareEntityPanel _window;

    public JumpscarePanelEui()
    {
        IoCManager.InjectDependencies(this);
        _window = new JumpscareEntityPanel();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnDoJumpscare += (data, targets) => SendMessage(new JumpscareTargetMessage([..targets], data));
        _window.OnRequestPlayers += () => SendMessage(new JumpscareRefreshPlayersMessage());
    }

    public override void Opened() => _window.OpenCentered();

    public override void Closed() => _window.Close();

    public override void HandleState(EuiStateBase state)
    {
        var pState = (JumpscarePanelEuiState)state;
        _window.SetState(pState.LockedTarget, pState.Players);
    }
}
