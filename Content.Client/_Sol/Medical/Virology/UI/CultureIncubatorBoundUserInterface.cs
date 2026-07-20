using Content.Shared._Sol.Medical.Virology;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Sol.Medical.Virology.UI;

[UsedImplicitly]
public sealed class CultureIncubatorBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private CultureIncubatorWindow? _window;

    private CultureIncubatorBoundUserInterfaceState? _pending;

    public CultureIncubatorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CultureIncubatorWindow>();
        _window.OnStart += () => SendMessage(new CultureIncubatorStartMessage());
        _window.OnRetrieve += () => SendMessage(new CultureIncubatorRetrieveMessage());
        _window.OnEjectAll += () => SendMessage(new CultureIncubatorEjectAllMessage());
        _window.OnEjectSample += netEnt => SendMessage(new CultureIncubatorEjectSampleMessage(netEnt));

        if (_pending != null)
            _window.UpdateState(_pending);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not CultureIncubatorBoundUserInterfaceState cState)
            return;

        _pending = cState;
        _window?.UpdateState(cState);
    }
}
