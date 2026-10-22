using System.Linq;
using System.Numerics;
using Robust.Server.GameObjects;
using Robust.Server.Physics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network.Messages;
using Robust.Shared.Placement;
using Robust.Shared.Timing;

// ReSharper disable CheckNamespace
namespace Content.Server.Sandbox;

public sealed partial class SandboxSystem
{
    [Dependency] private MapSystem _map = null!;
    [Dependency] private TransformSystem _transform = null!;
    [Dependency] private GridFixtureSystem _gridFixture = null!;
    [Dependency] private IGameTiming _timing = null!;

    private static readonly TimeSpan _mergeWindow = TimeSpan.FromSeconds(0.5);

    private static readonly Vector2i[] _cardinalOffsets =
    [
        Vector2i.Right,
        Vector2i.Left,
        Vector2i.Up,
        Vector2i.Down
    ];

    private readonly Dictionary<EntityUid, TimeSpan> _placedGrids = [];
    private List<Entity<MapGridComponent>> _grids = [];
    private readonly List<Entity<MapGridComponent>> _adjacent = [];
    private readonly List<EntityUid> _expired = [];
    private readonly List<Entity<MapGridComponent>> _pendingMerges = [];
    private Entity<MapGridComponent>? _mergeTarget;

    private bool CanPlace(MsgPlacement placement)
    {
        if (IsSandboxEnabled) return true;

        var channel = placement.MsgChannel;
        var player = _playerManager.GetSessionByChannel(channel);

        return _conGroupController.CanAdminPlace(player);
    }

    [SubscribeLocalEvent]
    private void OnPlacementTile(PlacementTileEvent ev)
    {
        var now = _timing.CurTime;

        _expired.Clear();
        foreach (var (uid, time) in _placedGrids)
            if (now - time > _mergeWindow || TerminatingOrDeleted(uid))
                _expired.Add(uid);
        foreach (var uid in _expired)
            _placedGrids.Remove(uid);
        if (ev.TileType == 0)
            return;

        if (!HasComp<MapGridComponent>(ev.Coordinates.EntityId))
        {
            if (_map.TryFindGridAt(_transform.ToMapCoordinates(ev.Coordinates), out var newGrid, out _))
                _placedGrids[newGrid] = now;

            return;
        }

        if (_placedGrids.ContainsKey(ev.Coordinates.EntityId))
            _placedGrids[ev.Coordinates.EntityId] = now;
        if (_mergeTarget is { } target && target.Owner == ev.Coordinates.EntityId)
            foreach (var grid in _pendingMerges)
                TryMerge(target, grid);

        _pendingMerges.Clear();
        _mergeTarget = null;
    }

    public void TryRedirectToAdjacentGrid(MsgPlacement msg)
    {
        _pendingMerges.Clear();
        _mergeTarget = null;

        if (msg.PlaceType != PlacementManagerMessage.RequestPlacement || !msg.IsTile || msg.TileType == 0)
            return;

        var coords = GetCoordinates(msg.NetCoordinates);
        if (!coords.IsValid(EntityManager) || HasComp<MapGridComponent>(coords.EntityId))
            return;
        var mapCoords = _transform.ToMapCoordinates(coords);
        if (mapCoords.MapId == MapId.Nullspace || _map.TryFindGridAt(mapCoords, out _, out _))
            return;

        _grids.Clear();
        _map.FindGridsIntersecting(mapCoords.MapId, Box2.CenteredAround(mapCoords.Position, new Vector2(2f)),
            ref _grids, includeMap: false);
        _adjacent.Clear();
        foreach (var grid in _grids.Where(grid =>
                    HasAdjacentTile(grid, _map.WorldToTile(grid, grid.Comp, mapCoords.Position)))) _adjacent.Add(grid);

        if (_adjacent.Count == 0)
            return;

        var target = _adjacent[0];
        foreach (var grid in _adjacent.Where(grid => IsBetterTarget(grid, target))) target = grid;

        _mergeTarget = target;
        foreach (var grid in _adjacent.Where(grid => grid.Owner != target.Owner && _placedGrids.ContainsKey(grid)))
            _pendingMerges.Add(grid);

        var tile = _map.WorldToTile(target, target.Comp, mapCoords.Position);
        msg.NetCoordinates = GetNetCoordinates(_map.GridTileToLocal(target, target.Comp, tile));
    }

    private bool IsBetterTarget(Entity<MapGridComponent> grid, Entity<MapGridComponent> current)
    {
        var gridPlaced = _placedGrids.ContainsKey(grid);
        var currentPlaced = _placedGrids.ContainsKey(current);

        if (gridPlaced != currentPlaced)
            return !gridPlaced;

        return Box2.Area(grid.Comp.LocalAABB) > Box2.Area(current.Comp.LocalAABB);
    }

    private void TryMerge(Entity<MapGridComponent> target, Entity<MapGridComponent> other)
    {
        if (TerminatingOrDeleted(target) || TerminatingOrDeleted(other))
            return;

        var targetXform = Transform(target);
        var otherXform = Transform(other);

        if (!_transform.GetWorldRotation(targetXform).EqualsApprox(_transform.GetWorldRotation(otherXform)))
            return;

        var offset = Vector2.Transform(_transform.GetWorldPosition(otherXform),
            _transform.GetInvWorldMatrix(targetXform));
        var rounded = new Vector2i((int)MathF.Round(offset.X), (int)MathF.Round(offset.Y));

        if ((offset - rounded).Length() > 0.01f)
            return;

        _gridFixture.Merge(target, other, rounded, Angle.Zero, target.Comp, other.Comp, targetXform, otherXform);
        _placedGrids.Remove(other);
    }

    private bool HasAdjacentTile(Entity<MapGridComponent> grid, Vector2i tile)
    {
        foreach (var offset in _cardinalOffsets)
            if (_map.TryGetTileRef(grid, grid.Comp, tile + offset, out var tileRef) && !tileRef.Tile.IsEmpty)
                return true;

        return false;
    }
}
