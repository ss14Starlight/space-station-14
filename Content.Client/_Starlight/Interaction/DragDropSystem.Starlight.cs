using System.Collections.Generic;
using System.Numerics;
using Content.Client.Graphics;
using Content.Shared.CombatMode;
using Content.Shared.DragDrop;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

// ReSharper disable CheckNamespace
namespace Content.Client.Interaction;

public sealed partial class DragDropSystem
{
    private const float RemoteMaxMouseDownTimeForReplayingClick = 0.85f;

    private EntityUid? _remoteDragUser;
    private EntityUid? _remoteDraggedEntity;
    private EntityUid? _remoteDragShadow;
    private Vector2? _remoteDragStartPosition;
    private float _remoteMouseDownTime;
    private float _remoteTargetRecheckTime;
    private bool _remoteDragging;
    private readonly HashSet<SpriteComponent> _remoteHighlightedSprites = new();
    private readonly HashSet<SpriteComponent> _nextRemoteHighlightedSprites = new();

    public bool TryStartRemoteDrag(EntityUid user, EntityUid? draggedEntity, Vector2 screenPosition)
    {
        CancelRemoteDrag();

        if (!Exists(user)
            || draggedEntity is not { } dragged
            || !Exists(dragged)
            || (TryComp<CombatModeComponent>(user, out var combatMode) && combatMode.IsInCombatMode)
            || !_interactionSystem.InRangeUnobstructed(user, dragged))
            return false;

        var ev = new CanDragEvent();
        RaiseLocalEvent(dragged, ref ev);

        if (!ev.Handled)
            return false;

        _remoteDragUser = user;
        _remoteDraggedEntity = dragged;
        _remoteDragStartPosition = screenPosition;
        _remoteMouseDownTime = 0f;
        return true;
    }

    public bool UpdateRemoteDrag(EntityUid user, Vector2 screenPosition, MapCoordinates mapPosition, float frameTime)
    {
        if (_remoteDragUser != user
            || _remoteDraggedEntity is not { } dragged
            || !Exists(user)
            || !Exists(dragged))
        {
            CancelRemoteDrag();
            return false;
        }

        if (!_remoteDragging)
        {
            _remoteMouseDownTime += frameTime;
            if (_remoteDragStartPosition is { } startPosition
                && (startPosition - screenPosition).Length() > Deadzone)
                StartRemoteDrag(mapPosition);
        }

        if (_remoteDragging)
        {
            if (_remoteDragShadow is { } shadow
                && Exists(shadow)
                && mapPosition.MapId != MapId.Nullspace)
                _transformSystem.SetWorldPosition(shadow, mapPosition.Position);

            _remoteTargetRecheckTime += frameTime;
            if (_remoteTargetRecheckTime > TargetRecheckInterval)
            {
                HighlightRemoteTargets(user, dragged, mapPosition);
                _remoteTargetRecheckTime -= TargetRecheckInterval;
            }
        }

        return true;
    }

    public bool TryFinishRemoteDrag(
        EntityUid user,
        Vector2 screenPosition,
        MapCoordinates mapPosition,
        IEnumerable<EntityUid> targetEntities,
        out EntityUid dragged,
        out EntityUid? dropTarget,
        out bool wasDragging,
        out bool replayClick)
    {
        dragged = default;
        dropTarget = null;
        wasDragging = false;
        replayClick = false;

        if (_remoteDraggedEntity is not { } draggedEntity)
            return false;

        dragged = draggedEntity;

        if (_remoteDragUser != user || !Exists(user) || !Exists(draggedEntity))
        {
            CancelRemoteDrag();
            return true;
        }

        if (!_remoteDragging
            && _remoteDragStartPosition is { } startPosition
            && (startPosition - screenPosition).Length() > Deadzone)
            StartRemoteDrag(mapPosition);

        wasDragging = _remoteDragging;
        if (!wasDragging)
        {
            replayClick = _remoteMouseDownTime < RemoteMaxMouseDownTimeForReplayingClick;
            CancelRemoteDrag();
            return true;
        }

        var outOfRange = false;
        foreach (var entity in targetEntities)
        {
            if (entity == draggedEntity)
                continue;

            var valid = ValidDragDrop(user, draggedEntity, entity);
            if (valid != true)
                continue;

            if (!_interactionSystem.InRangeUnobstructed(user, entity)
                || !_interactionSystem.InRangeUnobstructed(user, draggedEntity))
            {
                outOfRange = true;
                continue;
            }

            dropTarget = entity;
            break;
        }

        if (dropTarget is null && outOfRange)
            _popup.PopupEntity(Loc.GetString("drag-drop-system-out-of-range-text"), draggedEntity, Filter.Local(), true);

        CancelRemoteDrag();
        return true;
    }

    public void CancelRemoteDrag()
    {
        var hadRemoteDrag = _remoteDraggedEntity is not null;

        if (_remoteDragShadow is { } shadow && Exists(shadow))
            Del(shadow);

        _remoteDragUser = null;
        _remoteDraggedEntity = null;
        _remoteDragShadow = null;
        _remoteDragStartPosition = null;
        _remoteMouseDownTime = 0f;
        _remoteTargetRecheckTime = 0f;
        _remoteDragging = false;
        RemoveRemoteHighlights();

        if (hadRemoteDrag)
            _outline.SetEnabled(true);
    }

    private void StartRemoteDrag(MapCoordinates mapPosition)
    {
        if (_remoteDragUser is not { } user || _remoteDraggedEntity is not { } dragged)
            return;

        _remoteDragging = true;
        _remoteTargetRecheckTime = 0f;
        _outline.SetEnabled(false);
        HighlightRemoteTargets(user, dragged, mapPosition);

        if (mapPosition.MapId == MapId.Nullspace)
            return;

        if (!_spriteQuery.TryGetComponent(dragged, out var draggedSprite))
        {
            Log.Warning($"Dragged entity {ToPrettyString(dragged)} has no sprite!");
            return;
        }

        _remoteDragShadow = EntityManager.SpawnEntity("dragshadow", mapPosition);
        var dragSprite = Comp<SpriteComponent>(_remoteDragShadow.Value);
        _sprite.CopySprite((dragged, draggedSprite), (_remoteDragShadow.Value, dragSprite));
        dragSprite.RenderOrder = EntityManager.CurrentTick.Value;
        _sprite.SetColor((_remoteDragShadow.Value, dragSprite), dragSprite.Color.WithAlpha(0.7f));
        _sprite.SetDrawDepth((_remoteDragShadow.Value, dragSprite), (int)DrawDepth.Overlays);
        if (!dragSprite.NoRotation)
            _transformSystem.SetWorldRotationNoLerp(_remoteDragShadow.Value, Transform(dragged).WorldRotation);
    }

    private void HighlightRemoteTargets(EntityUid user, EntityUid dragged, MapCoordinates mapPosition)
    {
        if (mapPosition.MapId == MapId.Nullspace)
        {
            RemoveRemoteHighlights();
            return;
        }

        _nextRemoteHighlightedSprites.Clear();

        var expansion = new Vector2(1.5f, 1.5f);
        var bounds = new Box2(mapPosition.Position - expansion, mapPosition.Position + expansion);
        var pvsEntities = _lookup.GetEntitiesIntersecting(mapPosition.MapId, bounds);

        foreach (var entity in pvsEntities)
        {
            if (!_spriteQuery.TryGetComponent(entity, out var targetSprite)
                || !targetSprite.Visible
                || entity == dragged)
                continue;

            var valid = ValidDragDrop(user, dragged, entity);
            if (valid is null)
                continue;

            if (valid.Value)
            {
                valid = _interactionSystem.InRangeUnobstructed(user, dragged)
                        && _interactionSystem.InRangeUnobstructed(user, entity);
            }

            SetDragDropPostShader(
                (entity, targetSprite),
                valid.Value ? _dropTargetInRangeShader! : _dropTargetOutOfRangeShader!);
            targetSprite.RenderOrder = EntityManager.CurrentTick.Value;
            _nextRemoteHighlightedSprites.Add(targetSprite);
        }

        foreach (var highlightedSprite in _remoteHighlightedSprites)
        {
            if (_nextRemoteHighlightedSprites.Contains(highlightedSprite))
                continue;

            _sprite.RemovePostShader(highlightedSprite, ContentPostShaderIds.DragDropOutline);
            highlightedSprite.RenderOrder = 0;
        }

        _remoteHighlightedSprites.Clear();
        foreach (var highlightedSprite in _nextRemoteHighlightedSprites)
            _remoteHighlightedSprites.Add(highlightedSprite);
    }

    private void RemoveRemoteHighlights()
    {
        foreach (var highlightedSprite in _remoteHighlightedSprites)
        {
            _sprite.RemovePostShader(highlightedSprite, ContentPostShaderIds.DragDropOutline);
            highlightedSprite.RenderOrder = 0;
        }

        _remoteHighlightedSprites.Clear();
        _nextRemoteHighlightedSprites.Clear();
    }
}
