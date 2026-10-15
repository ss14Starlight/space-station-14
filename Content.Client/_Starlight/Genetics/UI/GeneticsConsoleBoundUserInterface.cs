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
        _window.OnRenameGene += (uid, newName) =>
            SendPredictedMessage(new GeneticsConsoleRenameGeneMessage(_ent.GetNetEntity(uid), newName));
        _window.OnClearInputs += () => SendPredictedMessage(new GeneticsConsoleClearInputsMessage());
        _window.OnDeleteGene += uid => SendPredictedMessage(new GeneticsConsoleDeleteGeneMessage(_ent.GetNetEntity(uid)));
        _window.OnDispenseSampler += () => SendPredictedMessage(new GeneticsConsoleDispenseSamplerMessage());
    }

    // Upon receiving any kind of message just refresh the whole UI.
    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is not GeneticsConsoleRefreshUiMessage) return;
        _window?.RefreshUI();
    }
}
