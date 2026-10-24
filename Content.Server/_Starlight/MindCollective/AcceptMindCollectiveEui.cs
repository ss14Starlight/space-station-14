using Content.Server.EUI;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Eui;

namespace Content.Server._Starlight.MindCollective;

public sealed class AcceptMindCollectiveEui(
    EntityUid collective,
    EntityUid mind,
    MindCollectiveSystem system,
    string name,
    string title,
    string description) : BaseEui
{
    private bool _resolved;
    public bool IsClosed { get; private set; }

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override EuiStateBase GetNewState()
        => new MindCollectiveConsentState(name, title, description);

    public override void HandleMessage(EuiMessageBase message)
    {
        if (_resolved || IsClosed)
            return;
        if (message is not MindCollectiveConsentMessage choice)
        {
            base.HandleMessage(message);
            return;
        }

        _resolved = true;
        system.RespondJoin(collective, mind, choice.Accepted);
        Dismiss();
    }

    public override void Closed()
    {
        // Disconnect calls Closed directly while EuiManager enumerates its open windows.
        IsClosed = true;
        base.Closed();
        if (_resolved)
            return;
        _resolved = true;
        system.RespondJoin(collective, mind, false);
    }

    public void Dismiss()
    {
        _resolved = true;
        if (!IsClosed && !IsShutDown)
            Close();
    }
}
