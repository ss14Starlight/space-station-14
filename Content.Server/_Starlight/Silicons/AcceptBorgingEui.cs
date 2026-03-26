using Content.Server.EUI;
using Content.Server.Silicons.Borgs;
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.Eui;
using Content.Shared.Mind;

namespace Content.Server._Starlight.Silicons;

public sealed class AcceptBorgingEui(EntityUid brain, EntityUid mindId, MindComponent mind, BorgSystem borgingSys) : BaseEui
{
    private readonly EntityUid _brain = brain;
    private readonly EntityUid _mindId = mindId;
    private readonly MindComponent _mind = mind;
    private readonly BorgSystem _borgingSystem = borgingSys;

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not AcceptBorgingChoiceMessage choice
        ||choice.Button != AcceptBorgingUiButton.Accept)
        {
            Close();
            _borgingSystem.OpenGhostRole(_brain, _mindId, _mind);
            return;
        }

        _borgingSystem.TransferMindToChassis(_brain, _mindId, _mind);
        Close();
    }
}
