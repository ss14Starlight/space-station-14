using Content.Shared._Starlight.Genetics;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Genetics.UI;

public sealed partial class GeneticsConsoleBoundUserInterface : BoundUserInterface
{
    [Dependency] private IEntityManager _ent = null!;

    [ViewVariables] private GeneticsConsoleWindow? _window;

    public GeneticsConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<GeneticsConsoleWindow>();
        _window.SetEntity(Owner);
        _window.OnStage += uid => SendPredictedMessage(new GeneticsConsoleStageGeneMessage(_ent.GetNetEntity(uid)));
        _window.OnUnstage += uid => SendPredictedMessage(new GeneticsConsoleUnstageGeneMessage(_ent.GetNetEntity(uid)));
        _window.OnSetGenePoolSort += index => SendPredictedMessage(new GeneticsConsoleSetSortIndex(0, index));
        _window.OnSetInputGenesSort += index => SendPredictedMessage(new GeneticsConsoleSetSortIndex(1, index));
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (_window is null) return;
        switch (message)
        {
            case GeneticsConsoleRefreshUIMessage:
                _window.RefreshUI();
                return;
        }
    }
}
