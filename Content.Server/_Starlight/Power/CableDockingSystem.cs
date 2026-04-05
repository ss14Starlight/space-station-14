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
using Content.Server.NodeContainer.EntitySystems;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._Starlight.Power.EntitySystems
{
    /// <summary>
    /// Allows cables to connect over docks.
    /// </summary>
    public sealed class CableDockingSystem : EntitySystem
    {
        #region Dependencies

        [Dependency] public readonly SharedMapSystem _mapSystem = default!;
        [Dependency] private readonly NodeGroupSystem _nodeGroupSystem = default!;
        [Dependency] private IConfigurationManager _configurationManager = default!;

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
                if (IsAnchored(cableA.Owner) &&
                    IsAnchored(cableB.Owner) &&
                    CanConnect(cableA, cableB))
                {
                    LinkCables(cableA, cableB);
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
                UnlinkCables(cableA, cableB);
            }
        }

        #endregion

        #region Cable Query

        public IEnumerable<CableNode> GetDockCableNodes(EntityUid dock)
        {
            var xform = Transform(dock);
            if (xform.GridUid == null)
                yield break;
            if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                yield break;

            var dockTile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, dockTile))
            {
                if (ent == dock)
                    continue;
                if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
                    continue;
                var entXform = Transform(ent);
                if (!entXform.Anchored)
                    continue;
                foreach (var node in nodeContainer.Nodes.Values.OfType<CableNode>())
                {
                    if (TryComp<CableComponent>(node.Owner, out var cable) && ShouldDockCableType(cable))
                        yield return node;
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
            var xform = Transform(node.Owner);
            if (xform.GridUid == null || !xform.Anchored)
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
                var otherCables = GetDockCableNodes(otherDock).Where(p => IsAnchored(p.Owner));
                foreach (var otherCable in otherCables)
                {
                    if (CanConnect(node, otherCable))
                        LinkCables(node, otherCable);
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
                    UnlinkCables(node, cableNode);
                }
            }
        }

        #endregion

        private bool IsAnchored(EntityUid uid)
        {
            return Transform(uid).Anchored;
        }

        private void LinkCables(CableNode a, CableNode b)
        {
            var reachableA = a.GetAlwaysReachable();
            var reachableB = b.GetAlwaysReachable();
            if (reachableA != null && reachableA.Contains(b) && reachableB != null && reachableB.Contains(a))
                return;

            a.AddAlwaysReachable(b);
            b.AddAlwaysReachable(a);
            _nodeGroupSystem.QueueReflood(a);
            _nodeGroupSystem.QueueReflood(b);
        }

        private void UnlinkCables(CableNode a, CableNode b)
        {
            var reachableA = a.GetAlwaysReachable();
            var reachableB = b.GetAlwaysReachable();
            var changed = false;

            if (reachableA != null && reachableA.Contains(b))
            {
                a.RemoveAlwaysReachable(b);
                changed = true;
            }

            if (reachableB != null && reachableB.Contains(a))
            {
                b.RemoveAlwaysReachable(a);
                changed = true;
            }

            if (!changed)
                return;

            _nodeGroupSystem.QueueReflood(a);
            _nodeGroupSystem.QueueReflood(b);
        }
    }
}
