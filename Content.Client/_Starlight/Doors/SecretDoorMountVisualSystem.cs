using System.Numerics;
using Content.Shared.Effects;
using Content.Shared.Doors.Components;
using Content.Shared._Starlight.Doors.Components;
using Content.Shared.Tag;
using Content.Shared.Wall;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Doors;

/// <summary>
/// Renders wall-mounted entities on an opening secret door as two clipped copies that
/// follow the door panels. The original entity remains in place for interaction.
/// </summary>
public sealed partial class SecretDoorMountVisualSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private TagSystem _tags = default!;

    private static readonly ProtoId<ShaderPrototype> LeftShader = "SecretDoorMountLeft";
    private static readonly ProtoId<ShaderPrototype> RightShader = "SecretDoorMountRight";
    private static readonly ProtoId<ShaderPrototype> LeftUnshadedShader = "SecretDoorMountLeftUnshaded";
    private static readonly ProtoId<ShaderPrototype> RightUnshadedShader = "SecretDoorMountRightUnshaded";
    private static readonly ProtoId<ShaderPrototype> TopShader = "SecretDoorMountTop";
    private static readonly ProtoId<ShaderPrototype> BottomShader = "SecretDoorMountBottom";
    private static readonly ProtoId<ShaderPrototype> TopUnshadedShader = "SecretDoorMountTopUnshaded";
    private static readonly ProtoId<ShaderPrototype> BottomUnshadedShader = "SecretDoorMountBottomUnshaded";
    private static readonly ProtoId<TagPrototype> HideContextMenuTag = "HideContextMenu";

    private readonly Dictionary<EntityUid, SplitMount> _mounts = new();
    private readonly Dictionary<EntityUid, float> _doorProgress = new();
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), EntityUid> _doorsByTile = new();
    private readonly HashSet<EntityUid> _activeDoors = new();
    private readonly HashSet<EntityUid> _verticalDoors = new();
    private readonly HashSet<EntityUid> _seenMounts = new();
    private readonly HashSet<EntityUid> _nearbyEntities = new();
    private readonly List<EntityUid> _removeMounts = new();
    private readonly List<EntityUid> _removeDoors = new();
    private float _scanCountdown;

    private sealed class SplitMount(EntityUid door, EntityUid left, EntityUid right, Color color, bool vertical, bool maskVertical, bool maskReversed)
    {
        public EntityUid Door = door;
        public EntityUid Left = left;
        public EntityUid Right = right;
        public Color OriginalColor = color;
        public bool Vertical = vertical;
        public bool MaskVertical = maskVertical;
        public bool MaskReversed = maskReversed;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SecretDoorPanelComponent, ComponentShutdown>(OnDoorShutdown);
        SubscribeLocalEvent<WallMountComponent, ComponentShutdown>(OnMountShutdown);
    }

    private void OnDoorShutdown(Entity<SecretDoorPanelComponent> ent, ref ComponentShutdown args)
    {
        _removeMounts.Clear();
        foreach (var (mount, split) in _mounts)
        {
            if (split.Door == ent.Owner)
                _removeMounts.Add(mount);
        }

        foreach (var mount in _removeMounts)
            RemoveMount(mount);
        _removeMounts.Clear();
        _activeDoors.Remove(ent.Owner);
        _doorProgress.Remove(ent.Owner);
    }

    private void OnMountShutdown(Entity<WallMountComponent> ent, ref ComponentShutdown args)
        => RemoveMount(ent.Owner);

    public override void FrameUpdate(float frameTime)
    {
        UpdateDoorProgress(frameTime);
        _scanCountdown -= frameTime;
        if (_scanCountdown <= 0f)
        {
            _scanCountdown = 0.1f;
            ScanMounts();
        }

        foreach (var (mount, split) in _mounts)
        {
            if (!HasComp<SecretDoorPanelComponent>(split.Door) ||
                !TryComp<DoorComponent>(split.Door, out var door) ||
                door.State == DoorState.Closed ||
                !_doorProgress.TryGetValue(split.Door, out var progress) ||
                !TryComp<SpriteComponent>(mount, out var sourceSprite) ||
                !TryComp(mount, out TransformComponent? sourceTransform) ||
                !TryComp<SpriteComponent>(split.Left, out var leftSprite) ||
                !TryComp<SpriteComponent>(split.Right, out var rightSprite))
            {
                _removeMounts.Add(mount);
                continue;
            }

            // Another visualizer may recolor the mount while the door is open.
            if (!HasComp<ColorFlashEffectComponent>(mount) &&
                sourceSprite.Color != split.OriginalColor.WithAlpha(0f))
                split.OriginalColor = sourceSprite.Color;

            var hiddenColor = split.OriginalColor.WithAlpha(0f);
            if (sourceSprite.Color != hiddenColor)
                _sprites.SetColor((mount, sourceSprite), hiddenColor);

            // Both halves retain the mount's normal position and rotation. Only
            // the sprite offsets move; the wall mount remains a single entity.
            _transforms.SetCoordinates(split.Left, sourceTransform.Coordinates);
            _transforms.SetCoordinates(split.Right, sourceTransform.Coordinates);
            if (Transform(split.Left).LocalRotation != sourceTransform.LocalRotation)
                _transforms.SetLocalRotationNoLerp(split.Left, sourceTransform.LocalRotation);
            if (Transform(split.Right).LocalRotation != sourceTransform.LocalRotation)
                _transforms.SetLocalRotationNoLerp(split.Right, sourceTransform.LocalRotation);

            var slide = 0.45f * progress;
            var direction = GetLocalSlideDirection(sourceTransform, sourceSprite, split.Vertical);
            var leftOffset = sourceSprite.Offset - (direction * slide);
            var rightOffset = sourceSprite.Offset + (direction * slide);
            if (leftSprite.Offset != leftOffset)
                _sprites.SetOffset((split.Left, leftSprite), leftOffset);
            if (rightSprite.Offset != rightOffset)
                _sprites.SetOffset((split.Right, rightSprite), rightOffset);
        }

        foreach (var mount in _removeMounts)
            RemoveMount(mount);
        _removeMounts.Clear();
    }

    private void UpdateDoorProgress(float frameTime)
    {
        foreach (var uid in _activeDoors)
        {
            if (!TryComp<DoorComponent>(uid, out var door))
                continue;

            var progress = _doorProgress.GetValueOrDefault(uid);
            _doorProgress[uid] = door.State switch
            {
                DoorState.Open => 1f,
                DoorState.Opening => MathF.Min(1f, progress + (frameTime / MathF.Max(0.001f, (float)door.OpeningAnimationTime.TotalSeconds))),
                DoorState.Closing => MathF.Max(0f, progress - (frameTime / MathF.Max(0.001f, (float)door.ClosingAnimationTime.TotalSeconds))),
                _ => progress,
            };
        }
    }

    private void ScanMounts()
    {
        _doorsByTile.Clear();
        _activeDoors.Clear();
        _verticalDoors.Clear();
        _removeDoors.Clear();

        var doorQuery = EntityQueryEnumerator<SecretDoorPanelComponent, DoorComponent, TransformComponent>();
        while (doorQuery.MoveNext(out var uid, out _, out var door, out var xform))
        {
            if (xform.GridUid is not { } grid || !HasComp<MapGridComponent>(grid))
                continue;

            if (door.State is not (DoorState.Open or DoorState.Opening or DoorState.Closing))
                continue;

            _doorProgress.TryAdd(uid, door.State is DoorState.Closing or DoorState.Open ? 1f : 0f);
            _activeDoors.Add(uid);
            if (MathF.Abs(xform.LocalRotation.RotateVec(Vector2.UnitX).Y) > 0.5f)
                _verticalDoors.Add(uid);
            _doorsByTile[(grid, _transforms.GetGridOrMapTilePosition(uid, xform))] = uid;
        }

        foreach (var door in _doorProgress.Keys)
        {
            if (!_activeDoors.Contains(door))
                _removeDoors.Add(door);
        }
        foreach (var door in _removeDoors)
            _doorProgress.Remove(door);

        // Most of the time there are no moving secret doors. Avoid scanning
        // every wall-mounted entity when there is nothing to split.
        if (_activeDoors.Count == 0)
        {
            _nearbyEntities.Clear();
            _removeMounts.Clear();
            _removeMounts.AddRange(_mounts.Keys);
            foreach (var mount in _removeMounts)
                RemoveMount(mount);
            _removeMounts.Clear();
            return;
        }

        _seenMounts.Clear();
        _nearbyEntities.Clear();
        foreach (var door in _activeDoors)
            _lookup.GetEntitiesInRange(door, 1.5f, _nearbyEntities, LookupFlags.Uncontained);

        foreach (var uid in _nearbyEntities)
        {
            if (!HasComp<WallMountComponent>(uid) ||
                !TryComp<SpriteComponent>(uid, out var sprite) ||
                !TryComp(uid, out TransformComponent? xform))
                continue;

            if (!sprite.Visible || (sprite.Color.A <= 0f && !_mounts.ContainsKey(uid)) ||
                xform.GridUid is not { } grid || !HasComp<MapGridComponent>(grid) ||
                !_doorsByTile.TryGetValue((grid, _transforms.GetGridOrMapTilePosition(uid, xform)), out var door))
                continue;

            _seenMounts.Add(uid);
            var localAxis = GetLocalSlideDirection(xform, sprite, _verticalDoors.Contains(door));
            var maskVertical = MathF.Abs(localAxis.Y) > 0.5f;
            var maskReversed = (maskVertical ? localAxis.Y : localAxis.X) < 0f;
            if (!_mounts.TryGetValue(uid, out var split))
            {
                split = CreateMount(uid, door, sprite, xform);
                _mounts.Add(uid, split);
            }
            else if (split.Door != door || split.Vertical != _verticalDoors.Contains(door) ||
                split.MaskVertical != maskVertical || split.MaskReversed != maskReversed)
            {
                RemoveMount(uid);
                split = CreateMount(uid, door, sprite, xform);
                _mounts.Add(uid, split);
            }

            if (!HasComp<ColorFlashEffectComponent>(uid) &&
                sprite.Color != split.OriginalColor.WithAlpha(0f))
                split.OriginalColor = sprite.Color;
            RefreshMount(uid, sprite, split);
        }

        _removeMounts.Clear();
        foreach (var mount in _mounts.Keys)
        {
            if (!_seenMounts.Contains(mount))
                _removeMounts.Add(mount);
        }
        foreach (var mount in _removeMounts)
            RemoveMount(mount);
        _removeMounts.Clear();
    }

    private SplitMount CreateMount(EntityUid mount, EntityUid door, SpriteComponent sprite, TransformComponent xform)
    {
        var left = Spawn("clientsideclone", xform.Coordinates);
        var right = Spawn("clientsideclone", xform.Coordinates);
        _tags.AddTag(left, HideContextMenuTag);
        _tags.AddTag(right, HideContextMenuTag);
        var vertical = _verticalDoors.Contains(door);
        var localAxis = GetLocalSlideDirection(xform, sprite, vertical);
        var maskVertical = MathF.Abs(localAxis.Y) > 0.5f;
        var maskReversed = (maskVertical ? localAxis.Y : localAxis.X) < 0f;
        var originalColor = TryComp<ColorFlashEffectComponent>(mount, out var flash) ? flash.Color : sprite.Color;
        return new SplitMount(door, left, right, originalColor, vertical, maskVertical, maskReversed);
    }

    private static Vector2 GetLocalSlideDirection(TransformComponent xform, SpriteComponent sprite, bool vertical)
    {
        var worldAxis = vertical ? Vector2.UnitY : Vector2.UnitX;
        return sprite.NoRotation ? worldAxis : (-xform.LocalRotation).RotateVec(worldAxis);
    }

    private void RefreshMount(EntityUid mount, SpriteComponent source, SplitMount split)
    {
        if (!TryComp<SpriteComponent>(split.Left, out var left) ||
            !TryComp<SpriteComponent>(split.Right, out var right))
            return;

        _sprites.CopySprite((mount, source), (split.Left, left));
        _sprites.CopySprite((mount, source), (split.Right, right));
        _sprites.SetColor((split.Left, left), split.OriginalColor);
        _sprites.SetColor((split.Right, right), split.OriginalColor);
        _sprites.SetColor((mount, source), split.OriginalColor.WithAlpha(0f));

        MaskHalf(left, firstHalf: true, split.MaskVertical, split.MaskReversed);
        MaskHalf(right, firstHalf: false, split.MaskVertical, split.MaskReversed);
    }

    private void MaskHalf(SpriteComponent sprite, bool firstHalf, bool vertical, bool reversed)
    {
        var index = 0;
        foreach (var layer in sprite.AllLayers)
        {
            if (layer is SpriteComponent.Layer renderLayer)
            {
                var unshaded = renderLayer.ShaderPrototype == SpriteSystem.UnshadedId;
                var lowSide = firstHalf != reversed;
                var shader = vertical
                    ? lowSide ? unshaded ? BottomUnshadedShader : BottomShader : unshaded ? TopUnshadedShader : TopShader
                    : lowSide ? unshaded ? LeftUnshadedShader : LeftShader : unshaded ? RightUnshadedShader : RightShader;
                sprite.LayerSetShader(index, _prototypes.Index(shader).Instance(), shader);
            }
            index++;
        }
    }

    private void RemoveMount(EntityUid mount)
    {
        if (!_mounts.Remove(mount, out var split))
            return;

        if (TryComp<ColorFlashEffectComponent>(mount, out var flash))
            flash.Color = split.OriginalColor;

        if (TryComp<SpriteComponent>(mount, out var sprite))
            _sprites.SetColor((mount, sprite), split.OriginalColor);

        QueueDel(split.Left);
        QueueDel(split.Right);
    }
}
