using Content.Server.Administration;
using Content.Shared._Starlight.Zones;
using Content.Shared.Administration;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;

namespace Content.Server._Starlight.Zones.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class ZoneAtCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entMan = default!;

    public string Command => "zoneat";
    public string Description => "Prints the zone at your feet, or at the given grid tile.";
    public string Help => $"Usage: {Command} | {Command} <gridUid> <x> <y>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var zones = _entMan.System<ZoneSystem>();

        EntityUid grid;
        Vector2i tile;

        switch (args.Length)
        {
            case 0:
                if (shell.Player?.AttachedEntity is not { } player)
                {
                    shell.WriteError("You need to be attached to an entity, or pass a grid and tile.");
                    return;
                }

                var xform = _entMan.System<TransformSystem>();
                if (xform.GetGrid(player) is not { } playerGrid ||
                    !_entMan.TryGetComponent(playerGrid, out MapGridComponent? playerGridComp))
                {
                    shell.WriteError("You are not on a grid.");
                    return;
                }

                grid = playerGrid;
                tile = _entMan.System<MapSystem>()
                    .TileIndicesFor(playerGrid, playerGridComp, _entMan.GetComponent<TransformComponent>(player).Coordinates);
                break;

            case 3:
                if (!NetEntity.TryParse(args[0], out var netGrid) ||
                    !_entMan.TryGetEntity(netGrid, out var parsedGrid))
                {
                    shell.WriteError($"Could not parse grid '{args[0]}'.");
                    return;
                }

                if (!int.TryParse(args[1], out var x) || !int.TryParse(args[2], out var y))
                {
                    shell.WriteError("Could not parse tile coordinates.");
                    return;
                }

                grid = parsedGrid.Value;
                tile = new Vector2i(x, y);
                break;

            default:
                shell.WriteError(Help);
                return;
        }

        var id = zones.GetZoneId(grid, tile);
        var room = zones.GetRegion(grid, tile);

        var zone = id == SharedZoneSystem.NoZone ? "no zone" : zones.GetZone(id)?.ID ?? "?";

        shell.WriteLine(room == SharedZoneSystem.NoRegion
            ? $"{tile} on {_entMan.ToPrettyString(grid)}: {zone}, not part of any room."
            : $"{tile} on {_entMan.ToPrettyString(grid)}: {zone}, room {room}.");
    }
}
