using System.Linq;
using Content.Server.Administration;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Shuttles.Components;
using Content.Shared.Administration;
using Content.Shared.NodeContainer;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.Commands;

/// <summary>
/// Command for managing and inspecting docked pipe connections.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class DockPipeCommand : IConsoleCommand
{
    public string Command => "dockpipe";
    public string Description => "Inspect and manage docked pipe connections between airlocks/grids.";
    public string Help => @"
dockpipe info                 - List all docked pairs and their pipe connections.
dockpipe pipe <entityId>      - Show all dock connections for a pipe entity.
dockpipe tile <gridId> <x> <y>- List all pipes and docks on a tile.
dockpipe test <pipeA> <pipeB> - Check if two pipes are dock-connected.
dockpipe scan                 - List all docked airlocks and pipe connection counts.
dockpipe refresh              - Remove and re-add all docked pipe connections.
dockpipe connect <pipeA> <pipeB> - Force-connect two pipes (manual).
dockpipe cleanup              - Alias for refresh.
dockpipe check <entityId>     - Force a dock connection check for a pipe entity, show what was connected.
";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        var entityManager = IoCManager.Resolve<IEntityManager>();
        var dockPipeSystem = entityManager.System<DockPipeSystem>();

        switch (args[0].ToLower())
        {
            case "info":
                foreach (var (dockA, dockB) in dockPipeSystem.GetAllDockedPairs())
                {
                    shell.WriteLine($"Dock {dockA} <-> {dockB}:");
                    var pipesA = dockPipeSystem.GetTilePipes(dockA);
                    var pipesB = dockPipeSystem.GetTilePipes(dockB);
                    int infoCount = 0;
                    var counted = new HashSet<(EntityUid, EntityUid)>();
                    foreach (var pipeAInfo in pipesA)
                    foreach (var pipeBInfo in pipesB)
                    {
                        if (pipeAInfo.Owner == pipeBInfo.Owner)
                            continue;
                        var reachableA = pipeAInfo.GetAlwaysReachable();
                        var reachableB = pipeBInfo.GetAlwaysReachable();
                        if (dockPipeSystem.CanConnect(pipeAInfo, pipeBInfo)
                            && reachableA != null && reachableA.Contains(pipeBInfo)
                            && reachableB != null && reachableB.Contains(pipeAInfo)
                            && counted.Add((pipeAInfo.Owner, pipeBInfo.Owner)))
                        {
                            infoCount++;
                        }
                    }
                    shell.WriteLine($"    {infoCount} pipe connections");
                    // Show pipe names and node names for pipesA
                    shell.WriteLine($"    PipesA: {string.Join(", ", pipesA.Select(p => $"{GetEntityName(entityManager, p.Owner)} [{GetNodeName(entityManager, p.Owner, p)}]"))}");
                    // Show pipe names and node names for pipesB
                    shell.WriteLine($"    PipesB: {string.Join(", ", pipesB.Select(p => $"{GetEntityName(entityManager, p.Owner)} [{GetNodeName(entityManager, p.Owner, p)}]"))}");
                }
                break;

            case "pipe":
                if (args.Length < 2)
                {
                    shell.WriteLine("Usage: dockpipe pipe <entityId>");
                    return;
                }
                if (!NetEntity.TryParse(args[1], out var netEnt) || !entityManager.TryGetEntity(netEnt, out var entityId))
                {
                    shell.WriteLine("Invalid entity ID");
                    return;
                }
                if (!entityManager.TryGetComponent<NodeContainerComponent>(entityId.Value, out var nodeContainerPipe))
                {
                    shell.WriteLine("No NodeContainerComponent found.");
                    return;
                }
                foreach (var node in nodeContainerPipe.Nodes.Values)
                {
                    if (node is PipeNode pipe)
                    {
                        shell.WriteLine($"Pipe {entityId.Value}: {GetEntityName(entityManager, pipe.Owner)} [{GetNodeName(entityManager, pipe.Owner, pipe)}] Layer={pipe.CurrentPipeLayer} Dir={pipe.CurrentPipeDirection}");
                        var reachable = pipe.GetAlwaysReachable();
                        if (reachable == null || reachable.Count == 0)
                        {
                            shell.WriteLine("  No dock connections.");
                        }
                        else
                        {
                            foreach (var target in reachable)
                            {
                                shell.WriteLine($"  Dock-connected to {target.Owner} ({GetEntityName(entityManager, target.Owner)} [{GetNodeName(entityManager, target.Owner, target)}], Layer={target.CurrentPipeLayer}, Dir={target.CurrentPipeDirection})");
                            }
                        }
                    }
                }
                break;

            case "tile":
                if (args.Length < 4)
                {
                    shell.WriteLine("Usage: dockpipe tile <gridId> <x> <y>");
                    return;
                }
                if (!NetEntity.TryParse(args[1], out var gridNet) || !entityManager.TryGetEntity(gridNet, out var gridId))
                {
                    shell.WriteLine("Invalid grid ID");
                    return;
                }
                if (!int.TryParse(args[2], out var x) || !int.TryParse(args[3], out var y))
                {
                    shell.WriteLine("Invalid coordinates");
                    return;
                }
                if (!entityManager.TryGetComponent<MapGridComponent>(gridId.Value, out var gridComp))
                {
                    shell.WriteLine("Grid not found.");
                    return;
                }
                var tile = new Vector2i(x, y);
                var entities = dockPipeSystem._mapSystem.GetAnchoredEntities(gridId.Value, gridComp, tile).ToList();
                if (entities.Count == 0)
                {
                    shell.WriteLine("No anchored entities at this tile.");
                    return;
                }
                shell.WriteLine($"Entities on grid {gridId.Value} tile {tile}:");
                foreach (var ent in entities)
                {
                    shell.WriteLine($"{ent} {GetEntityName(entityManager, ent)}");
                    if (entityManager.TryGetComponent<DockingComponent>(ent, out var docking))
                        shell.WriteLine(" [Dock]");
                    if (entityManager.TryGetComponent<NodeContainerComponent>(ent, out var nodesComp))
                    {
                        var pipes = nodesComp.Nodes.Values.OfType<PipeNode>().ToList();
                        if (pipes.Count > 0)
                            shell.WriteLine($" [PipeNodes: {string.Join(", ", pipes.Select(p => $"{GetNodeName(entityManager, ent, p)} {p.CurrentPipeLayer}/{p.CurrentPipeDirection}"))}]");
                    }
                }
                break;

            case "test":
                if (args.Length < 3)
                {
                    shell.WriteLine("Usage: dockpipe test <pipeA> <pipeB>");
                    return;
                }
                if (!NetEntity.TryParse(args[1], out var pipeANet) || !entityManager.TryGetEntity(pipeANet, out var pipeA))
                {
                    shell.WriteLine("Invalid pipe A entity ID");
                    return;
                }
                if (!NetEntity.TryParse(args[2], out var pipeBNet) || !entityManager.TryGetEntity(pipeBNet, out var pipeB))
                {
                    shell.WriteLine("Invalid pipe B entity ID");
                    return;
                }
                shell.WriteLine(dockPipeSystem.TestPipeConnection(pipeA.Value, pipeB.Value));
                break;

            case "scan":
                foreach (var (dockA, dockB) in dockPipeSystem.GetAllDockedPairs())
                {
                    var pipesA = dockPipeSystem.GetTilePipes(dockA);
                    var pipesB = dockPipeSystem.GetTilePipes(dockB);
                    int scanCount = 0;
                    var counted = new HashSet<(EntityUid, EntityUid)>();
                    foreach (var pipeAScan in pipesA)
                    foreach (var pipeBScan in pipesB)
                    {
                        if (pipeAScan.Owner == pipeBScan.Owner)
                            continue;
                        var reachableA = pipeAScan.GetAlwaysReachable();
                        var reachableB = pipeBScan.GetAlwaysReachable();
                        if (dockPipeSystem.CanConnect(pipeAScan, pipeBScan)
                            && reachableA != null && reachableA.Contains(pipeBScan)
                            && reachableB != null && reachableB.Contains(pipeAScan)
                            && counted.Add((pipeAScan.Owner, pipeBScan.Owner)))
                        {
                            scanCount++;
                        }
                    }
                    shell.WriteLine($"Dock {dockA} <-> {dockB}: {scanCount} pipe connections");
                    shell.WriteLine($"    PipesA: {string.Join(", ", pipesA.Select(p => $"{GetEntityName(entityManager, p.Owner)} [{GetNodeName(entityManager, p.Owner, p)}]"))}");
                    shell.WriteLine($"    PipesB: {string.Join(", ", pipesB.Select(p => $"{GetEntityName(entityManager, p.Owner)} [{GetNodeName(entityManager, p.Owner, p)}]"))}");
                }
                break;

            case "refresh":
            case "cleanup":
                dockPipeSystem.RefreshAllDockConnections();
                shell.WriteLine("Refreshed all dock connections.");
                break;

            case "connect":
                if (args.Length < 3)
                {
                    shell.WriteLine("Usage: dockpipe connect <pipeA> <pipeB>");
                    return;
                }
                if (!NetEntity.TryParse(args[1], out var connectANet) || !entityManager.TryGetEntity(connectANet, out var connectA))
                {
                    shell.WriteLine("Invalid pipe A entity ID");
                    return;
                }
                if (!NetEntity.TryParse(args[2], out var connectBNet) || !entityManager.TryGetEntity(connectBNet, out var connectB))
                {
                    shell.WriteLine("Invalid pipe B entity ID");
                    return;
                }
                if (entityManager.TryGetComponent<NodeContainerComponent>(connectA, out var nodeContainerA) &&
                    entityManager.TryGetComponent<NodeContainerComponent>(connectB, out var nodeContainerB))
                {
                    PipeNode? pipeNodeA = nodeContainerA.Nodes.Values.OfType<PipeNode>().FirstOrDefault();
                    PipeNode? pipeNodeB = nodeContainerB.Nodes.Values.OfType<PipeNode>().FirstOrDefault();
                    if (pipeNodeA != null && pipeNodeB != null)
                    {
                        pipeNodeA.AddAlwaysReachable(pipeNodeB);
                        pipeNodeB.AddAlwaysReachable(pipeNodeA);
                        shell.WriteLine($"Manually connected {connectA} and {connectB}.");
                    }
                    else
                        shell.WriteLine("One or both entities are not pipes.");
                }
                else
                    shell.WriteLine("Entities do not have NodeContainerComponent.");
                break;

            case "check":
                if (args.Length < 2)
                {
                    shell.WriteLine("Usage: dockpipe check <entityId>");
                    return;
                }
                if (!NetEntity.TryParse(args[1], out var netEntity) ||
                    !entityManager.TryGetEntity(netEntity, out var entity))
                {
                    shell.WriteLine("Invalid entity ID.");
                    return;
                }
                if (!entityManager.TryGetComponent<NodeContainerComponent>(entity.Value, out var nodeContainerCheck))
                {
                    shell.WriteLine("Entity doesn't have NodeContainerComponent.");
                    return;
                }
                foreach (var node in nodeContainerCheck.Nodes.Values)
                {
                    if (node is PipeNode pipeNodeCheck)
                    {
                        dockPipeSystem.CheckForDockConnections(entity.Value, pipeNodeCheck);
                        shell.WriteLine($"Checked dock connections for pipe node: {node.GetType().Name} ({pipeNodeCheck.Owner}) [{GetEntityName(entityManager, pipeNodeCheck.Owner)} {GetNodeName(entityManager, pipeNodeCheck.Owner, pipeNodeCheck)}]");
                        var reachable = pipeNodeCheck.GetAlwaysReachable();
                        if (reachable != null && reachable.Count > 0)
                        {
                            shell.WriteLine("Connections added:");
                            foreach (var target in reachable)
                                shell.WriteLine($"  {target.Owner} ({GetEntityName(entityManager, target.Owner)} [{GetNodeName(entityManager, target.Owner, target)}], Layer={target.CurrentPipeLayer} Dir={target.CurrentPipeDirection})");
                        }
                        else
                        {
                            shell.WriteLine("No dock connections found.");
                        }
                        return;
                    }
                }
                shell.WriteLine("Entity doesn't contain any pipe nodes.");
                break;

            default:
                shell.WriteLine($"Unknown subcommand: {args[0]}");
                shell.WriteLine(Help);
                break;
        }
    }

    public IEnumerable<string> GetCommandOptions(IConsoleShell shell, string[] args)
    {
        if (args.Length == 0)
        {
            yield return "info";
            yield return "pipe";
            yield return "tile";
            yield return "test";
            yield return "scan";
            yield return "refresh";
            yield return "connect";
            yield return "cleanup";
            yield return "check";
            yield break;
        }

        var cmd = args[0].ToLowerInvariant();

        // Suggest subcommands
        if (args.Length == 1)
        {
            foreach (var option in new[] { "info", "pipe", "tile", "test", "scan", "refresh", "connect", "cleanup", "check" })
                if (option.StartsWith(cmd))
                    yield return option;
            yield break;
        }

        // Suggest entity IDs for commands that take them
        var entityManager = IoCManager.Resolve<IEntityManager>();
        if (cmd is "pipe" or "test" or "connect" or "check")
        {
            // Suggest entity IDs (as NetEntity strings)
            foreach (var nodeContainer in entityManager.EntityQuery<NodeContainerComponent>())
            {
                yield return nodeContainer.Owner.ToString();
            }
            yield break;
        }

        // Suggest grid IDs for tile command
        if (cmd == "tile")
        {
            if (args.Length == 2)
            {
                foreach (var grid in entityManager.EntityQuery<MapGridComponent>())
                    yield return grid.Owner.ToString();
            }
            yield break;
        }
    }

    // Helper to get the entity name
    private static string GetEntityName(IEntityManager entityManager, EntityUid entity)
    {
        if (entityManager.TryGetComponent<MetaDataComponent>(entity, out var meta))
            return meta.EntityName;
        return entity.ToString();
    }

    // Helper to get the node name
    private static string GetNodeName(IEntityManager entityManager, EntityUid entity, PipeNode pipe)
    {
        if (entityManager.TryGetComponent<NodeContainerComponent>(entity, out var nodeContainer))
        {
            foreach (var kvp in nodeContainer.Nodes)
            {
                if (ReferenceEquals(kvp.Value, pipe))
                    return kvp.Key;
            }
        }
        return "?";
    }
}