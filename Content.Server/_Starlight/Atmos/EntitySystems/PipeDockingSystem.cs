using Content.Server.NodeContainer.Nodes;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Components;
using Content.Shared.NodeContainer;
using Content.Shared.Atmos;
using Robust.Shared.Map.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._Starlight.Atmos.EntitySystems;

/// <summary>
/// Allows pipes to connect over docks.
/// </summary>
public sealed class PipeDockingSystem : EntitySystem
{
    #region Dependencies

    [Dependency] public readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly NodeGroupSystem _nodeGroupSystem = default!;

    private readonly List<PipeNode> _dockAPipes = [];
    private readonly List<PipeNode> _dockBPipes = [];
    private readonly List<PipeNode> _localPipes = [];
    private readonly List<PipeNode> _otherPipes = [];
    private readonly List<PipeNode> _reachablePipeScratch = [];

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

        if (!TryGetDockEntity(ev.DockA, out var dockA) || !TryGetDockEntity(ev.DockB, out var dockB))
            return;

        GetDockConnectingPipes(dockA, _dockAPipes);
        GetDockConnectingPipes(dockB, _dockBPipes);

        foreach (var pipeA in _dockAPipes)
        {
            foreach (var pipeB in _dockBPipes)
            {
                if (!CanConnect(pipeA, pipeB))
                    continue;

                LinkPipes(pipeA, pipeB);
            }
        }
    }

    private void OnUndocked(UndockEvent ev)
    {
        if (!TryGetDockEntity(ev.DockA, out var dockA) || !TryGetDockEntity(ev.DockB, out var dockB))
            return;

        GetDockConnectingPipes(dockA, _dockAPipes, includeDisabled: true);
        GetDockConnectingPipes(dockB, _dockBPipes, includeDisabled: true);

        foreach (var pipeA in _dockAPipes)
        {
            foreach (var pipeB in _dockBPipes)
            {
                if (!CanConnect(pipeA, pipeB))
                    continue;

                UnlinkPipes(pipeA, pipeB);
            }
        }
    }

    private void GetDockConnectingPipes(EntityUid dock, List<PipeNode> dockNodes, bool includeDisabled = false)
    {
        dockNodes.Clear();

        var xform = Transform(dock);
        if (xform.GridUid == null)
            return;

        if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
            return;

        var dockTile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
        var dockDir = xform.LocalRotation.GetCardinalDir().ToPipeDirection();

        foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, dockTile))
        {
            if (ent == dock)
                continue;

            if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
                continue;

            foreach (var node in nodeContainer.Nodes.Values)
            {
                if (node is not PipeNode pipeNode)
                    continue;

                if (pipeNode.Deleting)
                    continue;

                if (!includeDisabled && !ShouldDockPipeType(pipeNode))
                    continue;

                if (!pipeNode.CurrentPipeDirection.HasDirection(dockDir))
                    continue;

                dockNodes.Add(pipeNode);
            }
        }
    }

    #endregion

    #region Pipe Query

    public bool ShouldDockPipeType(PipeNode _)
        => DockPipes;

    public List<PipeNode> GetTilePipes(EntityUid dock, bool includeDisabled = false)
    {
        var result = new List<PipeNode>();

        if (!includeDisabled && !DockPipes)
            return result;

        if (!TryGetAnchoredTile(dock, out var gridUid, out var grid, out var tile))
            return result;

        foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (ent == dock)
                continue;

            if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
                continue;

            foreach (var node in nodeContainer.Nodes.Values)
            {
                if (node is not PipeNode pipe)
                    continue;

                if (pipe.Deleting)
                    continue;

                if (!includeDisabled && !ShouldDockPipeType(pipe))
                    continue;

                result.Add(pipe);
            }
        }

        return result;
    }

    public static bool CanConnect(PipeNode a, PipeNode b)
        => a != b
            && a.NodeGroupID == b.NodeGroupID
            && a.CurrentPipeLayer == b.CurrentPipeLayer
            && !a.Deleting
            && !b.Deleting;

    #endregion

    #region Anchor Handling

    /// <summary>
    /// Anchoring Pipes
    /// </summary>
    public void TryConnectDockedPipe(EntityUid pipeEntity)
    {
        if (!DockPipes)
            return;

        if (!TryComp<NodeContainerComponent>(pipeEntity, out var nodeContainer))
            return;

        if (!TryGetAnchoredTile(pipeEntity, out var gridUid, out var grid, out var tile))
            return;

        var removedOldConnections = false;

        foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (ent == pipeEntity)
                continue;

            if (!TryComp<DockingComponent>(ent, out var docking) || docking.DockedWith is not { } otherDock)
                continue;

            if (!removedOldConnections)
            {
                RemoveDockConnections(pipeEntity, nodeContainer);
                removedOldConnections = true;
            }

            GetDockConnectingPipes(ent, _localPipes);
            GetDockConnectingPipes(otherDock, _otherPipes);

            foreach (var node in _localPipes)
            {
                if (node.Owner != pipeEntity)
                    continue;

                foreach (var pipeB in _otherPipes)
                {
                    if (!CanConnect(node, pipeB))
                        continue;

                    LinkPipes(node, pipeB);
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

        if (!TryGetAnchoredTile(pipeEntity, out var gridUid, out var grid, out var tile))
            return;

        foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (ent == pipeEntity)
                continue;

            if (!TryComp<DockingComponent>(ent, out var docking) || docking.DockedWith is not { } otherDock)
                continue;

            GetDockConnectingPipes(otherDock, _otherPipes);

            foreach (var pipeB in _otherPipes)
            {
                if (!CanConnect(pipeNode, pipeB))
                    continue;

                LinkPipes(pipeNode, pipeB);
            }
        }
    }

    #endregion

    public void RemoveDockConnections(EntityUid pipeEntity)
    {
        if (!TryComp<NodeContainerComponent>(pipeEntity, out var nodeContainer))
            return;

        RemoveDockConnections(pipeEntity, nodeContainer);
    }

    private void RemoveDockConnections(EntityUid _, NodeContainerComponent nodeContainer)
    {
        foreach (var node in nodeContainer.Nodes.Values)
        {
            if (node is not PipeNode pipe)
                continue;

            var reachable = pipe.GetAlwaysReachable();
            if (reachable == null)
                continue;

            _reachablePipeScratch.Clear();

            foreach (var target in reachable)
            {
                if (target is not PipeNode pipeNode)
                    continue;

                _reachablePipeScratch.Add(pipeNode);
            }

            if (_reachablePipeScratch.Count == 0)
                continue;

            foreach (var pipeNode in _reachablePipeScratch)
            {
                pipe.RemoveAlwaysReachable(pipeNode);
                pipeNode.RemoveAlwaysReachable(pipe);
                _nodeGroupSystem.QueueReflood(pipeNode);
            }

            _nodeGroupSystem.QueueReflood(pipe);
        }
    }

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

    private void LinkPipes(PipeNode a, PipeNode b)
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

    private void UnlinkPipes(PipeNode a, PipeNode b)
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
