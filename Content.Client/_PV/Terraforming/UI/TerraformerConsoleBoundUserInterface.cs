using Content.Shared._PV.Terraforming;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._PV.Terraforming.UI;

[UsedImplicitly]
public sealed class TerraformerConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private TerraformerConsoleWindow? _window;

    public TerraformerConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<TerraformerConsoleWindow>();

        _window.OnRefreshPressed += () =>
        {
            SendMessage(new TerraformerConsoleRefreshMessage());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not TerraformerConsoleBoundInterfaceState castState)
            return;

        _window?.UpdateState(castState);
    }
}
