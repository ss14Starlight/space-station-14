using Content.Server.Administration;
using Content.Server.Power.EntitySystems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;

namespace Content.Server.Power.Commands;

/// <summary>
/// Debug/admin command for inspecting and managing docked cable connections.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class DockCableCommand : IConsoleCommand
{
    public string Command => "dockcable";
    public string Description => "Inspect and manage docked cable connections between docks/grids.";
    public string Help => @"
dockcable info
    List all docked pairs and their cable connections.
dockcable cable <entityId>
    Show all dock connections for a cable entity.
dockcable tile <gridId> <x> <y>
    List all cables and docks on a tile.
dockcable test <cableA> <cableB>
    Check if two cables are dock-connected.
dockcable refresh
    Remove and re-add all docked cable connections.
";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var dockCableSystem = entMan.System<DockCableSystem>();

        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "info":
                shell.WriteLine(dockCableSystem.GetDebugInfo());
                break;

            case "cable":
                if (args.Length < 2)
                {
                    shell.WriteLine("Usage: dockcable cable <entityId>");
                    return;
                }
                if (!NetEntity.TryParse(args[1], out var netEnt) || !entMan.TryGetEntity(netEnt, out var entityId))
                {
                    shell.WriteLine("Invalid entity ID");
                    return;
                }
                shell.WriteLine(dockCableSystem.GetCableDebugInfo(entityId.Value));
                break;

            case "tile":
                if (args.Length < 4)
                {
                    shell.WriteLine("Usage: dockcable tile <gridId> <x> <y>");
                    return;
                }
                if (!NetEntity.TryParse(args[1], out var gridNet) || !entMan.TryGetEntity(gridNet, out var gridId))
                {
                    shell.WriteLine("Invalid grid ID");
                    return;
                }
                if (!int.TryParse(args[2], out var x) || !int.TryParse(args[3], out var y))
                {
                    shell.WriteLine("Invalid coordinates");
                    return;
                }
                shell.WriteLine(dockCableSystem.GetTileDebugInfo(gridId.Value, new Vector2i(x, y)));
                break;

            case "test":
                if (args.Length < 3)
                {
                    shell.WriteLine("Usage: dockcable test <cableA> <cableB>");
                    return;
                }
                if (!NetEntity.TryParse(args[1], out var cableANet) || !entMan.TryGetEntity(cableANet, out var cableA))
                {
                    shell.WriteLine("Invalid cable A entity ID");
                    return;
                }
                if (!NetEntity.TryParse(args[2], out var cableBNet) || !entMan.TryGetEntity(cableBNet, out var cableB))
                {
                    shell.WriteLine("Invalid cable B entity ID");
                    return;
                }
                shell.WriteLine(dockCableSystem.TestCableConnection(cableA.Value, cableB.Value));
                break;

            case "refresh":
                dockCableSystem.RefreshAllDockConnections();
                shell.WriteLine("Refreshed all docked cable connections.");
                break;

            default:
                shell.WriteLine($"Unknown subcommand: {args[0]}");
                shell.WriteLine(Help);
                break;
        }
    }
}