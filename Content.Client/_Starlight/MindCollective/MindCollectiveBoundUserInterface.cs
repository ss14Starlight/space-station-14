using Content.Shared._Starlight.MindCollective;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.MindCollective;

[UsedImplicitly]
public sealed class MindCollectiveBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private MindCollectiveWindow? _window;
    private MindCollectiveUiState? _lastState;

    protected override void Open()
    {
        base.Open();
        _lastState = null;
        _window = this.CreateWindow<MindCollectiveWindow>();
        // The actor can change during a transfer; wait for the server to close the interface.
        _window.OnClose -= Close;
        _window.OnClose += () => SendMessage(new CloseBoundInterfaceMessage());
        _window.OnEnter += body => SendMessage(new EnterCollectiveBodyMessage(body));
        _window.OnObserve += body => SendMessage(new ObserveCollectiveBodyMessage(body));
        _window.OnRecall += mind => SendMessage(new RecallCollectiveMemberMessage(mind));
        _window.OnLeave += () => SendMessage(new LeaveCollectiveBodyMessage());
        _window.OnExpel += (mind, request, confirmed) =>
            SendMessage(new ExpelCollectiveMemberMessage(mind, request, confirmed));
        Update();
    }

    public override void Update()
    {
        base.Update();
        if (!EntMan.TryGetComponent<MindCollectiveUiComponent>(Owner, out var menu)
            || menu.State is not { } state || ReferenceEquals(_lastState, state))
            return;
        _lastState = state;
        _window?.UpdateState(state);
    }
}
