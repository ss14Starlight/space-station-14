using System.Numerics;
using Content.Client._Starlight.Zones.Overlays;
using Content.Shared._Starlight.Zones;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Zones;

public sealed partial class ZonePlacementSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private InputSystem _inputSystem = default!;
    [Dependency] private MapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<NetEntity, List<ZoneShapeSet>> _shapes = [];

    private readonly HashSet<NetEntity> _requested = [];

    private readonly Dictionary<NetEntity, ZoneRoomView> _rooms = [];

    private TimeSpan _nextRoomRequest;

    private static readonly TimeSpan _roomRefresh = TimeSpan.FromSeconds(0.5);

    public bool Active { get; private set; }

    public bool ShowZones { get; private set; }

    public bool ShowRooms { get; private set; }

    public ProtoId<ZonePrototype>? Selected;

    private (EntityUid Grid, Vector2i Tile, bool Erasing)? _drag;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.EditorPlaceObject, new PointerStateInputCmdHandler(
                (_, coords, _) => BeginDrag(coords, erasing: false),
                (_, coords, _) => EndDrag(coords),
                true))
            .Bind(EngineKeyFunctions.EditorCancelPlace, new PointerStateInputCmdHandler(
                (_, coords, _) => BeginDrag(coords, erasing: true),
                (_, coords, _) => EndDrag(coords),
                true))
            .Register<ZonePlacementSystem>();

        _overlay.AddOverlay(new ZonePlacementOverlay(this, _transform, _eye, _proto, _cache));
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<ZonePlacementOverlay>();
        CommandBinds.Unregister<ZonePlacementSystem>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (ShowRooms)
            RequestRooms();

        if (!Active && !ShowZones)
            return;

        if (TryGetHoveredGrid(out var grid))
            RequestShapes(grid);

        if (_player.LocalEntity is { } player && _transform.GetGrid(player) is { } playerGrid)
            RequestShapes(playerGrid);
    }

    #region Activation

    /// <summary>
    /// Activates or deactivates the zone placement system, enabling or disabling the editor context and clearing any ongoing drag operations.
    /// </summary>
    public void SetActive(bool active)
    {
        Active = active;
        _drag = null;

        if (active)
            _input.Contexts.SetActiveContext("editor");
        else
            _inputSystem.SetEntityContextActive();
    }

    /// <summary>
    /// Toggles the visibility of zones in the editor. If zones are hidden and the system is not active, it clears any cached zone shapes and requested grids.
    /// </summary>
    public void ToggleShowZones()
    {
        ShowZones = !ShowZones;

        if (!ShowZones && !Active)
        {
            _shapes.Clear();
            _requested.Clear();
        }
    }

    /// <summary>
    /// Toggles the visibility of rooms in the editor. If rooms are hidden, it clears any cached room data and resets the next room request timer.
    /// </summary>
    public void ToggleShowRooms()
    {
        ShowRooms = !ShowRooms;
        _nextRoomRequest = TimeSpan.Zero;

        if (!ShowRooms)
            _rooms.Clear();
    }

    #endregion

    #region Drag

    private bool BeginDrag(EntityCoordinates coords, bool erasing)
    {
        if (!Active ||
            _drag != null ||
            (!erasing && Selected == null) ||
            !TryGetTile(coords, out var grid, out var tile))
            return false;

        _drag = (grid, tile, erasing);
        return true;
    }

    private bool EndDrag(EntityCoordinates coords)
    {
        if (_drag is not { } drag)
            return false;

        _drag = null;

        if (!TryGetTile(coords, out var grid, out var tile) || grid != drag.Grid)
            return true;

        var rect = RectBetween(drag.Tile, tile);
        var net = GetNetEntity(grid);

        if (drag.Erasing)
            RaiseNetworkEvent(new RequestZoneEraseEvent(net, rect));
        else if (Selected is { } zone)
            RaiseNetworkEvent(new RequestZoneRectEvent(net, rect, zone));

        return true;
    }

    /// <summary>
    /// Calculates the rectangle that encompasses two tile coordinates, ensuring that the rectangle is defined from the minimum to maximum coordinates and includes the end tiles.
    /// </summary>
    public static Box2i RectBetween(Vector2i a, Vector2i b)
        => new(
            Math.Min(a.X, b.X),
            Math.Min(a.Y, b.Y),
            Math.Max(a.X, b.X) + 1,
            Math.Max(a.Y, b.Y) + 1);

    private bool TryGetTile(EntityCoordinates coords, out EntityUid grid, out Vector2i tile)
    {
        grid = default;
        tile = default;

        if (_transform.GetGrid(coords) is not { } gridUid || !TryComp(gridUid, out MapGridComponent? gridComp))
            return false;

        grid = gridUid;
        tile = _maps.TileIndicesFor(gridUid, gridComp, coords);
        return true;
    }

    #endregion

    #region Overlay data

    /// <summary>
    /// Gets the current draw target for the zone placement overlay, including whether to draw, the grid entity, and the list of zone shapes for that grid.
    /// </summary>
    public (bool Draw, EntityUid Grid, List<ZoneShapeSet>? Shapes) GetDrawTarget()
    {
        if (!Active && !ShowZones)
            return (false, default, null);

        if (!TryGetHoveredGrid(out var grid))
        {
            if (_player.LocalEntity is not { } player ||
                _transform.GetGrid(player) is not { } playerGrid)
                return (false, default, null);

            grid = playerGrid;
        }

        _shapes.TryGetValue(GetNetEntity(grid), out var shapes);
        return (true, grid, shapes);
    }

    /// <summary>
    /// Attempts to get the preview for the zone placement overlay, including the grid entity, the rectangle to draw, and the color to use.
    /// </summary>
    public bool TryGetPreview(out EntityUid grid, out Box2i rect, out Color color)
    {
        grid = default;
        rect = default;
        color = Color.White;

        if (_drag is not { } drag ||
            !TryGetMouseTile(drag.Grid, out var tile))
            return false;

        grid = drag.Grid;
        rect = RectBetween(drag.Tile, tile);
        color = drag.Erasing ? Color.Red : ZoneColor(Selected);
        return true;
    }

    /// <summary>
    /// Gets the color for a zone based on its prototype.
    /// </summary>
    public Color ZoneColor(ProtoId<ZonePrototype>? zone)
        => zone is { } id && _proto.TryIndex(id, out var proto) ? proto.Color : Color.White;

    private bool TryGetHoveredGrid(out EntityUid grid)
    {
        grid = default;

        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);

        if (mouse.MapId == MapId.Nullspace ||
            !_maps.TryFindGridAt(mouse, out var gridUid, out _))
            return false;

        grid = gridUid;
        return true;
    }

    private bool TryGetMouseTile(EntityUid grid, out Vector2i tile)
    {
        tile = default;

        if (!HasComp<MapGridComponent>(grid))
            return false;

        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        var local = Vector2.Transform(mouse.Position, _transform.GetInvWorldMatrix(grid));

        tile = new Vector2i((int) MathF.Floor(local.X), (int) MathF.Floor(local.Y));
        return true;
    }

    private void RequestShapes(EntityUid grid)
    {
        var net = GetNetEntity(grid);

        if (!_requested.Add(net))
            return;

        RaiseNetworkEvent(new RequestZoneShapesEvent(net));
    }

    /// <summary>
    /// Gets the room view for the grid that the local player is currently on, if available. If the player is not on a grid or room data is not available, it returns null.
    /// </summary>
    public ZoneRoomView? GetRooms(out EntityUid grid)
    {
        grid = default;

        if (!ShowRooms ||
            _player.LocalEntity is not { } player ||
            _transform.GetGrid(player) is not { } playerGrid)
            return null;

        grid = playerGrid;
        return _rooms.GetValueOrDefault(GetNetEntity(playerGrid));
    }

    private void RequestRooms()
    {
        if (_timing.CurTime < _nextRoomRequest ||
            _player.LocalEntity is not { } player ||
            _transform.GetGrid(player) is not { } grid ||
            !TryComp(grid, out MapGridComponent? gridComp))
            return;

        _nextRoomRequest = _timing.CurTime + _roomRefresh;

        var tile = _maps.TileIndicesFor(grid, gridComp, Transform(player).Coordinates);
        RaiseNetworkEvent(new RequestZoneRoomsEvent(GetNetEntity(grid), tile));
    }

    [SubscribeNetworkEvent]
    private void OnRoomsSync(ZoneRoomsSyncEvent ev)
    {
        var view = new ZoneRoomView(ev.Zones);

        foreach (var chunk in ev.Chunks)
            view.Chunks[chunk.Origin] = chunk.Rooms;

        _rooms[ev.Grid] = view;
    }

    [SubscribeNetworkEvent]
    private void OnShapesSync(ZoneShapesSyncEvent ev)
    {
        _shapes[ev.Grid] = ev.Shapes;
        _requested.Add(ev.Grid);
    }

    /// <summary>
    /// Gets the known zone shapes for a specific grid entity, if available. If no shapes are known for the grid, it returns null.
    /// </summary>
    public IReadOnlyList<ZoneShapeSet>? GetKnownShapes(NetEntity grid)
        => _shapes.GetValueOrDefault(grid);

    #endregion
}

public sealed class ZoneRoomView(Dictionary<ushort, ProtoId<ZonePrototype>> zones)
{
    public readonly Dictionary<Vector2i, ushort[]> Chunks = [];

    public readonly Dictionary<ushort, ProtoId<ZonePrototype>> Zones = zones;

    public ushort RoomAt(Vector2i tile)
        => Chunks.TryGetValue(SharedZoneSystem.ChunkOrigin(tile), out var rooms)
            ? rooms[SharedZoneSystem.TileIndex(tile)]
            : SharedZoneSystem.NoRegion;
}
