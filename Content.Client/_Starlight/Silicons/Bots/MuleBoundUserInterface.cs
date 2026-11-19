using Content.Shared._Starlight.Silicons.Bots;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Silicons.Bots;

[UsedImplicitly]
public sealed class MuleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private MuleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MuleWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.OnCommand += command => SendMessage(new MuleCommandMessage(command));
        _window.OnBeaconPicked += (beacon, home) => SendMessage(new MuleSetDestinationMessage(beacon, home));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is MuleBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
