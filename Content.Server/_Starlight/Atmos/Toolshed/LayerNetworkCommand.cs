using System.Linq;
using Content.Server.Administration;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Administration;
using Content.Shared.Atmos.Components;
using Content.Shared.NodeContainer;
using Content.Shared.NodeContainer.NodeGroups;
using Content.Shared._Starlight.Atmos;
using Content.Shared._Starlight.Atmos.EntitySystems;
using Robust.Shared.Map.Components;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Atmos.Toolshed;

/// <summary>
/// Shifts an entire connected pipe network (the one touched by the piped-in entity) onto a single
/// <see cref="AtmosPipeLayer"/>.
/// </summary>
[ToolshedCommand, AdminCommand(AdminFlags.Mapping)]
public sealed partial class LayerNetworkCommand : ToolshedCommand
{
    private SharedMapSystem? _map;
    private AtmosPipeLayersSystem? _layers;
    private PipeRestrictOverlapSystem? _overlap;

    #region Commands

    /// <summary>
    /// List every entity in the entity's network of the specified type, restricted to the same layer only.
    /// </summary>
    /// <param name="root">The targeted entity</param>
    /// <param name="group">The network type</param>
    /// <returns>All entities part of the network, restricted to the same layer.</returns>
    [CommandImplementation("list")]
    public IEnumerable<EntityUid> List([PipedArgument] EntityUid root, NodeGroupID group)
        => CollectNetwork(root, group);

    /// <summary>
    /// Report which entities would block moving the network to the given layer.
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="root">The targeted entity</param>
    /// <param name="group">The network type</param>
    /// <param name="layer">The targeted layer</param>
    /// <returns>The entities that would fail to move (error), or would fail to partake (warning).</returns>
    [CommandImplementation("check")]
    public IEnumerable<EntityUid> Check(IInvocationContext ctx, [PipedArgument] EntityUid root, NodeGroupID group,
        AtmosPipeLayer layer)
    {
        var net = CollectNetwork(root, group);
        var conflicts = FindConflicts(net, layer);
        var unmovable = FindUnmovable(net, group, layer);

        // Conflicts cause an early return as it blocks the operation entirely. Warnings are not included in this case
        // to prevent confusing the user.
        if (conflicts.Count > 0)
        {
            ctx.WriteLine($"Error: {conflicts.Count} entities would overlap another network already on {layer}.");
            return conflicts;
        }

        if (unmovable.Count > 0)
            ctx.WriteLine(
                $"Warning: {unmovable.Count} entities can't use {layer} and would be left behind: {string.Join(", ", unmovable.Select(EntName))}.");
        if (conflicts.Count == 0 && unmovable.Count == 0)
            ctx.WriteLine($"Success: the {group} network ({net.Count} entities) can move to {layer} cleanly.");

        return conflicts.Concat(unmovable).Distinct();
    }

    /// <summary>
    /// Move the piped entity's given network to the chosen layer. Returns entities that couldn't use the layer, if any.
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="root"></param>
    /// <param name="group">The network type</param>
    /// <param name="layer">The target layer</param>
    /// <returns>The entities that would fail to move (error), or failed to partake (warning).</returns>
    [CommandImplementation("set")]
    public IEnumerable<EntityUid> Set(IInvocationContext ctx, [PipedArgument] EntityUid root, NodeGroupID group,
        AtmosPipeLayer layer)
    {
        _layers ??= GetSys<AtmosPipeLayersSystem>();
        var net = CollectNetwork(root, group);

        // Don't do anything if there are any conflicts.
        var conflicts = FindConflicts(net, layer);
        if (conflicts.Count > 0)
        {
            ctx.WriteLine(
                $"Error: {conflicts.Count} entities would overlap another network already on {layer}. No changes made.");
            return conflicts;
        }

        // Move all the entities.
        var moved = 0;
        foreach (var uid in net)
        {
            if (!TryComp<AtmosPipeLayersComponent>(uid, out var comp)
                || comp.CurrentPipeLayer == layer
                || !CanUseLayer(comp, layer))
                continue;

            _layers.SetPipeLayer((uid, comp), layer);
            moved++;
        }
        ctx.WriteLine($"Moved {moved} entities on the {group} network to {layer}.");

        // Report entities left behind.
        var unmoved = FindUnmovable(net, group, layer);
        if (unmoved.Count > 0)
        {
            ctx.WriteLine(
                $"Warning: {unmoved.Count} entities can't use {layer} and were left behind (now disconnected): {string.Join(", ", unmoved.Select(EntName))}.");
            return unmoved;
        }

        return Array.Empty<EntityUid>();
    }

    #endregion
    #region Logic

    /// <summary>
    /// Finds entities in the network that, when moved to <paramref name="layer"/>, would overlap a pipe from another
    /// network already on that layer. Returns both the network member and the obstruction it would hit; empty if none.
    /// </summary>
    private List<EntityUid> FindConflicts(IReadOnlySet<EntityUid> net, AtmosPipeLayer layer)
    {
        _overlap ??= GetSys<PipeRestrictOverlapSystem>();
        _map ??= GetSys<SharedMapSystem>();

        var conflicts = new List<EntityUid>();
        var seen = new HashSet<EntityUid>();

        foreach (var uid in net)
        {
            if (!HasComp<AtmosPipeLayersComponent>(uid) ||
                !TryComp<NodeContainerComponent>(uid, out var container))
                continue;

            var xform = Transform(uid);
            if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
                continue;

            var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);

            foreach (var node in container.Nodes.Values)
            {
                if (node is not IPipeNode pipeNode || pipeNode.Layer == layer)
                    continue;

                var proposed =
                    new PipeRestrictOverlapSystem.ProposedPipe(pipeNode.Direction, layer, xform.LocalRotation);

                if (_overlap.CheckIfWouldConflict(grid, tile, proposed, net) is not { } other)
                    continue;

                if (seen.Add(uid))
                    conflicts.Add(uid);
                seen.Add(other);
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Find entities that will not be able to move due to a limitation, and not because another entity is already
    /// present on the target layer.
    /// </summary>
    private List<EntityUid> FindUnmovable(IReadOnlySet<EntityUid> net, NodeGroupID group, AtmosPipeLayer layer)
    {
        var unmovable = new List<EntityUid>();
        foreach (var uid in net)
        {
            // Already has a node on the target layer (e.g. a manifold).
            if (HasNodeOnLayer(uid, group, layer))
                continue;

            // Will actually be moved onto the target layer.
            if (TryComp<AtmosPipeLayersComponent>(uid, out var comp) && CanUseLayer(comp, layer))
                continue;

            unmovable.Add(uid);
        }

        return unmovable;
    }

    /// <summary>
    /// Finds all entities on the target network that match the layer of the root entity.
    /// </summary>
    private HashSet<EntityUid> CollectNetwork(EntityUid root, NodeGroupID group)
    {
        var result = new HashSet<EntityUid>();

        if (!TryComp<NodeContainerComponent>(root, out var rootContainer) ||
            !GetRootLayer(root, rootContainer, group, out var layer))
            return result;

        var nodeQuery = GetEntityQuery<NodeContainerComponent>();
        var visited = new HashSet<INodeGroup>();
        var queue = new Queue<INodeGroup>();

        // Flood fill, starting from the root.
        EnqueueGroups(rootContainer, group, layer, visited, queue);

        while (queue.TryDequeue(out var pipeNet))
        {
            foreach (var node in pipeNet.Nodes)
            {
                result.Add(node.Owner);

                // Some entities don't interconnect the nodes (e.g. a pump has two nodes on the same layer).
                // We jump this gap and enqueue the other network so long as it matches the layer.
                if (nodeQuery.TryGetComponent(node.Owner, out var container))
                    EnqueueGroups(container, group, layer, visited, queue);
            }
        }

        return result;
    }

    #endregion
    #region Helpers

    private bool HasNodeOnLayer(EntityUid uid, NodeGroupID group, AtmosPipeLayer layer)
    {
        if (!TryComp<NodeContainerComponent>(uid, out var container))
            return false;

        foreach (var node in container.Nodes.Values)
        {
            if (node is PipeNode pipe && pipe.NodeGroupID == group && pipe.CurrentPipeLayer == layer)
                return true;
        }

        return false;
    }

    private bool GetRootLayer(EntityUid root, NodeContainerComponent container, NodeGroupID group,
        out AtmosPipeLayer layer)
    {
        if (TryComp<AtmosPipeLayersComponent>(root, out var layers))
        {
            layer = layers.CurrentPipeLayer;
            return true;
        }

        foreach (var node in container.Nodes.Values)
        {
            if (node is PipeNode pipe && pipe.NodeGroupID == group)
            {
                layer = pipe.CurrentPipeLayer;
                return true;
            }
        }

        layer = default;
        return false;
    }

    private static void EnqueueGroups(NodeContainerComponent container, NodeGroupID group, AtmosPipeLayer layer,
        HashSet<INodeGroup> visited, Queue<INodeGroup> queue)
    {
        foreach (var node in container.Nodes.Values)
        {
            if (node is PipeNode pipe
                && pipe.NodeGroupID == group
                && pipe.CurrentPipeLayer == layer
                && pipe.NodeGroup is { } pipeNet
                && visited.Add(pipeNet))
            {
                queue.Enqueue(pipeNet);
            }
        }
    }

    private static bool CanUseLayer(AtmosPipeLayersComponent comp, AtmosPipeLayer layer)
        => !comp.PipeLayersLocked && (int)layer < comp.NumberOfPipeLayers;

    #endregion

}
