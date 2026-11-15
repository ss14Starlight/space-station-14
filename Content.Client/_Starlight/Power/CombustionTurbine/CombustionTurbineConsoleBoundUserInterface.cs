using Content.Shared._Starlight.Power.CombustionTurbine;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Power.CombustionTurbine;

[UsedImplicitly]
public sealed class CombustionTurbineConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private CombustionTurbineConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CombustionTurbineConsoleWindow>();
        _window.OnToggle += enabled => SendMessage(new CombustionTurbineToggleMessage(enabled));
        _window.OnIntakeChanged += ratio => SendMessage(new CombustionTurbineIntakeMessage(ratio));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CombustionTurbineConsoleState cast)
            _window?.UpdateState(cast);
    }
}
