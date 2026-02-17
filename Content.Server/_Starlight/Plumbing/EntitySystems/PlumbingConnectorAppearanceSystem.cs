using Content.Server._Starlight.Plumbing.Nodes;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared._Starlight.Plumbing;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared.Atmos;
using Content.Shared.Maps;
using Content.Shared.NodeContainer;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Starlight.Plumbing.EntitySystems;

/// <summary>
///     Server system that sends PlumbingNode directions and connection state to clients.
///     Also tracks floor coverage to hide connectors under floor tiles.
/// </summary>
public sealed class PlumbingConnectorAppearanceSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;

    private static readonly PipeDirection[] CardinalDirections =
    [
        PipeDirection.North,
        PipeDirection.South,
        PipeDirection.East,
        PipeDirection.West
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlumbingConnectorAppearanceComponent, NodeGroupsRebuilt>(OnNodeUpdate);
        SubscribeLocalEvent<PlumbingConnectorAppearanceComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
    }

    private void OnStartup(EntityUid uid, PlumbingConnectorAppearanceComponent component, ComponentStartup args)
    {
        UpdateAppearance(uid);
    }

    private void OnNodeUpdate(EntityUid uid, PlumbingConnectorAppearanceComponent component, ref NodeGroupsRebuilt args)
    {
        UpdateAppearance(args.NodeOwner);
    }

    private void OnTileChanged(ref TileChangedEvent ev)
    {
        var grid = ev.Entity.Comp;

        foreach (var change in ev.Changes)
        {
            foreach (var uid in _map.GetAnchoredEntities(ev.Entity, grid, change.GridIndices))
            {
                if (HasComp<PlumbingConnectorAppearanceComponent>(uid))
                    UpdateAppearance(uid);
            }
        }
    }

    private bool HasFloorCover(EntityUid gridUid, MapGridComponent grid, Vector2i position)
    {
        var tileRef = _map.GetTileRef(gridUid, grid, position);
        var tileDef = (ContentTileDefinition)_tileDefManager[tileRef.Tile.TypeId];
        return !tileDef.IsSubFloor;
    }

    private void UpdateAppearance(EntityUid uid, AppearanceComponent? appearance = null,
        NodeContainerComponent? container = null, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref appearance, ref container, ref xform, false))
            return;

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        if (!TryComp<PlumbingConnectorAppearanceComponent>(uid, out var connectorComp))
            return;

        var tile = _map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);

        var nodeDirections = PipeDirection.None;
        var connectedDirections = PipeDirection.None;
        var inletDirections = PipeDirection.None;
        var outletDirections = PipeDirection.None;
        var mixingInletDirections = PipeDirection.None;

        TryComp<PlumbingInletComponent>(uid, out var inletComp);
        TryComp<PlumbingOutletComponent>(uid, out var outletComp);

        foreach (var (nodeName, node) in container.Nodes)
        {
            if (node is not PlumbingNode plumbingNode)
                continue;

            var nodeDir = plumbingNode.CurrentPipeDirection;
            nodeDirections |= nodeDir;

            if (connectorComp.MixingInletNames.Contains(nodeName))
            {
                mixingInletDirections |= nodeDir;
            }
            // Classify as inlet/outlet based on component match OR node name fallback
            else
            {
                var isInlet = false;
                if (inletComp != null)
                {
                    foreach (var inletName in inletComp.InletNames)
                    {
                        if (nodeName.Equals(inletName, StringComparison.OrdinalIgnoreCase))
                        {
                            isInlet = true;
                            break;
                        }
                    }
                }

                var isOutlet = false;
                if (outletComp != null)
                {
                    foreach (var outletName in outletComp.OutletNames)
                    {
                        if (nodeName.Equals(outletName, StringComparison.OrdinalIgnoreCase))
                        {
                            isOutlet = true;
                            break;
                        }
                    }
                }

                // Fallback: any node literally named "inlet" is colored as inlet
                if (!isInlet && !isOutlet && nodeName.Equals("inlet", StringComparison.OrdinalIgnoreCase))
                    isInlet = true;

                if (isInlet)
                    inletDirections |= nodeDir;
                else if (isOutlet)
                    outletDirections |= nodeDir;
            }

            connectedDirections |= GetConnectedDirections(node, nodeDir, tile, xform.GridUid.Value, grid);
        }

        var coveredByFloor = HasFloorCover(xform.GridUid.Value, grid, tile);

        _appearance.SetData(uid, PlumbingVisuals.NodeDirections, (int)nodeDirections, appearance);
        _appearance.SetData(uid, PlumbingVisuals.ConnectedDirections, (int)connectedDirections, appearance);
        _appearance.SetData(uid, PlumbingVisuals.InletDirections, (int)inletDirections, appearance);
        _appearance.SetData(uid, PlumbingVisuals.OutletDirections, (int)outletDirections, appearance);
        _appearance.SetData(uid, PlumbingVisuals.MixingInletDirections, (int)mixingInletDirections, appearance);
        _appearance.SetData(uid, PlumbingVisuals.CoveredByFloor, coveredByFloor, appearance);
    }

    private PipeDirection GetConnectedDirections(Node node, PipeDirection nodeDir, Vector2i tile,
        EntityUid gridUid, MapGridComponent grid)
    {
        var connected = PipeDirection.None;

        foreach (var dir in CardinalDirections)
        {
            if (!nodeDir.HasFlag(dir))
                continue;

            var neighborTile = dir switch
            {
                PipeDirection.North => tile + (0, 1),
                PipeDirection.South => tile + (0, -1),
                PipeDirection.East => tile + (1, 0),
                PipeDirection.West => tile + (-1, 0),
                _ => tile
            };

            foreach (var reachable in node.ReachableNodes)
            {
                if (reachable is not PlumbingNode || reachable.Owner == node.Owner)
                    continue;

                var otherTile = _map.TileIndicesFor(gridUid, grid, Transform(reachable.Owner).Coordinates);
                if (otherTile == neighborTile)
                {
                    connected |= dir;
                    break;
                }
            }
        }

        return connected;
    }
}
