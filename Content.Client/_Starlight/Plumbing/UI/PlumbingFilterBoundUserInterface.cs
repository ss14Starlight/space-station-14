using Content.Shared._Starlight.Plumbing;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Plumbing.UI;

[UsedImplicitly]
public sealed class PlumbingFilterBoundUserInterface : BoundUserInterface
{
    private PlumbingFilterWindow? _window;

    public PlumbingFilterBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PlumbingFilterWindow>();

        _window.OnToggle += OnToggle;
        _window.OnAddReagent += OnAddReagent;
        _window.OnRemoveReagent += OnRemoveReagent;
        _window.OnClear += OnClear;
    }

    private void OnToggle(bool enabled)
        => SendMessage(new PlumbingFilterToggleMessage(enabled));

    private void OnAddReagent(string reagentId)
        => SendMessage(new PlumbingFilterAddReagentMessage(reagentId));

    private void OnRemoveReagent(string reagentId)
        => SendMessage(new PlumbingFilterRemoveReagentMessage(reagentId));

    private void OnClear()
        => SendMessage(new PlumbingFilterClearMessage());

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not PlumbingFilterBoundUserInterfaceState cast)
            return;

        _window.UpdateState(cast);
    }
}
