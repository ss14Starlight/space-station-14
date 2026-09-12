using Content.Server.Administration;
using Content.Shared._Starlight.Zones;
using Content.Shared.Administration;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;

namespace Content.Server._Starlight.Zones.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class ZoneAtCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entMan = default!;

    public override string Command => "zoneat";

    /// <summary>
    /// Executes the zoneat command, which retrieves the zone and room information for a specific tile on a grid.
    /// </summary>
    public override void Execute(IConsoleShell shell, string _, string[] args)
    {
        var zones = _entMan.System<ZoneSystem>();

        EntityUid grid;
        Vector2i tile;

        switch (args.Length)
        {
            case 0:
                if (shell.Player?.AttachedEntity is not { } player)
                {
                    shell.WriteError(Loc.GetString("cmd-zoneat-need-entity-or-grid"));
                    return;
                }

                var xform = _entMan.System<TransformSystem>();
                if (xform.GetGrid(player) is not { } playerGrid ||
                    !_entMan.TryGetComponent(playerGrid, out MapGridComponent? playerGridComp))
                {
                    shell.WriteError(Loc.GetString("cmd-zoneat-not-on-grid"));
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
                    shell.WriteError(Loc.GetString("cmd-zoneat-cant-parse-grid", ("gridUid", args[0])));
                    return;
                }

                if (!int.TryParse(args[1], out var x) || !int.TryParse(args[2], out var y))
                {
                    shell.WriteError(Loc.GetString("cmd-zoneat-cant-parse-tile", ("x", args[1]), ("y", args[2])));
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
            ? Loc.GetString("cmd-zoneat-not-in-room", ("tile", tile), ("grid", _entMan.ToPrettyString(grid)), ("zone", zone))
            : Loc.GetString("cmd-zoneat-in-room", ("tile", tile), ("grid", _entMan.ToPrettyString(grid)), ("zone", zone), ("room", room)));
    }
}
