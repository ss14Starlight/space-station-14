using Content.Shared._Starlight.Power.BluespaceHarvester;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Power.UI;

public sealed class BluespaceHarvesterBoundUserInterface : BoundUserInterface
{
    private BluespaceHarvesterWindow? _window;

    public BluespaceHarvesterBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey){}

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BluespaceHarvesterWindow>();
        _window.OnDesiredLevelChanged += level => SendMessage(new BluespaceHarvesterSetLevelMessage(level));
        _window.OnPoolRequested += poolId => SendMessage(new BluespaceHarvesterPurchaseMessage(poolId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not BluespaceHarvesterUiState cast)
            return;

        _window.UpdateState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _window?.Close();
    }
}
