using System.Linq;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Components;
using Content.Shared.NodeContainer;
using Content.Shared.Atmos;
using Robust.Shared.Map.Components;
using Robust.Shared.GameObjects;
using Content.Server.NodeContainer.EntitySystems;
using Content.Shared.Atmos.Components;
using Robust.Shared.Utility;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._Starlight.Atmos.EntitySystems
{
    /// <summary>
    /// Allows pipes to connect over docks.
    /// </summary>
    public sealed class PipeDockingSystem : EntitySystem
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

            foreach (var pipeA in dockAConnecting)
            {
                if (!IsAnchored(pipeA.Owner))
                    continue;

                foreach (var pipeB in dockBConnecting)
                {
                    if (!IsAnchored(pipeB.Owner) || !CanConnect(pipeA, pipeB))
                        continue;

                    LinkPipes(pipeA, pipeB);
                }
            }

            foreach (var dock in new[] { dockA, dockB })
            {
                foreach (var pipe in GetDockConnectingPipe(dock).Where(ShouldDockPipeType))
                {
                    if (!IsAnchored(pipe.Owner))
                        continue;

                    CheckForDockConnections(pipe.Owner, pipe);
                }
            }
        }

        private List<PipeNode> GetDockConnectingPipe(EntityUid dock)
        {
            var xform = Transform(dock);
            if (xform.GridUid == null)
                return new();
            if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                return new();

            var dockTile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
            var dockDir = xform.LocalRotation.GetCardinalDir();
            var dockNodes = new List<PipeNode>();

            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, dockTile))
            {
                if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
                    continue;
                var entXform = Transform(ent);
                if (!entXform.Anchored)
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
                    dockNodes.Add(node);
                }
            }

            return dockNodes;
        }

        private void OnUndocked(UndockEvent ev)
        {
            var dockA = ev.DockA.Owner;
            var dockB = ev.DockB.Owner;

            var pipesA = GetDockConnectingPipe(dockA);
            var pipesB = GetDockConnectingPipe(dockB);

            foreach (var pipeA in pipesA)
            {
                if (!IsAnchored(pipeA.Owner))
                    continue;

                foreach (var pipeB in pipesB)
                {
                    if (!IsAnchored(pipeB.Owner) || !CanConnect(pipeA, pipeB))
                        continue;

                    UnlinkPipes(pipeA, pipeB);
                }
            }
        }

        #endregion

        #region Pipe Query

        public bool ShouldDockPipeType(PipeNode node)
        {
            return DockPipes;
        }

        public List<PipeNode> GetTilePipes(EntityUid dock)
        {
            if (!DockPipes)
                return new();
            var xform = Transform(dock);
            if (xform.GridUid == null)
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
                var entXform = Transform(ent);
                if (!entXform.Anchored)
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
            var result = a.NodeGroupID == b.NodeGroupID
                && a.CurrentPipeLayer == b.CurrentPipeLayer
                && !a.Deleting
                && !b.Deleting;
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
            var xform = Transform(pipeEntity);
            if (xform.GridUid == null || !xform.Anchored)
                return;
            if (!EntityManager.TryGetComponent<MapGridComponent>(xform.GridUid.Value, out var grid))
                return;
            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);

            var anchoredEntities = _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile).ToList();
            var dockedEntities = anchoredEntities.Where(ent =>
                ent != pipeEntity &&
                EntityManager.TryGetComponent<DockingComponent>(ent, out var docking) &&
                docking.DockedWith is not null).ToList();

            if (dockedEntities.Count == 0)
                return;

            RemoveDockConnections(pipeEntity);

            foreach (var ent in dockedEntities)
            {
                var docking = EntityManager.GetComponent<DockingComponent>(ent);
                var otherDock = docking.DockedWith!.Value;

                var localPipes = GetDockConnectingPipe(ent)
                    .Where(p => p.Owner == pipeEntity && IsAnchored(p.Owner) && ShouldDockPipeType(p))
                    .ToList();

                if (localPipes.Count == 0)
                    continue;

                var pipesOther = GetDockConnectingPipe(otherDock)
                    .Where(p => IsAnchored(p.Owner) && ShouldDockPipeType(p))
                    .ToList();

                foreach (var node in localPipes)
                foreach (var pipeB in pipesOther)
                {
                    if (CanConnect(node, pipeB))
                        LinkPipes(node, pipeB);
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

            var xform = Transform(pipeEntity);
            if (xform.GridUid == null || !xform.Anchored)
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
                var pipesOther = GetDockConnectingPipe(otherDock).Where(p => IsAnchored(p.Owner));
                foreach (var pipeB in pipesOther)
                {
                    if (CanConnect(pipeNode, pipeB))
                        LinkPipes(pipeNode, pipeB);
                }
            }
        }

        #endregion

        public void RemoveDockConnections(EntityUid pipeEntity)
        {
            if (!EntityManager.TryGetComponent<NodeContainerComponent>(pipeEntity, out var nodeContainer))
                return;

            foreach (var node in nodeContainer.Nodes.Values.OfType<PipeNode>())
            {
                var reachable = node.GetAlwaysReachable();
                if (reachable == null)
                    continue;

                foreach (var target in reachable.ToList())
                {
                    if (target is not PipeNode pipeNode)
                        continue;

                    node.RemoveAlwaysReachable(pipeNode);
                    pipeNode.RemoveAlwaysReachable(node);
                }
            }
        }

        private bool IsAnchored(EntityUid uid)
        {
            return Transform(uid).Anchored;
        }

        private void LinkPipes(PipeNode a, PipeNode b)
        {
            var reachableA = a.GetAlwaysReachable();
            var reachableB = b.GetAlwaysReachable();
            if (reachableA != null && reachableA.Contains(b) && reachableB != null && reachableB.Contains(a))
                return;

            a.AddAlwaysReachable(b);
            b.AddAlwaysReachable(a);
        }

        private void UnlinkPipes(PipeNode a, PipeNode b)
        {
            a.RemoveAlwaysReachable(b);
            b.RemoveAlwaysReachable(a);
        }
    }
}
