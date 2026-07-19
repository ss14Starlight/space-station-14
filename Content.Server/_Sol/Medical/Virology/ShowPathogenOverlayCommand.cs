using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Sol.Medical.Virology;

[AdminCommand(AdminFlags.Debug)]
public sealed class ShowPathogenOverlayCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "showpathogens";
    public string Description => "Toggles seeing the airborne pathogen debug overlay.";
    public string Help => $"Usage: {Command}";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;
        if (player == null)
        {
            shell.WriteLine("You must be a player to use this command.");
            return;
        }

        var sys = _entities.System<PathogenDebugOverlaySystem>();
        var enabled = sys.ToggleObserver(player);
        shell.WriteLine(enabled
            ? "Enabled the pathogen debug overlay."
            : "Disabled the pathogen debug overlay.");
    }
}
