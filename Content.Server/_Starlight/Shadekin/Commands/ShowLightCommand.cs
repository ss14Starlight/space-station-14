using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Shadekin.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class ShowLightCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _e = default!;

    public string Command => "showlight";
    public string Description => "Toggles seeing the light grid debug overlay";
    public string Help => $"Usage: {Command}";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;
        if (player == null)
        {
            shell.WriteLine("You must be a player to use this command");
            return;
        }

        var lightDebug = _e.System<LightDebugOverlaySystem>();
        var enabled = lightDebug.ToggleObserver(player);

        shell.WriteLine(enabled
            ? "Enabled the light grid debug overlay"
            : "Disabled the light grid debug overlay");
    }
}
