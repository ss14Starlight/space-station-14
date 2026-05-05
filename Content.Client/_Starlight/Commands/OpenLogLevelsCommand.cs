using Content.Client._Starlight.Logs;
using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client._Starlight.Commands;

public sealed class OpenLogLevelsCommand : IConsoleCommand
{
    public string Command => "logs";
    public string Description => "Open the sawmill log level configuration window.";
    public string Help => "logs";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var window = new LogLevelsWindow();
        window.OpenCentered();
    }
}
