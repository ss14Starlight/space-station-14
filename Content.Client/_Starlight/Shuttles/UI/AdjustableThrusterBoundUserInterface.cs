using Content.Shared._Starlight.Shuttles;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Shuttles.UI;

[UsedImplicitly]
public sealed class AdjustableThrusterBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private AdjustableThrusterWindow? _window;

    public AdjustableThrusterBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AdjustableThrusterWindow>();

        _window.OnThrustChanged += thrust => SendMessage(new AdjustableThrusterSetThrustMessage(thrust));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is AdjustableThrusterBuiState thrusterState)
            _window?.UpdateState(thrusterState);
    }
}
