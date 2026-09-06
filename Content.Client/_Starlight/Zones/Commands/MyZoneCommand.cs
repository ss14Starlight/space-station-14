using Content.Shared._Starlight.Zones;
using Robust.Client.Player;
using Robust.Shared.Console;

namespace Content.Client._Starlight.Zones.Commands;

public sealed partial class MyZoneCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;

    public string Command => "myzone";
    public string Description => "Prints the zone you are currently in.";
    public string Help => $"Usage: {Command}";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_player.LocalEntity is not { } player)
        {
            shell.WriteError("You are not attached to an entity.");
            return;
        }

        if (!_entMan.TryGetComponent(player, out ZoneTrackerComponent? tracker))
        {
            shell.WriteLine("The server is not tracking zones for you.");
            return;
        }

        shell.WriteLine(tracker.Zone is { } zone
            ? $"You are in {zone.Id}."
            : "You are not in any zone.");
    }
}
