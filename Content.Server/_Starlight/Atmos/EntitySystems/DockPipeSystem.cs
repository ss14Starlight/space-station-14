using System.Linq;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Components;
using Content.Shared.NodeContainer;
using Content.Shared.Atmos;
using Robust.Shared.Map.Components;
using Robust.Shared.GameObjects;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.NodeGroups;
using Content.Shared.Atmos.Components;
using Robust.Shared.Utility;
using Robust.Shared.Log;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server.Atmos.EntitySystems
{
    /// <summary>
    /// Allows pipes to connect over docks.
    /// </summary>
    public sealed class DockPipeSystem : EntitySystem
    {
        #region Dependencies

        [Dependency] public readonly SharedMapSystem _mapSystem = default!;
        [Dependency] private IConfigurationManager _configurationManager = default!;
        private readonly HashSet<EntityUid> _dockConnectionsChecked = new();

        public bool DockPipes { get; private set; } = true;

        #endregion

        #region Initialization

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DockEvent>(OnDocked);
            SubscribeLocalEvent<UndockEvent>(OnUndocked);

            // CVar
            _configurationManager.OnValueChanged(StarlightCCVars.DockPipes, v => DockPipes = v, true);
        }

        #endregion

        #region Docking Logic

        private void OnDocked(DockEvent ev)
        {
            if (!DockPipes)
                return;

            var dockA = ev.DockA.Owner;
            var dockB = ev.DockB.Owner;


            var dockAConnecting = GetDockConnectingPipe(dockA).Where(ShouldDockPipeType).ToList();
            var dockBConnecting = GetDockConnectingPipe(dockB).Where(ShouldDockPipeType).ToList();

            var anchoredA = new HashSet<EntityUid>(dockAConnecting.Select(p => p.Owner));
            var anchoredB = new HashSet<EntityUid>(dockBConnecting.Select(p => p.Owner));

            foreach (var pipeA in dockAConnecting)
            {
                var pipeAType = pipeA.GetType().Name;
                var pipeADir = pipeA.CurrentPipeDirection;
                var pipeALayer = pipeA.CurrentPipeLayer;
                var pipeAAnchored = EntityManager.GetComponent<TransformComponent>(pipeA.Owner).Anchored;

                if (!anchoredA.Contains(pipeA.Owner) || !pipeAAnchored) continue;
                foreach (var pipeB in dockBConnecting)
                {
                    var pipeBType = pipeB.GetType().Name;
                    var pipeBDir = pipeB.CurrentPipeDirection;
                    var pipeBLayer = pipeB.CurrentPipeLayer;
                    var pipeBAnchored = EntityManager.GetComponent<TransformComponent>(pipeB.Owner).Anchored;

                    if (!anchoredB.Contains(pipeB.Owner) || !pipeBAnchored) continue;
                    var canConnect = CanConnect(pipeA, pipeB) && pipeA.CurrentPipeLayer == pipeB.CurrentPipeLayer;
                    if (canConnect)
                    {
                        pipeA.AddAlwaysReachable(pipeB);
                        pipeB.AddAlwaysReachable(pipeA);
                    }
                }
            }

            foreach (var dock in new[] { dockA, dockB })
            {
                foreach (var pipe in GetDockConnectingPipe(dock).Where(ShouldDockPipeType))
                {
                    var pipeType = pipe.GetType().Name;
                    var pipeDir = pipe.CurrentPipeDirection;
                    var pipeLayer = pipe.CurrentPipeLayer;
                    var pipeAnchored = EntityManager.GetComponent<TransformComponent>(pipe.Owner).Anchored;
                    if (!pipeAnchored) continue;
                    CheckForDockConnections(pipe.Owner, pipe);
                }
            }
        }

        private List<PipeNode> GetDockConnectingPipe(EntityUid dock)
        {
            if (!DockPipes)
                return new();
            if (!TryComp<TransformComponent>(dock, out var xform) || xform.GridUid == null)
                return new();
            if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                return new();

            var dockTile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
            var dockDir = xform.LocalRotation.GetCardinalDir();
            var edgeTile = dockTile.Offset(dockDir);

            var edgeNodes = new List<PipeNode>();
            float closestEdgeDist = float.MaxValue;
            PipeNode? closestEdgeNode = null;
            var facingNodes = new List<(PipeNode node, float dist)>();

            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, dockTile))
            {
                if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
                    continue;
                if (!TryComp<TransformComponent>(ent, out var entXform) || !entXform.Anchored)
                    continue;

                foreach (var node in nodeContainer.Nodes.Values.OfType<PipeNode>())
                {
                    if (node.Deleting)
                        continue;
                    if (!ShouldDockPipeType(node))
                        continue;
                    var hasDir = node.CurrentPipeDirection.HasDirection(dockDir.ToPipeDirection());
                    if (!hasDir)
                        continue;


                    if (!TryComp<TransformComponent>(node.Owner, out var pipeXform))
                        continue;
                    var pipeTile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, pipeXform.Coordinates);

                    var dockPos = xform.Coordinates.Position;
                    var nodePos = pipeXform.Coordinates.Position;
                    var dist = (dockPos - nodePos).Length();

                    if (pipeTile == edgeTile)
                    {
                        edgeNodes.Add(node);
                        if (dist < closestEdgeDist)
                        {
                            closestEdgeDist = dist;
                            closestEdgeNode = node;
                        }
                    }

                    facingNodes.Add((node, dist));
                }
            }

            if (edgeNodes.Count > 0)
                return edgeNodes;

            if (facingNodes.Count > 0)
            {
                var minDist = facingNodes.Min(x => x.dist);
                var closestNodes = facingNodes.Where(x => Math.Abs(x.dist - minDist) < 0.01f).Select(x => x.node).ToList();
                return closestNodes;
            }

            return new();
        }

        private void OnUndocked(UndockEvent ev)
        {
            if (!DockPipes)
                return;

            var dockA = ev.DockA.Owner;
            var dockB = ev.DockB.Owner;

            var pipesA = GetTilePipes(dockA);
            var pipesB = GetTilePipes(dockB);

            var anchoredA = new HashSet<EntityUid>(pipesA.Select(p => p.Owner));
            var anchoredB = new HashSet<EntityUid>(pipesB.Select(p => p.Owner));

            foreach (var pipeA in pipesA)
            {
                if (!anchoredA.Contains(pipeA.Owner) || !EntityManager.GetComponent<TransformComponent>(pipeA.Owner).Anchored) continue;
                foreach (var pipeB in pipesB)
                {
                    if (!anchoredB.Contains(pipeB.Owner) || !EntityManager.GetComponent<TransformComponent>(pipeB.Owner).Anchored) continue;
                    if (pipeA.CurrentPipeLayer == pipeB.CurrentPipeLayer)
                    {
                        pipeA.RemoveAlwaysReachable(pipeB);
                        pipeB.RemoveAlwaysReachable(pipeA);
                    }
                }
            }
        }

        #endregion

        #region Pipe Query

        public bool ShouldDockPipeType(PipeNode node)
        {
            return DockPipes;
        }

        private List<(PipeNode pipe, int visualLayer)> GetTilePipesWithRotation(EntityUid dock, out int gridRotation)
        {
            gridRotation = 0;
            if (!TryComp<TransformComponent>(dock, out var xform) || xform.GridUid == null)
                return new();

            if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                return new();

            gridRotation = GetGridRotation(xform.GridUid.Value);

            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
            var result = new List<(PipeNode, int)>();
            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile))
            {
                if (ent == dock)
                    continue;
                if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
                    continue;
                foreach (var node in nodeContainer.Nodes.Values)
                {
                    if (node is PipeNode pipe)
                        result.Add((pipe, CalculateVisualLayer(pipe.CurrentPipeLayer, gridRotation)));
                }
            }
            return result;
        }

        // Grid rotation
        private int GetGridRotation(EntityUid gridUid)
        {
            var gridXform = EntityManager.GetComponent<TransformComponent>(gridUid);
            var angle = gridXform.LocalRotation.Theta;
            return ((int)Math.Round(angle / (Math.PI / 2)) % 4 + 4) % 4;
        }

        private int CalculateVisualLayer(AtmosPipeLayer pipeLayer, int gridRotation)
        {
            return ((int)pipeLayer + gridRotation) % 3;
        }

        public List<PipeNode> GetTilePipes(EntityUid dock)
        {
            if (!DockPipes)
                return new();
            if (!TryComp<TransformComponent>(dock, out var xform) || xform.GridUid == null)
                return new();

            if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                return new();

            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
            var result = new List<PipeNode>();
            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile))
            {
                if (ent == dock)
                    continue;
                if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
                    continue;
                if (!TryComp<TransformComponent>(ent, out var entXform) || !entXform.Anchored)
                    continue;
                foreach (var pipe in nodeContainer.Nodes.Values.OfType<PipeNode>().Where(pipe => !pipe.Deleting))
                {
                    if (!ShouldDockPipeType(pipe))
                        continue;
                    result.Add(pipe);
                }
            }
            return result;
        }

        public bool CanConnect(PipeNode a, PipeNode b)
        {
            if (!DockPipes)
                return false;
            var result = a.NodeGroupID == b.NodeGroupID && !a.Deleting && !b.Deleting;
            return result;
        }

        #endregion

        #region Anchor Handling

        /// <summary>
        /// Anchoring Pipes
        /// </summary>
        public void TryConnectDockedPipe(EntityUid pipeEntity)
        {
            if (!DockPipes)
                return;
            if (!EntityManager.TryGetComponent<NodeContainerComponent>(pipeEntity, out var nodeContainer))
                return;
            if (!EntityManager.TryGetComponent<TransformComponent>(pipeEntity, out var xform) || xform.GridUid == null || !xform.Anchored)
                return;
            if (!EntityManager.TryGetComponent<MapGridComponent>(xform.GridUid.Value, out var grid))
                return;
            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);

            // Find the dock direction and edge
            var dockDir = xform.LocalRotation.GetCardinalDir();
            var edgeTile = tile.Offset(dockDir);

            List<PipeNode> nodesToConnect = new();

            foreach (var node in nodeContainer.Nodes.Values.OfType<PipeNode>())
            {
                if (node.Deleting)
                    continue;
                if (!ShouldDockPipeType(node))
                    continue;
                var nodeDir = node.CurrentPipeDirection;
                if (!nodeDir.HasDirection(dockDir.ToPipeDirection()))
                    continue;
                if (!EntityManager.TryGetComponent<TransformComponent>(node.Owner, out var nodeXform))
                    continue;
                var nodeTile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, nodeXform.Coordinates);
                var dockPos = xform.Coordinates.Position;
                var nodePos = nodeXform.Coordinates.Position;
                var dist = (dockPos - nodePos).Length();

                if (nodeTile == edgeTile)
                {
                    if (nodesToConnect.Count == 0 || dist < (nodesToConnect.Count > 0 ? (dockPos - EntityManager.GetComponent<TransformComponent>(nodesToConnect[0].Owner).Coordinates.Position).Length() : float.MaxValue))
                    {
                        nodesToConnect.Clear();
                        nodesToConnect.Add(node);
                    }
                }
                else if (nodeTile == tile)
                {
                    if (nodesToConnect.Count == 0)
                        nodesToConnect.Add(node);
                }
            }

            if (nodesToConnect.Count == 0)
                return;

            foreach (var node in nodesToConnect)
            {
                var reachable = node.GetAlwaysReachable();
                if (reachable != null)
                {
                    foreach (var target in reachable.ToList())
                        node.RemoveAlwaysReachable(target);
                }
            }

            var anchoredEntities = _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile).ToList();
            var dockedEntities = anchoredEntities.Where(ent =>
                ent != pipeEntity &&
                EntityManager.TryGetComponent<DockingComponent>(ent, out var docking) &&
                docking.DockedWith is not null).ToList();

            foreach (var ent in dockedEntities)
            {
                var docking = EntityManager.GetComponent<DockingComponent>(ent);
                var otherDock = docking.DockedWith!.Value;
                var pipesOther = GetDockConnectingPipe(otherDock)
                    .Where(p => EntityManager.GetComponent<TransformComponent>(p.Owner).Anchored && ShouldDockPipeType(p))
                    .ToList();
                foreach (var node in nodesToConnect)
                foreach (var pipeB in pipesOther)
                {
                    if (CanConnect(node, pipeB) && node.CurrentPipeLayer == pipeB.CurrentPipeLayer)
                    {
                        var reachableA = node.GetAlwaysReachable();
                        var reachableB = pipeB.GetAlwaysReachable();
                        if (reachableA == null || !reachableA.Contains(pipeB))
                            node.AddAlwaysReachable(pipeB);
                        if (reachableB == null || !reachableB.Contains(node))
                            pipeB.AddAlwaysReachable(node);
                    }
                }
            }
        }

        #endregion

        #region Dock Checking

        public void CheckForDockConnections(EntityUid pipeEntity, PipeNode pipeNode)
        {
            if (!DockPipes)
                return;
            if (!_dockConnectionsChecked.Add(pipeEntity))
                return;

            if (!EntityManager.TryGetComponent<TransformComponent>(pipeEntity, out var xform) || xform.GridUid == null || !xform.Anchored)
                return;
            if (!EntityManager.TryGetComponent<MapGridComponent>(xform.GridUid.Value, out var grid))
                return;
            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);

            var anchoredEntities = _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile).ToList();
            var dockedEntities = anchoredEntities.Where(ent =>
                ent != pipeEntity &&
                EntityManager.TryGetComponent<DockingComponent>(ent, out var docking) &&
                docking.DockedWith is not null).ToList();

            foreach (var ent in dockedEntities)
            {
                var docking = EntityManager.GetComponent<DockingComponent>(ent);
                var otherDock = docking.DockedWith!.Value;
                var pipesOther = GetDockConnectingPipe(otherDock).Where(p => EntityManager.GetComponent<TransformComponent>(p.Owner).Anchored);
                foreach (var pipeB in pipesOther)
                {
                    var pipeBType = pipeB.GetType().Name;
                    var pipeBDir = pipeB.CurrentPipeDirection;
                    var pipeBLayer = pipeB.CurrentPipeLayer;
                    if (CanConnect(pipeNode, pipeB) && pipeNode.CurrentPipeLayer == pipeB.CurrentPipeLayer)
                    {
                        var reachableA = pipeNode.GetAlwaysReachable();
                        var reachableB = pipeB.GetAlwaysReachable();
                        if (reachableA == null || !reachableA.Contains(pipeB))
                        {
                            pipeNode.AddAlwaysReachable(pipeB);
                        }
                        if (reachableB == null || !reachableB.Contains(pipeNode))
                        {
                            pipeB.AddAlwaysReachable(pipeNode);
                        }
                    }
                }
            }
        }

        #endregion

        #region Debug Commands

        public string GetDebugInfo()
        {
            if (!DockPipes)
                return "DockPipeSystem is disabled by CVar.";
            var lines = new List<string> { "Docked pipe connections:" };
            foreach (var (dockA, dockB) in GetAllDockedPairs())
            {
                var pipesA = GetTilePipes(dockA);
                var pipesB = GetTilePipes(dockB);
                int count = 0;
                foreach (var pipeA in pipesA)
                foreach (var pipeB in pipesB)
                {
                    var reachable = pipeA.GetAlwaysReachable();
                    if (CanConnect(pipeA, pipeB) && reachable != null && reachable.Contains(pipeB))
                        count++;
                }
                lines.Add($"Dock {dockA} <-> {dockB}: {count} connections");
            }
            return string.Join('\n', lines);
        }

        public string GetPipeDebugInfo(EntityUid entity)
        {
            if (!DockPipes)
                return "DockPipeSystem is disabled by CVar.";
            if (!EntityManager.TryGetComponent<NodeContainerComponent>(entity, out var nodeContainer))
                return "No NodeContainerComponent.";
            foreach (var node in nodeContainer.Nodes.Values)
            {
                if (node is PipeNode pipe)
                {
                    var reachable = pipe.GetAlwaysReachable();
                    if (reachable == null || reachable.Count == 0)
                        return $"Pipe {entity} has no dock connections.";
                    var targets = string.Join(", ", reachable.Select(x => x.Owner.ToString()));
                    return $"Pipe {entity} dock connections: {targets}";
                }
            }
            return "No PipeNode found.";
        }

        public string GetTileDebugInfo(EntityUid gridId, Vector2i tile)
        {
            if (!DockPipes)
                return "DockPipeSystem is disabled by CVar.";
            if (!EntityManager.TryGetComponent<MapGridComponent>(gridId, out var grid))
                return $"Grid {gridId} not found.";
            var ents = _mapSystem.GetAnchoredEntities(gridId, grid, tile).ToList();
            if (ents.Count == 0)
                return $"No anchored entities at {gridId} {tile}.";
            var lines = new List<string> { $"Entities at {gridId} {tile}:" };
            foreach (var ent in ents)
                lines.Add($"  {ent}");
            return string.Join('\n', lines);
        }

        public string TestPipeConnection(EntityUid pipeAUid, EntityUid pipeBUid)
        {
            if (!DockPipes)
                return "DockPipeSystem is disabled by CVar.";
            if (!EntityManager.TryGetComponent<NodeContainerComponent>(pipeAUid, out var nodeA) ||
                !EntityManager.TryGetComponent<NodeContainerComponent>(pipeBUid, out var nodeB))
                return "One or both entities are not pipes.";
            PipeNode? pipeA = nodeA.Nodes.Values.OfType<PipeNode>().FirstOrDefault();
            PipeNode? pipeB = nodeB.Nodes.Values.OfType<PipeNode>().FirstOrDefault();
            if (pipeA == null || pipeB == null)
                return "One or both entities are not pipes.";
            var reachable = pipeA.GetAlwaysReachable();
            if (reachable != null && reachable.Contains(pipeB))
                return $"Pipes {pipeAUid} and {pipeBUid} are dock-connected.";
            return $"Pipes {pipeAUid} and {pipeBUid} are not dock-connected.";
        }

        #endregion

        #region Connection Refresh

        public void RefreshAllDockConnections()
        {
            if (!DockPipes)
                return;
            ClearDockConnectionsChecked();
            foreach (var (dockA, dockB) in GetAllDockedPairs())
            {
                var pipesA = GetTilePipes(dockA);
                var pipesB = GetTilePipes(dockB);

                var anchoredA = new HashSet<EntityUid>(pipesA.Select(p => p.Owner));
                var anchoredB = new HashSet<EntityUid>(pipesB.Select(p => p.Owner));

                foreach (var pipeA in pipesA)
                foreach (var pipeB in pipesB)
                {
                    if (!anchoredA.Contains(pipeA.Owner) || !anchoredB.Contains(pipeB.Owner) ||
                        !EntityManager.GetComponent<TransformComponent>(pipeA.Owner).Anchored ||
                        !EntityManager.GetComponent<TransformComponent>(pipeB.Owner).Anchored) continue;
                    var reachableA = pipeA.GetAlwaysReachable();
                    var reachableB = pipeB.GetAlwaysReachable();
                    if (reachableA != null && reachableA.Contains(pipeB))
                        pipeA.RemoveAlwaysReachable(pipeB);
                    if (reachableB != null && reachableB.Contains(pipeA))
                        pipeB.RemoveAlwaysReachable(pipeA);
                }
                foreach (var pipeA in pipesA)
                foreach (var pipeB in pipesB)
                    if (anchoredA.Contains(pipeA.Owner) && anchoredB.Contains(pipeB.Owner) &&
                        EntityManager.GetComponent<TransformComponent>(pipeA.Owner).Anchored &&
                        EntityManager.GetComponent<TransformComponent>(pipeB.Owner).Anchored &&
                        CanConnect(pipeA, pipeB))
                    {
                        pipeA.AddAlwaysReachable(pipeB);
                        pipeB.AddAlwaysReachable(pipeA);
                    }
            }
        }

        private void ClearDockConnectionsChecked()
        {
            _dockConnectionsChecked.Clear();
        }

        #endregion

        #region Helper Methods

        public IEnumerable<(EntityUid, EntityUid)> GetAllDockedPairs()
        {
            foreach (var dockEntity in EntityManager.EntityQuery<DockingComponent>())
            {
                var dockA = dockEntity.Owner;
                if (dockEntity.DockedWith is not { } dockB)
                    continue;
                if (dockA.CompareTo(dockB) < 0)
                    yield return (dockA, dockB);
            }
        }

        #endregion
    }
}