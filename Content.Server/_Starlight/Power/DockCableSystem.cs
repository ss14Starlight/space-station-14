using System.Collections.Generic;
using System.Linq;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Server.Power.Nodes;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.NodeContainer;
using Content.Shared.Power;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;
using Robust.Shared.Log;
using Content.Server.NodeContainer.EntitySystems;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server.Power.EntitySystems
{
    /// <summary>
    /// Allows cables to connect over docks.
    /// </summary>
    public sealed class DockCableSystem : EntitySystem
    {
        #region Dependencies

        [Dependency] public readonly SharedMapSystem _mapSystem = default!;
        [Dependency] private IConfigurationManager _configurationManager = default!;
        private readonly HashSet<EntityUid> _dockConnectionsChecked = new();

        #endregion

        #region CVar

        public bool DockHV = true;
        public bool DockMV = false;
        public bool DockLV = false;

        #endregion

        #region Initialization

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DockEvent>(OnDocked);
            SubscribeLocalEvent<UndockEvent>(OnUndocked);

            _configurationManager.OnValueChanged(StarlightCCVars.DockHV, v => DockHV = v, true);
            _configurationManager.OnValueChanged(StarlightCCVars.DockMV, v => DockMV = v, true);
            _configurationManager.OnValueChanged(StarlightCCVars.DockLV, v => DockLV = v, true);
        }

        #endregion

        #region Docking Logic

        private void OnDocked(DockEvent ev)
        {
            var dockA = ev.DockA.Owner;
            var dockB = ev.DockB.Owner;

            var cablesA = GetDockCableNodes(dockA).ToList();
            var cablesB = GetDockCableNodes(dockB).ToList();

            foreach (var cableA in cablesA)
            foreach (var cableB in cablesB)
            {
                if (EntityManager.GetComponent<TransformComponent>(cableA.Owner).Anchored &&
                    EntityManager.GetComponent<TransformComponent>(cableB.Owner).Anchored &&
                    CanConnect(cableA, cableB))
                {
                    cableA.AddAlwaysReachable(cableB);
                    cableB.AddAlwaysReachable(cableA);
                    EntityManager.System<NodeGroupSystem>().QueueReflood(cableA);
                    EntityManager.System<NodeGroupSystem>().QueueReflood(cableB);
                }
            }
        }

        private void OnUndocked(UndockEvent ev)
        {
            var dockA = ev.DockA.Owner;
            var dockB = ev.DockB.Owner;

            var cablesA = GetDockCableNodes(dockA).ToList();
            var cablesB = GetDockCableNodes(dockB).ToList();

            foreach (var cableA in cablesA)
            foreach (var cableB in cablesB)
            {
                cableA.RemoveAlwaysReachable(cableB);
                cableB.RemoveAlwaysReachable(cableA);
                EntityManager.System<NodeGroupSystem>().QueueReflood(cableA);
                EntityManager.System<NodeGroupSystem>().QueueReflood(cableB);
            }
        }

        #endregion

        #region Cable Query
        
        public IEnumerable<CableNode> GetDockCableNodes(EntityUid dock)
        {
            if (!TryComp<TransformComponent>(dock, out var xform) || xform.GridUid == null)
                yield break;
            if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                yield break;

            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
            var foundTypes = new HashSet<CableType>();
            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile))
            {
                if (ent == dock)
                    continue;
                if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
                    continue;
                if (!TryComp<TransformComponent>(ent, out var entXform) || !entXform.Anchored)
                    continue;
                foreach (var node in nodeContainer.Nodes.Values.OfType<CableNode>())
                {
                    if (TryComp<CableComponent>(node.Owner, out var cable) && ShouldDockCableType(cable))
                    {
                        if (foundTypes.Add(cable.CableType))
                            yield return node;
                    }
                }
            }
        }

        private bool ShouldDockCableType(CableComponent cable)
        {
            return cable.CableType switch
            {
                CableType.HighVoltage => DockHV,
                CableType.MediumVoltage => DockMV,
                CableType.Apc => DockLV,
                _ => false
            };
        }

        private bool ShouldDockCableType(CableNode node)
        {
            if (!TryComp<CableComponent>(node.Owner, out var cable))
                return false;
            return ShouldDockCableType(cable);
        }

        public bool CanConnect(CableNode a, CableNode b)
        {
            if (a == b)
                return false;
            if (!TryComp<CableComponent>(a.Owner, out var cableA) || !ShouldDockCableType(cableA))
                return false;
            if (!TryComp<CableComponent>(b.Owner, out var cableB) || !ShouldDockCableType(cableB))
                return false;
            if (a.Deleting || b.Deleting)
                return false;
            if (cableA.CableType != cableB.CableType)
                return false;
            return true;
        }

        public void TryConnectDockedCable(CableNode node)
        {
            if (!ShouldDockCableType(node))
                return;
            if (!TryComp<TransformComponent>(node.Owner, out var xform) || xform.GridUid == null || !xform.Anchored)
                return;
            if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                return;
            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);

            var anchoredEntities = _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile).ToList();
            var dockedEntities = anchoredEntities.Where(ent =>
                ent != node.Owner &&
                TryComp<DockingComponent>(ent, out var docking) &&
                docking.DockedWith is not null).ToList();

            foreach (var ent in dockedEntities)
            {
                var docking = Comp<DockingComponent>(ent);
                var otherDock = docking.DockedWith!.Value;
                var otherCables = GetDockCableNodes(otherDock).Where(p => TryComp<TransformComponent>(p.Owner, out var pXform) && pXform.Anchored);
                foreach (var otherCable in otherCables)
                {
                    if (CanConnect(node, otherCable))
                    {
                        node.AddAlwaysReachable(otherCable);
                        otherCable.AddAlwaysReachable(node);
                        EntityManager.System<NodeGroupSystem>().QueueReflood(node);
                        EntityManager.System<NodeGroupSystem>().QueueReflood(otherCable);
                    }
                }
            }
        }

        public void RemoveDockConnections(CableNode node)
        {
            var reachable = node.GetAlwaysReachable();
            if (reachable == null)
                return;
            foreach (var target in reachable.ToList())
            {
                if (target is CableNode cableNode &&
                    TryComp<CableComponent>(cableNode.Owner, out var cable) &&
                    ShouldDockCableType(cable))
                {
                    node.RemoveAlwaysReachable(cableNode);
                    cableNode.RemoveAlwaysReachable(node);
                    EntityManager.System<NodeGroupSystem>().QueueReflood(node);
                    EntityManager.System<NodeGroupSystem>().QueueReflood(cableNode);
                }
            }
        }

        #endregion

        #region Dock Checking

        public void CheckForDockConnections(EntityUid cableEntity, CableNode cableNode)
        {
            if (!_dockConnectionsChecked.Add(cableEntity))
                return;

            if (!TryComp<TransformComponent>(cableEntity, out var xform) || xform.GridUid == null || !xform.Anchored)
                return;
            if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                return;
            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);

            var anchoredEntities = _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile).ToList();
            var dockedEntities = anchoredEntities.Where(ent =>
                ent != cableEntity &&
                TryComp<DockingComponent>(ent, out var docking) &&
                docking.DockedWith is not null).ToList();

            foreach (var ent in dockedEntities)
            {
                var docking = Comp<DockingComponent>(ent);
                var otherDock = docking.DockedWith!.Value;
                var cablesOther = GetDockCableNodes(otherDock).Where(p => TryComp<TransformComponent>(p.Owner, out var pXform) && pXform.Anchored);
                foreach (var other in cablesOther)
                {
                    if (CanConnect(cableNode, other))
                    {
                        var reachableA = cableNode.GetAlwaysReachable();
                        var reachableB = other.GetAlwaysReachable();
                        if (reachableA == null || !reachableA.Contains(other))
                            cableNode.AddAlwaysReachable(other);
                        if (reachableB == null || !reachableB.Contains(cableNode))
                            other.AddAlwaysReachable(cableNode);
                    }
                }
            }
        }

        #endregion

        #region Debug Commands

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

        public string GetDebugInfo()
        {
            var lines = new List<string> { "Docked cable connections:" };
            foreach (var (dockA, dockB) in GetAllDockedPairs())
            {
                var cablesA = GetDockCableNodes(dockA).ToList();
                var cablesB = GetDockCableNodes(dockB).ToList();
                int count = 0;
                foreach (var cableA in cablesA)
                foreach (var cableB in cablesB)
                {
                    var reachable = cableA.GetAlwaysReachable();
                    if (CanConnect(cableA, cableB) && reachable != null && reachable.Contains(cableB))
                        count++;
                }
                lines.Add($"Dock {dockA} <-> {dockB}: {count} connections");
            }
            return string.Join('\n', lines);
        }

        public string GetCableDebugInfo(EntityUid entity)
        {
            if (!EntityManager.TryGetComponent<NodeContainerComponent>(entity, out var nodeContainer))
                return "No NodeContainerComponent.";
            foreach (var node in nodeContainer.Nodes.Values)
            {
                if (node is CableNode cable)
                {
                    var reachable = cable.GetAlwaysReachable();
                    if (reachable == null || reachable.Count == 0)
                        return $"Cable {entity} has no dock connections.";
                    var targets = string.Join(", ", reachable.Select(x => x.Owner.ToString()));
                    return $"Cable {entity} dock connections: {targets}";
                }
            }
            return "No CableNode found.";
        }

        public string GetTileDebugInfo(EntityUid gridId, Vector2i tile)
        {
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

        public string TestCableConnection(EntityUid cableAUid, EntityUid cableBUid)
        {
            if (!EntityManager.TryGetComponent<NodeContainerComponent>(cableAUid, out var nodeA) ||
                !EntityManager.TryGetComponent<NodeContainerComponent>(cableBUid, out var nodeB))
                return "One or both entities are not cables.";
            CableNode? cableA = nodeA.Nodes.Values.OfType<CableNode>().FirstOrDefault();
            CableNode? cableB = nodeB.Nodes.Values.OfType<CableNode>().FirstOrDefault();
            if (cableA == null || cableB == null)
                return "One or both entities are not cables.";
            var reachable = cableA.GetAlwaysReachable();
            if (reachable != null && reachable.Contains(cableB))
                return $"Cables {cableAUid} and {cableBUid} are dock-connected.";
            return $"Cables {cableAUid} and {cableBUid} are not dock-connected.";
        }

        public void RefreshAllDockConnections()
        {
            ClearDockConnectionsChecked();
            foreach (var (dockA, dockB) in GetAllDockedPairs())
            {
                var cablesA = GetDockCableNodes(dockA).ToList();
                var cablesB = GetDockCableNodes(dockB).ToList();

                var anchoredA = new HashSet<EntityUid>(cablesA.Select(p => p.Owner));
                var anchoredB = new HashSet<EntityUid>(cablesB.Select(p => p.Owner));

                foreach (var cableA in cablesA)
                foreach (var cableB in cablesB)
                {
                    if (!anchoredA.Contains(cableA.Owner) || !anchoredB.Contains(cableB.Owner) ||
                        !EntityManager.GetComponent<TransformComponent>(cableA.Owner).Anchored ||
                        !EntityManager.GetComponent<TransformComponent>(cableB.Owner).Anchored) continue;
                    var reachableA = cableA.GetAlwaysReachable();
                    var reachableB = cableB.GetAlwaysReachable();
                    if (reachableA != null && reachableA.Contains(cableB))
                        cableA.RemoveAlwaysReachable(cableB);
                    if (reachableB != null && reachableB.Contains(cableA))
                        cableB.RemoveAlwaysReachable(cableA);
                }
                foreach (var cableA in cablesA)
                foreach (var cableB in cablesB)
                    if (anchoredA.Contains(cableA.Owner) && anchoredB.Contains(cableB.Owner) &&
                        EntityManager.GetComponent<TransformComponent>(cableA.Owner).Anchored &&
                        EntityManager.GetComponent<TransformComponent>(cableB.Owner).Anchored &&
                        CanConnect(cableA, cableB))
                    {
                        cableA.AddAlwaysReachable(cableB);
                        cableB.AddAlwaysReachable(cableA);
                    }
            }
        }

        private void ClearDockConnectionsChecked()
        {
            _dockConnectionsChecked.Clear();
        }

        #endregion
    }
}
