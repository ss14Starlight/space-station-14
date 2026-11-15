using Content.Shared._Starlight.StationRadio;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.StationRadio.UI;

[UsedImplicitly]
public sealed class BroadcastRecorderBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private BroadcastRecorderWindow? _window;

    public BroadcastRecorderBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BroadcastRecorderWindow>();
        _window.OnAction += action => SendMessage(new BroadcastRecorderActionMessage(action));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BroadcastRecorderState recorderState)
            _window?.UpdateState(recorderState);
    }
}
