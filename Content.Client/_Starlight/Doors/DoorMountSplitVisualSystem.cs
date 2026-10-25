using System;
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
/// Renders wall mounts on explicitly opted-in doors as two clipped copies that
/// follow the door panels. The original entity remains in place for interaction.
/// </summary>
public sealed partial class DoorMountSplitVisualSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

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
    private readonly Dictionary<EntityUid, DoorState> _activeDoors = new();
    private readonly HashSet<EntityUid> _verticalDoors = new();
    private readonly HashSet<EntityUid> _seenMounts = new();
    private readonly HashSet<EntityUid> _nearbyEntities = new();
    private readonly List<EntityUid> _removeMounts = new();
    private readonly List<EntityUid> _removeDoors = new();
    private float _scanCountdown;

    // First/Second are the negative/positive slide directions. Depending on
    // door rotation they can appear as left/right or bottom/top on screen.
    private sealed class SplitMount(EntityUid door, EntityUid first, EntityUid second, Color color, bool vertical, bool maskVertical, bool maskReversed)
    {
        public EntityUid Door = door;
        public EntityUid First = first;
        public EntityUid Second = second;
        public Color OriginalColor = color;
        public bool Vertical = vertical;
        public bool MaskVertical = maskVertical;
        public bool MaskReversed = maskReversed;
        public int VisualHash;
        public float RefreshCountdown;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DoorMountSplitComponent, ComponentStartup>(OnDoorStartup);
        SubscribeLocalEvent<DoorMountSplitComponent, AppearanceChangeEvent>(OnDoorAppearanceChange);
        SubscribeLocalEvent<DoorMountSplitComponent, ComponentShutdown>(OnDoorShutdown);
        SubscribeLocalEvent<WallMountComponent, ComponentShutdown>(OnMountShutdown);
    }

    private void OnDoorStartup(Entity<DoorMountSplitComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<DoorComponent>(ent, out var door))
            SetDoorState(ent.Owner, door.State);
    }

    private void OnDoorAppearanceChange(Entity<DoorMountSplitComponent> ent, ref AppearanceChangeEvent args)
    {
        if (_appearance.TryGetData<DoorState>(ent, DoorVisuals.State, out var state, args.Component))
            SetDoorState(ent.Owner, state);
    }

    private void SetDoorState(EntityUid uid, DoorState state)
    {
        if (state is DoorState.Open or DoorState.Opening or DoorState.Closing)
        {
            if (_activeDoors.TryGetValue(uid, out var previous) && previous == state)
                return;

            _activeDoors[uid] = state;
            _doorProgress.TryAdd(uid, state is DoorState.Open or DoorState.Closing ? 1f : 0f);
        }
        else if (!_activeDoors.Remove(uid))
        {
            return;
        }
        else
        {
            _doorProgress.Remove(uid);
        }

        _scanCountdown = 0f;
    }

    private void OnDoorShutdown(Entity<DoorMountSplitComponent> ent, ref ComponentShutdown args)
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
        if (_activeDoors.Count == 0 && _mounts.Count == 0)
            return;

        UpdateDoorProgress(frameTime);
        _scanCountdown -= frameTime;
        if (_scanCountdown <= 0f)
        {
            _scanCountdown = 0.1f;
            ScanMounts();
        }

        foreach (var (mount, split) in _mounts)
        {
            if (!TryComp<DoorMountSplitComponent>(split.Door, out var panel) ||
                !HasComp<DoorComponent>(split.Door) ||
                !_activeDoors.ContainsKey(split.Door) ||
                !_doorProgress.TryGetValue(split.Door, out var progress) ||
                !TryComp<SpriteComponent>(mount, out var sourceSprite) ||
                !TryComp(mount, out TransformComponent? sourceTransform) ||
                !TryComp<SpriteComponent>(split.First, out var firstSprite) ||
                !TryComp<SpriteComponent>(split.Second, out var secondSprite))
            {
                _removeMounts.Add(mount);
                continue;
            }

            TrackSourceColor(mount, sourceSprite, split);

            var hiddenColor = split.OriginalColor.WithAlpha(0f);
            if (sourceSprite.Color != hiddenColor)
                _sprites.SetColor((mount, sourceSprite), hiddenColor);

            _transforms.SetCoordinates(split.First, sourceTransform.Coordinates);
            _transforms.SetCoordinates(split.Second, sourceTransform.Coordinates);
            if (Transform(split.First).LocalRotation != sourceTransform.LocalRotation)
                _transforms.SetLocalRotationNoLerp(split.First, sourceTransform.LocalRotation);
            if (Transform(split.Second).LocalRotation != sourceTransform.LocalRotation)
                _transforms.SetLocalRotationNoLerp(split.Second, sourceTransform.LocalRotation);

            var slide = MathF.Max(0f, panel.SlideDistance) * progress;
            var direction = GetLocalSlideDirection(sourceTransform, sourceSprite, split.Vertical);
            var firstOffset = sourceSprite.Offset - (direction * slide);
            var secondOffset = sourceSprite.Offset + (direction * slide);
            if (firstSprite.Offset != firstOffset)
                _sprites.SetOffset((split.First, firstSprite), firstOffset);
            if (secondSprite.Offset != secondOffset)
                _sprites.SetOffset((split.Second, secondSprite), secondOffset);
        }

        foreach (var mount in _removeMounts)
            RemoveMount(mount);
        _removeMounts.Clear();
    }

    private void UpdateDoorProgress(float frameTime)
    {
        foreach (var (uid, state) in _activeDoors)
        {
            if (!TryComp<DoorComponent>(uid, out var door))
                continue;

            var progress = _doorProgress.GetValueOrDefault(uid);
            _doorProgress[uid] = state switch
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
        _verticalDoors.Clear();
        _removeDoors.Clear();

        foreach (var (uid, _) in _activeDoors)
        {
            if (!HasComp<DoorMountSplitComponent>(uid) ||
                !HasComp<DoorComponent>(uid) ||
                !TryComp(uid, out TransformComponent? xform))
            {
                _removeDoors.Add(uid);
                continue;
            }

            if (xform.GridUid is not { } grid || !HasComp<MapGridComponent>(grid))
                continue;

            if (MathF.Abs(xform.LocalRotation.RotateVec(Vector2.UnitX).Y) > 0.5f)
                _verticalDoors.Add(uid);
            _doorsByTile[(grid, _transforms.GetGridOrMapTilePosition(uid, xform))] = uid;
        }

        foreach (var door in _removeDoors)
        {
            _activeDoors.Remove(door);
            _doorProgress.Remove(door);
        }

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
        foreach (var door in _activeDoors.Keys)
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
            // Door orientation is in world space, but the clipping shader runs
            // in sprite-local space. A rotated mount can reverse either axis.
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

            TrackSourceColor(uid, sprite, split);
            // Avoid full sprite copies unless visuals change; the fallback
            // catches properties not included in the hash.
            var visualHash = GetVisualHash(sprite, split.OriginalColor);
            split.RefreshCountdown -= 0.1f;
            if (visualHash != split.VisualHash || split.RefreshCountdown <= 0f)
            {
                RefreshMount(uid, sprite, split);
                split.VisualHash = visualHash;
                split.RefreshCountdown = 1f;
            }
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
        var first = Spawn("clientsideclone", xform.Coordinates);
        var second = Spawn("clientsideclone", xform.Coordinates);
        _tags.AddTag(first, HideContextMenuTag);
        _tags.AddTag(second, HideContextMenuTag);
        var vertical = _verticalDoors.Contains(door);
        var localAxis = GetLocalSlideDirection(xform, sprite, vertical);
        var maskVertical = MathF.Abs(localAxis.Y) > 0.5f;
        var maskReversed = (maskVertical ? localAxis.Y : localAxis.X) < 0f;
        var originalColor = TryComp<ColorFlashEffectComponent>(mount, out var flash) ? flash.Color : sprite.Color;
        return new SplitMount(door, first, second, originalColor, vertical, maskVertical, maskReversed);
    }

    /// <summary>
    /// Converts the door's world-space slide axis into the mount sprite's local
    /// space so a rotated mount is clipped along the correct axis.
    /// </summary>
    private static Vector2 GetLocalSlideDirection(TransformComponent xform, SpriteComponent sprite, bool vertical)
    {
        var worldAxis = vertical ? Vector2.UnitY : Vector2.UnitX;
        return sprite.NoRotation ? worldAxis : (-xform.LocalRotation).RotateVec(worldAxis);
    }

    private void RefreshMount(EntityUid mount, SpriteComponent source, SplitMount split)
    {
        if (!TryComp<SpriteComponent>(split.First, out var first) ||
            !TryComp<SpriteComponent>(split.Second, out var second))
            return;

        _sprites.CopySprite((mount, source), (split.First, first));
        _sprites.CopySprite((mount, source), (split.Second, second));
        _sprites.SetColor((split.First, first), split.OriginalColor);
        _sprites.SetColor((split.Second, second), split.OriginalColor);
        _sprites.SetColor((mount, source), split.OriginalColor.WithAlpha(0f));

        MaskHalf(first, firstHalf: true, split.MaskVertical, split.MaskReversed);
        MaskHalf(second, firstHalf: false, split.MaskVertical, split.MaskReversed);
    }

    /// <summary>
    /// Selects the shaded or unshaded clipping shader for each sprite layer,
    /// accounting for the mount's orientation relative to the door.
    /// </summary>
    private void MaskHalf(SpriteComponent sprite, bool firstHalf, bool vertical, bool reversed)
    {
        var index = 0;
        foreach (var layer in sprite.AllLayers)
        {
            if (layer is SpriteComponent.Layer renderLayer)
            {
                var unshaded = renderLayer.ShaderPrototype == SpriteSystem.UnshadedId;
                var lowSide = firstHalf != reversed;
                var shader = (vertical, lowSide, unshaded) switch
                {
                    (true, true, true) => BottomUnshadedShader,
                    (true, true, false) => BottomShader,
                    (true, false, true) => TopUnshadedShader,
                    (true, false, false) => TopShader,
                    (false, true, true) => LeftUnshadedShader,
                    (false, true, false) => LeftShader,
                    (false, false, true) => RightUnshadedShader,
                    _ => RightShader,
                };
                sprite.LayerSetShader(index, _prototypes.Index(shader).Instance(), shader);
            }
            index++;
        }
    }

    /// <summary>
    /// Restores the original mount's color before removing its client-side
    /// halves, including any color managed by an active flash effect.
    /// </summary>
    private void RemoveMount(EntityUid mount)
    {
        if (!_mounts.Remove(mount, out var split))
            return;

        if (TryComp<ColorFlashEffectComponent>(mount, out var flash))
            flash.Color = split.OriginalColor;

        if (TryComp<SpriteComponent>(mount, out var sprite))
            _sprites.SetColor((mount, sprite), split.OriginalColor);

        QueueDel(split.First);
        QueueDel(split.Second);
    }

    /// <summary>
    /// Saves persistent color changes while excluding temporary flash colors
    /// and the transparent color used to hide the original sprite.
    /// </summary>
    private void TrackSourceColor(EntityUid mount, SpriteComponent sprite, SplitMount split)
    {
        if (!HasComp<ColorFlashEffectComponent>(mount) &&
            sprite.Color != split.OriginalColor.WithAlpha(0f))
            split.OriginalColor = sprite.Color;
    }

    /// <summary>
    /// Fingerprints common sprite properties to avoid full copies on every
    /// scan; the timed refresh covers properties omitted from this hash.
    /// </summary>
    private static int GetVisualHash(SpriteComponent sprite, Color originalColor)
    {
        var hash = new HashCode();
        hash.Add(originalColor);
        hash.Add(sprite.Visible);
        hash.Add(sprite.BaseRSI);
        hash.Add(sprite.DrawDepth);
        hash.Add(sprite.Scale);
        hash.Add(sprite.Rotation);
        hash.Add(sprite.Offset);
        hash.Add(sprite.NoRotation);
        hash.Add(sprite.DirectionOverride);
        hash.Add(sprite.EnableDirectionOverride);
        hash.Add(sprite.RenderOrder);
        hash.Add(sprite.GranularLayersRendering);
        hash.Add(sprite.Loop);
        foreach (var layer in sprite.AllLayers)
        {
            if (layer is not SpriteComponent.Layer renderLayer)
                continue;

            hash.Add(renderLayer.RSI);
            hash.Add(renderLayer.State);
            hash.Add(renderLayer.Texture);
            hash.Add(renderLayer.Visible);
            hash.Add(renderLayer.Color);
            hash.Add(renderLayer.Offset);
            hash.Add(renderLayer.Scale);
            hash.Add(renderLayer.Rotation);
            hash.Add(renderLayer.ShaderPrototype);
            hash.Add(renderLayer.AutoAnimated);
            hash.Add(renderLayer.DirOffset);
            hash.Add(renderLayer.RenderingStrategy);
        }

        return hash.ToHashCode();
    }
}
