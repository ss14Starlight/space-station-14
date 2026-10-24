using Content.Server.EUI;
using Content.Shared._Starlight.Changeling;
using Content.Shared.Eui;

namespace Content.Server._Starlight.Changeling;

public sealed class AcceptChangelingHiveEui(EntityUid master, EntityUid mind, ChangelingSystem changeling) : BaseEui
{
    private bool _resolved;
    public bool IsClosed { get; private set; }

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (_resolved || IsClosed)
            return;
        if (msg is not AcceptChangelingHiveChoiceMessage choice)
        {
            base.HandleMessage(msg);
            return;
        }

        _resolved = true;
        changeling.RespondHiveConsent((master, null), mind, choice.Accepted);
        if (!IsClosed && !IsShutDown)
            Close();
    }

    public override void Closed()
    {
        IsClosed = true;
        base.Closed();
        if (_resolved)
            return;
        _resolved = true;
        changeling.RespondHiveConsent((master, null), mind, false);
    }
}
