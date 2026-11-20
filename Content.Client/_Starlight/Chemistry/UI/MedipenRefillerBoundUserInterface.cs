using Content.Shared.Chemistry;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Chemistry.UI;

/// <summary>
/// Opens the <see cref="MedipenRefillerWindow"/> and sends its eject buttons to the server.
/// </summary>
[UsedImplicitly]
public sealed class MedipenRefillerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private MedipenRefillerWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MedipenRefillerWindow>();
        _window.SetOwner(Owner);
        _window.OnEjectJugButtonPressed += location => SendPredictedMessage(new ReagentDispenserEjectContainerMessage(location));
    }
}
