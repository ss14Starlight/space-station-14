using System.Diagnostics.CodeAnalysis;
using Content.Shared.Tabletop.Components;
using Content.Shared.Tabletop.Events;
using Robust.Client.Timing;
using Robust.Shared.Map;
using Robust.Shared.Timing;

// ReSharper disable CheckNamespace
namespace Content.Client.Tabletop;

public sealed partial class TabletopSystem
{
    [Dependency] private IClientGameTiming _clientTiming = default!;

    private EntityUid? _camera;
    private bool _draggingWithoutTable;
    private MapCoordinates _lastSentCoords = MapCoordinates.Nullspace;

    private (EntityUid Entity, MapCoordinates Coords, GameTick Tick)? _held;

    private NetEntity GetDragTable() =>
        _draggingWithoutTable || _table == null ? NetEntity.Invalid : GetNetEntity(_table.Value);

    private bool IsOnTable(EntityUid entity)
    {
        if (_window == null || !TryComp(_camera, out TransformComponent? cameraXform))
            return false;

        return TryComp(entity, out TransformComponent? xform) && xform.MapID == cameraXform.MapID;
    }

    public bool IsInDragRange(EntityUid player, EntityUid entity)
    {
        if (IsOnTable(entity))
            return CanSeeTable(player, _table) && CanDrag(player, entity, out _);

        return CanDragWithoutTable(player, entity, out _);
    }

    private MapCoordinates GetCursorCoordinates() =>
        _viewport == null
            ? MapCoordinates.Nullspace
            : ClampPositionToViewport(_viewport.PixelToMap(_inputManager.MouseScreenPosition.Position), _viewport);

    private void UpdateDragPosition()
    {
        if (_held is { } held)
        {
            if (Deleted(held.Entity) || _clientTiming.LastRealTick >= held.Tick ||
                Transform(held.Entity).MapID != held.Coords.MapId)
                _held = null;
            else
                _transformSystem.SetWorldPosition(held.Entity, held.Coords.Position);
        }

        if (_draggedEntity is not { } dragged)
            return;

        var coords = GetCursorCoordinates();
        if (coords.Equals(MapCoordinates.Nullspace) || coords.MapId != Transform(dragged).MapID)
            return;

        _transformSystem.SetWorldPosition(dragged, coords.Position);
    }

    private MapCoordinates GetDropCoordinates(EntityUid dragged)
    {
        var current = Transforms.GetMapCoordinates(dragged);
        var coords = GetCursorCoordinates();
        return coords.Equals(MapCoordinates.Nullspace) || coords.MapId != current.MapId ? current : coords;
    }

    private void HoldDroppedEntity(EntityUid dragged, MapCoordinates coords) =>
        _held = (dragged, coords, _gameTiming.CurTick);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_gameTiming.IsFirstTimePredicted || _draggedEntity is not { } dragged || _viewport == null)
            return;

        if (_playerManager.LocalEntity is not { } playerEntity ||
            !CanKeepDragging(playerEntity, dragged, out var draggable))
        {
            StopDragging();
            return;
        }

        if (draggable.DraggingPlayer != null && draggable.DraggingPlayer != _playerManager.LocalSession!.UserId)
        {
            StopDragging(false);
            return;
        }

        var coords = GetCursorCoordinates();
        if (coords.Equals(MapCoordinates.Nullspace) || coords.Equals(_lastSentCoords))
            return;

        _lastSentCoords = coords;
        RaisePredictiveEvent(new TabletopMoveEvent(GetNetEntity(dragged), coords, GetDragTable()));
    }

    private bool CanKeepDragging(EntityUid player, EntityUid dragged,
        [NotNullWhen(true)] out TabletopDraggableComponent? draggable)
    {
        if (_draggingWithoutTable)
            return CanDragWithoutTable(player, dragged, out draggable);

        draggable = null;
        return _table != null && CanSeeTable(player, _table) && CanDrag(player, dragged, out draggable);
    }
}
