using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.NodeContainer;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
// Starlight Start: DockCableSystem
using System.Collections.Generic;
using Robust.Shared.Utility;
// Starlight End

namespace Content.Server.Power.Nodes
{
    [DataDefinition]
    public sealed partial class CableNode : Node
    {
        // Starlight Start: DockCableSystem
        private HashSet<CableNode>? _alwaysReachable;

        public void AddAlwaysReachable(CableNode node)
        {
            if (node == this) return;
            _alwaysReachable ??= new();
            _alwaysReachable.Add(node);
        }

        public void RemoveAlwaysReachable(CableNode node)
        {
            if (_alwaysReachable == null) return;
            _alwaysReachable.Remove(node);
        }

        public HashSet<CableNode>? GetAlwaysReachable() => _alwaysReachable;
        // Starlight End
        public override IEnumerable<Node> GetReachableNodes(TransformComponent xform,
            EntityQuery<NodeContainerComponent> nodeQuery,
            EntityQuery<TransformComponent> xformQuery,
            MapGridComponent? grid,
            IEntityManager entMan)
        {
            // Starlight Start: DockCableSystem
            if (_alwaysReachable != null)
            {
                var remQ = new RemQueue<CableNode>();
                foreach (var node in _alwaysReachable)
                {
                    if (node.Deleting)
                    {
                        remQ.Add(node);
                    }
                    else
                    {
                        yield return node;
                    }
                }
                foreach (var node in remQ)
                {
                    _alwaysReachable.Remove(node);
                }
            }
            // Starlight End
            if (!xform.Anchored || grid == null)
                yield break;

            var gridIndex = grid.TileIndicesFor(xform.Coordinates);

            // While we go over adjacent nodes, we build a list of blocked directions due to
            // incoming or outgoing wire terminals.
            var terminalDirs = 0;
            List<(Direction, Node)> nodeDirs = new();

            foreach (var (dir, node) in NodeHelpers.GetCardinalNeighborNodes(nodeQuery, grid, gridIndex))
            {
                if (node is CableNode && node != this)
                {
                    nodeDirs.Add((dir, node));
                }

                if (node is CableDeviceNode && dir == Direction.Invalid)
                {
                    // device on same tile
                    nodeDirs.Add((Direction.Invalid, node));
                }

                if (node is CableTerminalNode)
                {
                    if (dir == Direction.Invalid)
                    {
                        // On own tile, block direction it faces
                        terminalDirs |= 1 << (int) xformQuery.GetComponent(node.Owner).LocalRotation.GetCardinalDir();
                    }
                    else
                    {
                        var terminalDir = xformQuery.GetComponent(node.Owner).LocalRotation.GetCardinalDir();
                        if (terminalDir.GetOpposite() == dir)
                        {
                            // Target tile has a terminal towards us, block the direction.
                            terminalDirs |= 1 << (int) dir;
                        }
                    }
                }
            }

            foreach (var (dir, node) in nodeDirs)
            {
                // If there is a wire terminal connecting across this direction, skip the node.
                if (dir != Direction.Invalid && (terminalDirs & (1 << (int) dir)) != 0)
                    continue;

                yield return node;
            }
        }
        // Starlight Start: DockCableSystem
        public override void OnAnchorStateChanged(IEntityManager entityManager, bool anchored)
        {
            base.OnAnchorStateChanged(entityManager, anchored);

            var dockCableSystem = entityManager.System<Content.Server.Power.EntitySystems.DockCableSystem>();
            if (anchored)
            {
                dockCableSystem.TryConnectDockedCable(this);
            }
            else
            {
                dockCableSystem.RemoveDockConnections(this);
            }
        }
        // Starlight End
    }
}
