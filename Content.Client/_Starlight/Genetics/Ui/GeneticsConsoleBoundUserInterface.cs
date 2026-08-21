using Content.Shared._Starlight.Genetics;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Genetics.Ui;

public sealed class GeneticsConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    public GeneticsConsoleWindow _window = default!;

    public GeneticsConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GeneticsConsoleWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not GeneticsConsoleState cast)
            return;

        _window.UpdateState(cast);

        _window.OnRenameGenePressed += data =>
        {
            SendPredictedMessage(new GeneticsConsoleRenameGeneMessage(data));
        };
    }
}

