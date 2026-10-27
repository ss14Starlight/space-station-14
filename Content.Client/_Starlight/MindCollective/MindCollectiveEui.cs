using Content.Client.Eui;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Starlight.MindCollective;

[UsedImplicitly]
public sealed class MindCollectiveEui : BaseEui
{
    private readonly MindCollectiveWindow _window = new();

    public MindCollectiveEui()
    {
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnEnter += body => SendMessage(new EnterCollectiveBodyMessage(body));
        _window.OnObserve += body => SendMessage(new ObserveCollectiveBodyMessage(body));
        _window.OnRecall += mind => SendMessage(new RecallCollectiveMemberMessage(mind));
        _window.OnControl += body => SendMessage(new ControlCollectiveBodyMessage(body));
        _window.OnSetRole += (mind, role, assigned) => SendMessage(new SetCollectiveRoleMessage(mind, role, assigned));
        _window.OnLeave += () => SendMessage(new LeaveCollectiveBodyMessage());
        _window.OnExpel += mind => SendMessage(new ExpelCollectiveMemberMessage(mind));
        _window.OnConfigureControl += (mind, body) => SendMessage(new ConfigureCollectiveControlMessage(mind, body));
        _window.OnSetControl += (mind, body, access) => SendMessage(new SetCollectiveControlMessage(mind, body, access));
    }

    public override void Opened() => _window.OpenCentered();

    public override void Closed()
    {
        _window.Dispose();
        base.Closed();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is MindCollectiveUiState menu)
            _window.UpdateState(menu);
    }
}
