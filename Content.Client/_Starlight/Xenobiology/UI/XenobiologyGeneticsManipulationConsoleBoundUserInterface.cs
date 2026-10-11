using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Xenobiology.UI;

public sealed partial class XenobiologyGeneticsManipulationConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables] private XenobiologyGeneticsManipulationConsoleWindow? _window;

    public XenobiologyGeneticsManipulationConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) =>
        IoCManager.InjectDependencies(this);

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<XenobiologyGeneticsManipulationConsoleWindow>();
    }
}
