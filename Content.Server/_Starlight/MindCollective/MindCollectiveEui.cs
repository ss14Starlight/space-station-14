using Content.Server.EUI;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Server._Starlight.MindCollective;

/// <summary>The collective menu follows its participant's session across body transfers.</summary>
[UsedImplicitly]
public sealed class MindCollectiveEui(MindCollectiveSystem system, EntityUid collective, EntityUid mind) : BaseEui
{
    public readonly EntityUid Collective = collective;
    public readonly EntityUid Mind = mind;
    public EntityUid? EditingMind;
    public EntityUid? EditingBody;
    public bool IsClosed { get; private set; }

    public override void Opened() => StateDirty();

    public override EuiStateBase GetNewState() => system.GetMenuState(this);

    public override void HandleMessage(EuiMessageBase message)
    {
        if (message is CloseEuiMessage)
            base.HandleMessage(message);
        else if (!IsClosed && !system.HandleMenuMessage(this, message))
            Close();
    }

    public override void Closed()
    {
        IsClosed = true;
        system.MenuClosed(this);
        base.Closed();
    }
}
