using Content.Shared._Starlight.Zones;
using Robust.Client.Player;
using Robust.Shared.Console;

namespace Content.Client._Starlight.Zones.Commands;

public sealed partial class MyZoneCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override string Command => "myzone";

    /// <summary>
    /// Executes the myzone command, which retrieves the zone information for the player's current location.
    /// </summary>
    public override void Execute(IConsoleShell shell, string _, string[] __)
    {
        if (_player.LocalEntity is not { } player)
        {
            shell.WriteError(Loc.GetString("cmd-myzone-no-entity"));
            return;
        }

        if (!_entMan.TryGetComponent(player, out ZoneTrackerComponent? tracker))
        {
            shell.WriteError(Loc.GetString("cmd-myzone-no-tracker"));
            return;
        }

        shell.WriteLine(tracker.Zones.Count > 0
            ? Loc.GetString("cmd-myzone-in-zone", ("zone", string.Join(", ", tracker.Zones)))
            : Loc.GetString("cmd-myzone-not-in-zone"));
    }
}
