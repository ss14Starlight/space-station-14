using Content.Client._Daiquiri.Planets.UI;
using Content.Shared._Daiquiri.Planets;
using Robust.Client.UserInterface;

namespace Content.Client._Daiquiri.Planets;

public sealed class PlanetConsoleBoundUserInterface : BoundUserInterface
{
    private PlanetConsoleWindow? _window;

    public PlanetConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindowCenteredLeft<PlanetConsoleWindow>();
        _window.OnPrint += planet => SendMessage(new PlanetConsolePrintMessage { Planet = planet });
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is PlanetConsoleState consoleState)
            _window?.UpdateState(consoleState);
    }
}