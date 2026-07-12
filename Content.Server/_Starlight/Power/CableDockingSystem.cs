using Content.Server.Power.Components;
using Content.Server.Power.Nodes;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.NodeContainer;
using Content.Shared.Power;
using Robust.Shared.Map.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Shared._Starlight.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._Starlight.Power;

/// <summary>
/// Allows cables to connect over docks.
/// </summary>
public sealed partial class CableDockingSystem : EntitySystem
{
    #region Dependencies

    [Dependency] public SharedMapSystem _mapSystem = default!;
    [Dependency] private NodeGroupSystem _nodeGroupSystem = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;

    private readonly List<CableNode> _dockACables = [];
    private readonly List<CableNode> _dockBCables = [];
    private readonly List<CableNode> _otherCables = [];
    private readonly List<CableNode> _reachableCableScratch = [];

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
        if (!TryGetDockEntity(ev.DockA, out var dockA) || !TryGetDockEntity(ev.DockB, out var dockB))
            return;

        GetDockCableNodes(dockA, _dockACables);
        GetDockCableNodes(dockB, _dockBCables);

        foreach (var cableA in _dockACables)
        {
            foreach (var cableB in _dockBCables)
            {
                if (!CanConnect(cableA, cableB))
                    continue;

                LinkCables(cableA, cableB);
            }
        }
    }

    private void OnUndocked(UndockEvent ev)
    {
        if (!TryGetDockEntity(ev.DockA, out var dockA) || !TryGetDockEntity(ev.DockB, out var dockB))
            return;

        GetDockCableNodes(dockA, _dockACables);
        GetDockCableNodes(dockB, _dockBCables);

        foreach (var cableA in _dockACables)
        {
            foreach (var cableB in _dockBCables)
            {
                UnlinkCables(cableA, cableB);
            }
        }
    }

    #endregion

    #region Cable Query

    public IEnumerable<CableNode> GetDockCableNodes(EntityUid dock)
    {
        var result = new List<CableNode>();
        GetDockCableNodes(dock, result);
        return result;
    }

    private void GetDockCableNodes(EntityUid dock, List<CableNode> result)
    {
        result.Clear();

        var xform = Transform(dock);
        if (xform.GridUid == null)
            return;

        if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
            return;

        var dockTile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);

        foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, dockTile))
        {
            if (ent == dock)
                continue;

            if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
                continue;

            foreach (var node in nodeContainer.Nodes.Values)
            {
                if (node is not CableNode cableNode)
                    continue;

                if (cableNode.Deleting)
                    continue;

                if (!TryComp<CableComponent>(cableNode.Owner, out var cable))
                    continue;

                if (!ShouldDockCableType(cable))
                    continue;

                result.Add(cableNode);
            }
        }
    }

    private bool ShouldDockCableType(CableComponent cable)
        => cable.CableType switch
        {
            CableType.HighVoltage => DockHV,
            CableType.MediumVoltage => DockMV,
            CableType.Apc => DockLV,
            _ => false
        };

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

        if (a.Deleting || b.Deleting)
            return false;

        if (!TryComp<CableComponent>(a.Owner, out var cableA) || !ShouldDockCableType(cableA))
            return false;

        if (!TryComp<CableComponent>(b.Owner, out var cableB) || !ShouldDockCableType(cableB))
            return false;

        return cableA.CableType == cableB.CableType;
    }

    public void TryConnectDockedCable(CableNode node)
    {
        if (!ShouldDockCableType(node))
            return;

        if (!TryGetAnchoredTile(node.Owner, out var gridUid, out var grid, out var tile))
            return;

        foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (ent == node.Owner)
                continue;

            if (!TryComp<DockingComponent>(ent, out var docking) || docking.DockedWith is not { } otherDock)
                continue;

            GetDockCableNodes(otherDock, _otherCables);

            foreach (var otherCable in _otherCables)
            {
                if (!CanConnect(node, otherCable))
                    continue;

                LinkCables(node, otherCable);
            }
        }
    }

    public void RemoveDockConnections(CableNode node)
    {
        var reachable = node.GetAlwaysReachable();
        if (reachable == null)
            return;

        _reachableCableScratch.Clear();

        foreach (var target in reachable)
        {
            if (target is not CableNode cableNode)
                continue;

            if (!TryComp<CableComponent>(cableNode.Owner, out var cable))
                continue;

            if (!ShouldDockCableType(cable))
                continue;

            _reachableCableScratch.Add(cableNode);
        }

        foreach (var cableNode in _reachableCableScratch)
        {
            UnlinkCables(node, cableNode);
        }
    }

    #endregion

    private bool TryGetAnchoredTile(
        EntityUid uid,
        out EntityUid gridUid,
        out MapGridComponent grid,
        out Vector2i tile)
    {
        gridUid = default;
        grid = default!;
        tile = default;

        var xform = Transform(uid);
        if (xform.GridUid is not { } xformGridUid || !xform.Anchored)
            return false;

        if (!TryComp<MapGridComponent>(xformGridUid, out var gridComp))
            return false;

        gridUid = xformGridUid;
        grid = gridComp;
        tile = _mapSystem.TileIndicesFor(gridUid, grid, xform.Coordinates);
        return true;
    }

#pragma warning disable CS0618 // Using .Owner for Performance.
    private static bool TryGetDockEntity(DockingComponent component, out EntityUid uid)
    {
        uid = component.Owner;
        return true;
    }
#pragma warning restore CS0618

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
