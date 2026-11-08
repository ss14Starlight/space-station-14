using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Client.Gravity;
using Content.Client._Starlight.Humanoid;
using Content.Client.IconSmoothing;
using Content.Client.MapText;
using Content.Shared._Starlight.Snapshots;
using Content.Shared.Buckle.Components;
using Content.Shared.Gravity;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Mind.Components;
using Content.Shared.Parallax;
using Content.Shared.SSDIndicator;
using Content.Shared.Sprite;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Snapshots;

/// <summary>
/// Rebuilds a snapshot on a client-only map so it can be looked at through a fixed eye.
/// The map exists only on this client <see cref="SnapshotViewBoundUserInterface"/> and deletes it when the view is closed.
/// </summary>
public sealed partial class ClientSnapshotViewSystem : EntitySystem
{
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedRoofSystem _roof = default!;
    [Dependency] private HumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private SharedScaleVisualsSystem _scaleVisuals = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private FloatingVisualizerSystem _floating = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private EntityQuery<StrapComponent> _strapQuery = default!;
    [Dependency] private EntityQuery<SpriteComponent> _spriteQuery = default!;
    [Dependency] private EntityQuery<MapTextComponent> _mapTextQuery = default!;

    private static readonly Vector2 _center = new(0.5f, 0.5f);

    private static readonly TimeSpan _requestRetryDelay = TimeSpan.FromSeconds(0.2);
    private const int MaxRequests = 25;

    private readonly List<(SnapshotViewBoundUserInterface Ui, SnapshotData Data)> _pendingBuilds = [];

    private readonly Dictionary<SnapshotViewBoundUserInterface, (TimeSpan NextRequest, int Requests)> _awaiting = [];

    private readonly Dictionary<NetEntity, CachedScene> _scenes = [];

    /// <summary>
    /// Snapshots this player has held or opened, so opening them again doesn't wait on the server.
    /// </summary>
    /// <remarks>
    /// Keyed by net id: a freshly printed snapshot's data can arrive before the entity itself does.
    /// </remarks>
    private readonly Dictionary<NetEntity, (SnapshotData Data, TimeSpan Received)> _known = [];

    private static readonly TimeSpan _knownSnapshotLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan _knownPruneInterval = TimeSpan.FromMinutes(1);
    private TimeSpan _lastKnownPrune = TimeSpan.FromMinutes(-1);
    private const int MaxCachedScenes = 8;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new SnapshotViewShadowsOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<SnapshotViewShadowsOverlay>();
        foreach (var scene in _scenes.Values)
        {
            Destroy(scene.Map);
        }

        _scenes.Clear();
    }

    [SubscribeNetworkEvent]
    private void OnSnapshotData(SnapshotViewDataEvent ev)
    {
        _known[ev.Snapshot] = (ev.Data, _timing.RealTime);

        if (TryGetEntity(ev.Snapshot, out var snapshot)
            && _ui.TryGetOpenUi<SnapshotViewBoundUserInterface>(snapshot.Value, SnapshotViewUiKey.Key, out var ui))
        {
            ui.SetSnapshot(ev.Data);
        }
    }

    public bool TryGetKnownSnapshot(EntityUid snapshot, [NotNullWhen(true)] out SnapshotData? data)
    {
        data = null;
        if (!_known.TryGetValue(GetNetEntity(snapshot), out var known))
            return false;

        data = known.Data;
        return true;
    }

    public (EntityUid Map, IEye Eye, Vector2i ViewportSize) BuildScene(SnapshotData data)
        => Build(data);

    /// <summary>
    /// Queues <paramref name="data"/> to build a scene for <paramref name="ui"/> on the next frame.
    /// </summary>
    public void QueueBuild(SnapshotViewBoundUserInterface ui, SnapshotData data)
        => _pendingBuilds.Add((ui, data));

    /// <summary>
    /// Asks the server again until <paramref name="ui"/> has its snapshot, closes, or runs out of attempts.
    /// </summary>
    public void AwaitSnapshot(SnapshotViewBoundUserInterface ui)
        => _awaiting[ui] = (_timing.RealTime + _requestRetryDelay, 1);

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        RetrySnapshotRequests();

        if (_pendingBuilds.Count == 0)
            return;

        // Take the queue first so a failing Build can't be retried (reopening windows) every frame.
        var builds = _pendingBuilds.ToArray();
        _pendingBuilds.Clear();

        foreach (var (ui, data) in builds)
        {
            if (ui.Disposed)
                continue;

            var snapshot = GetNetEntity(ui.Owner);
            if (!_scenes.TryGetValue(snapshot, out var scene))
            {
                var (map, eye, size) = Build(data);
                scene = new CachedScene(map, eye, size, _timing.RealTime);
                _scenes.Add(snapshot, scene);
            }

            scene.OpenViews++;
            scene.LastAccess = _timing.RealTime;
            ui.ShowScene(snapshot, scene.Eye, scene.ViewportSize);
        }

        TrimSceneCache();
    }

    private void RetrySnapshotRequests()
    {
        var now = _timing.RealTime;
        if (_known.Count > 0 && now - _lastKnownPrune >= _knownPruneInterval)
        {
            _lastKnownPrune = now;
            foreach (var (snapshot, known) in _known.ToArray())
            {
                if (now - known.Received > _knownSnapshotLifetime)
                    _known.Remove(snapshot);
            }
        }

        if (_awaiting.Count == 0)
            return;

        foreach (var (ui, (nextRequest, requests)) in _awaiting.ToArray())
        {
            if (ui.Snapshot != null || ui.Disposed || requests >= MaxRequests)
            {
                _awaiting.Remove(ui);
                continue;
            }

            if (now < nextRequest)
                continue;

            ui.RequestSnapshot();
            _awaiting[ui] = (now + _requestRetryDelay, requests + 1);
        }
    }

    private (EntityUid Map, IEye Eye, Vector2i ViewportSize) Build(SnapshotData snapshot)
    {
        var mapUid = _map.CreateMap(out var mapId);
        _map.SetPaused(mapId, true);
        _map.SetAmbientLight(mapId, snapshot.AmbientLight);
        if (snapshot.Parallax != null)
            EnsureComp<ParallaxComponent>(mapUid).Parallax = snapshot.Parallax;

        // Built where the shot was taken, so the space background lines up with what was captured.
        var grid = _map.CreateGridEntity(mapId);
        _transform.SetWorldPosition(grid.Owner, snapshot.WorldCenter - _center);
        var shadows = EnsureComp<SnapshotViewShadowsComponent>(grid);
        shadows.Tiles = snapshot.Hidden;
        shadows.Shadows = BuildShadows(_center + snapshot.Viewer, snapshot.Occluders);
        var tiles = new List<(Vector2i, Tile)>(snapshot.Tiles.Count);
        RoofComponent? roof = null;
        foreach (var tile in snapshot.Tiles)
        {
            if (!_tileDef.TryGetDefinition(tile.TileId, out var def))
                continue;

            tiles.Add((tile.Offset, new Tile(def.TileId, 0, tile.Variant, tile.RotationMirroring)));

            if (tile.Roof is not { } roofColor)
                continue;

            if (roof == null)
            {
                roof = EnsureComp<RoofComponent>(grid);
                roof.Color = roofColor;
            }
        }
        // Tiles go down first so anchored things like walls have something to anchor to.
        _map.SetTiles(grid, tiles);

        // Roofs are required so ambient blue starlight doesnt bleed in
        if (roof != null)
        {
            foreach (var tile in snapshot.Tiles)
            {
                if (tile.Roof != null)
                    _roof.SetRoof((grid, grid.Comp, roof), tile.Offset, true);
            }
        }

        var clones = new EntityUid?[snapshot.Entities.Count];
        for (var i = 0; i < snapshot.Entities.Count; i++)
        {
            var entity = snapshot.Entities[i];
            if (!_proto.HasIndex(entity.Prototype))
                continue;

            try
            {
                clones[i] = SpawnClone(grid, entity);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to rebuild {entity.Prototype} in snapshot: {e}");
            }
        }

        for (var i = 0; i < snapshot.Entities.Count; i++)
        {
            if (snapshot.Entities[i].BuckledTo is { } strapIndex
                && clones[i] is { } buckled
                && strapIndex < clones.Length
                && clones[strapIndex] is { } strap)
            {
                ApplyBuckleDrawDepth(buckled, strap);
            }
        }

        var eye = new FixedEye
        {
            Position = new MapCoordinates(snapshot.WorldCenter, mapId),
            DrawFov = false,
            Zoom = Vector2.One,
        };

        var size = ((snapshot.HalfExtents * 2) + Vector2i.One) * EyeManager.PixelsPerMeter;
        return (mapUid, eye, size);
    }

    /// <summary>
    /// How far shadows reach past the wall casting them, in tiles. Anything past the frame's edge is off the snapshot.
    /// </summary>
    private const float ShadowLength = 64f;

    /// <summary>
    /// Shadows like the game's field of view: every wall edge facing away from <paramref name="viewer"/> is stretched
    /// away from it. Edges between two neighbouring walls are skipped, so walls don't shade each other along a wall.
    /// </summary>
    /// <returns>A triangle list in grid-local coordinates.</returns>
    private static Vector2[] BuildShadows(Vector2 viewer, List<Vector2i> occluderTiles)
    {
        var occluders = new HashSet<Vector2i>(occluderTiles);
        var triangles = new List<Vector2>(occluders.Count * 6);
        foreach (var tile in occluders)
        {
            var (x, y) = (tile.X, tile.Y);
            AddEdge(new Vector2i(0, -1), new Vector2(x, y), new Vector2(x + 1, y));
            AddEdge(new Vector2i(0, 1), new Vector2(x, y + 1), new Vector2(x + 1, y + 1));
            AddEdge(new Vector2i(-1, 0), new Vector2(x, y), new Vector2(x, y + 1));
            AddEdge(new Vector2i(1, 0), new Vector2(x + 1, y), new Vector2(x + 1, y + 1));

            void AddEdge(Vector2i normal, Vector2 a, Vector2 b)
            {
                if (occluders.Contains(tile + normal))
                    return;

                // Only edges facing away from the viewer start a shadow.
                if (Vector2.Dot(normal, ((a + b) / 2) - viewer) <= 0)
                    return;

                var aFar = a + (Away(a) * ShadowLength);
                var bFar = b + (Away(b) * ShadowLength);
                triangles.AddRange([a, b, bFar, a, bFar, aFar]);
            }
        }

        return [.. triangles];

        Vector2 Away(Vector2 point)
        {
            var direction = point - viewer;
            return direction.LengthSquared() > 0.0001f ? Vector2.Normalize(direction) : Vector2.Zero;
        }
    }

    public void Destroy(EntityUid map)
    {
        if (!TerminatingOrDeleted(map))
            Del(map);
    }

    /// <summary>
    /// Releases one open view of the cached scene for <paramref name="snapshot"/>.
    /// Call once for each view that closes. Scenes with no open views can be destroyed when the cache is trimmed.
    /// </summary>
    public void ReleaseScene(NetEntity snapshot)
    {
        if (_scenes.TryGetValue(snapshot, out var scene) && scene.OpenViews > 0)
        {
            scene.OpenViews--;
            scene.LastAccess = _timing.RealTime;
        }

        TrimSceneCache();
    }

    private void TrimSceneCache()
    {
        while (_scenes.Count > MaxCachedScenes)
        {
            var oldest = _scenes
                .Where(entry => entry.Value.OpenViews == 0)
                .OrderBy(entry => entry.Value.LastAccess)
                .FirstOrDefault();
            if (oldest.Value == null)
                return;

            _scenes.Remove(oldest.Key);
            Destroy(oldest.Value.Map);
        }
    }

    private sealed class CachedScene(EntityUid map, IEye eye, Vector2i viewportSize, TimeSpan lastAccess)
    {
        public readonly EntityUid Map = map;
        public readonly IEye Eye = eye;
        public readonly Vector2i ViewportSize = viewportSize;
        public int OpenViews;
        public TimeSpan LastAccess = lastAccess;
    }

    private void ApplyBuckleDrawDepth(EntityUid buckled, EntityUid strap)
    {
        if (!_strapQuery.TryComp(strap, out var strapComp)
            || !_spriteQuery.TryComp(strap, out var strapSprite)
            || !_spriteQuery.TryComp(buckled, out var buckledSprite))
        {
            return;
        }

        if ((_transform.GetWorldRotation(strap).GetCardinalDir().AsFlag() & strapComp.LoweredDrawdepthDirections) == 0)
            return;

        _sprite.SetDrawDepth((buckled, buckledSprite), strapComp.LoweredDrawdepth ?? (strapSprite.DrawDepth - 1));
    }

    private EntityUid? SpawnClone(EntityUid grid, SnapshotEntity entity)
    {
        var coords = new EntityCoordinates(grid, _center + entity.Offset);
        var uid = entity.Humanoid is { } humanoid
            ? SpawnHumanoid(entity.Prototype, humanoid, coords) ?? Spawn(entity.Prototype, coords)
            : Spawn(entity.Prototype, coords);
        var spawned = uid;

        if (TryComp<FloatingVisualsComponent>(spawned, out var floating))
        {
            _floating.FloatAnimation(spawned, floating.Offset, floating.AnimationKey, floating.AnimationTime, stop: true);
            RemComp<FloatingVisualsComponent>(spawned);
        }

        // The snapshot grid is camera-aligned, so even prototypes that normally inherit grid rotation need a local rotation.
        var transform = Transform(spawned);
        if (transform.NoLocalRotation)
            _transform.SetNoLocalRotation(spawned, false, transform);

        _transform.SetLocalRotationNoLerp(spawned, entity.Rotation);
        if (entity.Anchored && !transform.Anchored)
            _transform.AnchorEntity(spawned);

        // Spawning or anchoring can adjust the transform. restore the captured in-tile position.
        _transform.SetLocalPositionNoLerp(spawned, coords.Position, transform);

        if (entity.Ssd)
        {
            EnsureComp<StatusIconComponent>(spawned);
            EnsureComp<MindExaminableComponent>(spawned);
            EnsureComp<SSDIndicatorComponent>(spawned).IsSSD = true;
        }

        if (entity.SupportOnly && _spriteQuery.TryGetComponent(spawned, out var supportSprite))
            _sprite.SetVisible((spawned, supportSprite), false);

        ApplyAppearance(spawned, entity.Appearance);
        ApplyIconSmoothStates(spawned, entity.IconSmoothStates);
        ApplyAnimationFrames(spawned, entity.AnimationFrames);

        if (entity.Light is { } snapshotLight)
            ApplyLight(spawned, snapshotLight);

        // Fed through the normal state handler, the same way the server's copy reaches clients.
        if (entity.MapText is { } mapTextState && _mapTextQuery.TryComp(spawned, out var mapText))
        {
            var handleState = new ComponentHandleState(mapTextState, null);
            EntityManager.EventBus.RaiseComponentEvent(spawned, mapText, ref handleState);
        }

        return spawned;
    }

    private void ApplyIconSmoothStates(EntityUid uid, List<SnapshotSpriteLayerState>? states)
    {
        if (states is not { Count: > 0 }
            || !TryComp<IconSmoothComponent>(uid, out var smooth)
            || !_spriteQuery.TryComp(uid, out var sprite))
        {
            return;
        }

        var validStates = new List<SnapshotSpriteLayerState>(states.Count);
        foreach (var state in states)
        {
            if (state.Layer < 0
                || string.IsNullOrEmpty(state.State)
                || state.State.Length > SnapshotCaptureSystem.MaxSnapshotIconSmoothStateLength
                || !_sprite.TryGetLayer((uid, sprite), state.Layer, out var layer, false)
                || layer?.ActualRsi is not { } rsi
                || !rsi.TryGetState(state.State, out _))
            {
                continue;
            }

            validStates.Add(state);
        }

        if (validStates.Count == 0)
            return;

        smooth.Mode = IconSmoothingMode.NoSprite;
        foreach (var state in validStates)
            _sprite.LayerSetRsiState((uid, sprite), state.Layer, state.State);
    }

    private void ApplyAnimationFrames(EntityUid uid, List<SnapshotSpriteLayerFrame>? frames)
    {
        if (frames == null || !_spriteQuery.TryComp(uid, out var sprite))
            return;

        var changed = false;
        foreach (var frame in frames)
        {
            if (!_sprite.TryGetLayer((uid, sprite), frame.Layer, out var layer, false)
                || layer == null
                || layer.ActualState is not { IsAnimated: true } state
                || frame.Frame < 0
                || frame.Frame >= state.DelayCount)
            {
                continue;
            }

            _sprite.LayerSetAutoAnimated((uid, sprite), frame.Layer, false);
            layer.AnimationFrame = frame.Frame;
            changed = true;
        }

        if (changed)
            _sprite.ForceUpdate(uid);
    }

    /// <summary>
    /// Builds a character the way the lobby preview does.
    /// </summary>
    private EntityUid? SpawnHumanoid(EntProtoId prototype, SnapshotHumanoid humanoid, EntityCoordinates coords)
    {
        if (string.IsNullOrEmpty(humanoid.Species.Id)
            || !_proto.Resolve(humanoid.Species, out var species))
            return null;

        var doll = Spawn(species.DollPrototype, coords);

        // Dolls don't inherit BaseMob's sprite settings so we need to add draw depth and rotation
        if (_proto.Index(prototype).TryComp<SpriteComponent>(out var mobSprite, _componentFactory)
            && _spriteQuery.TryComp(doll, out var dollSprite))
        {
            _sprite.SetDrawDepth((doll, dollSprite), mobSprite.DrawDepth);
            _sprite.SetSnapCardinals((doll, dollSprite), mobSprite.SnapCardinals);
            dollSprite.NoRotation = mobSprite.NoRotation;
        }

        var dollAppearance = Comp<HumanoidAppearanceComponent>(doll);
        dollAppearance.Species = humanoid.Species;
        dollAppearance.MarkingSet = new(humanoid.Markings);
        dollAppearance.CustomBaseLayers = new(humanoid.CustomBaseLayers);
        dollAppearance.PermanentlyHidden = [.. humanoid.PermanentlyHidden];
        dollAppearance.HiddenLayers = new(humanoid.HiddenLayers);
        dollAppearance.HideLayersOnEquip = [.. humanoid.HideLayersOnEquip];
        dollAppearance.MarkingsDisplacement = new(humanoid.MarkingsDisplacement);
        dollAppearance.Sex = humanoid.Sex;
        dollAppearance.Gender = humanoid.Gender;
        dollAppearance.SkinColor = humanoid.SkinColor;
        dollAppearance.EyeColor = humanoid.EyeColor;
        dollAppearance.EyeGlowing = humanoid.EyeGlowing;
        dollAppearance.Width = humanoid.Width;
        dollAppearance.Height = humanoid.Height;
        dollAppearance.UndergarmentTop = humanoid.UndergarmentTop;
        dollAppearance.UndergarmentBottom = humanoid.UndergarmentBottom;
        _humanoidAppearance.UpdateSprite((doll, dollAppearance, Comp<SpriteComponent>(doll)));

        foreach (var (slot, worn) in humanoid.Worn)
        {
            if (SpawnItem(worn) is not { } item)
                continue;

            if (!_inventory.TryEquip(doll, item, slot, silent: true, force: true))
                Del(item);
        }

        var fallbackHandIndex = 0;
        foreach (var (hand, held) in humanoid.Held)
        {
            if (SpawnItem(held) is not { } item)
                continue;

            var location = humanoid.HeldHandLocations.TryGetValue(hand, out var savedLocation)
                ? savedLocation
                : GetFallbackHandLocation(hand, fallbackHandIndex);
            fallbackHandIndex++;

            // Functional hands have no matching in-hand sprite state on most items.
            if (location == HandLocation.Functional)
                location = GetFallbackHandLocation(hand, fallbackHandIndex - 1);

            var hands = EnsureComp<HandsComponent>(doll);
            if (!_hands.TryGetHand((doll, hands), hand, out _))
                _hands.AddHand((doll, hands), hand, location);

            if (_hands.TryPickup(doll, item, hand, checkActionBlocker: false, animate: false, handsComp: hands))
                continue;

            var fallbackHand = $"snapshot_{hand}";
            for (var suffix = 1; _hands.TryGetHand((doll, hands), fallbackHand, out _); suffix++)
                fallbackHand = $"snapshot_{hand}_{suffix}";

            _hands.AddHand((doll, hands), fallbackHand, HandLocation.Functional);
            if (!_hands.TryPickup(doll, item, fallbackHand, checkActionBlocker: false, animate: false, handsComp: hands))
                Log.Error($"Failed to restore held item {held.Prototype} on snapshot humanoid {doll}.");
        }

        return doll;
    }

    private static HandLocation GetFallbackHandLocation(string hand, int index)
        => hand.Contains("left", StringComparison.OrdinalIgnoreCase)
            ? HandLocation.Left
            : hand.Contains("right", StringComparison.OrdinalIgnoreCase)
            ? HandLocation.Right
            : index % 2 == 0 ? HandLocation.Right : HandLocation.Left;

    private EntityUid? SpawnItem(SnapshotItem item)
    {
        if (!_proto.HasIndex(item.Prototype))
            return null;

        var uid = Spawn(item.Prototype, MapCoordinates.Nullspace);
        ApplyAppearance(uid, item.Appearance);
        return uid;
    }

    private void ApplyAppearance(EntityUid uid, Dictionary<Enum, object>? appearance)
    {
        if (appearance == null)
            return;

        foreach (var (key, value) in appearance)
        {
            _appearance.SetData(uid, key, value);
        }

        // Character height lives in ScaleVisuals, which only applies with its component present.
        if (appearance.TryGetValue(ScaleVisuals.Scale, out var scale) && scale is Vector2 scaleVector)
            _scaleVisuals.SetSpriteScale(uid, scaleVector);
    }

    private void ApplyLight(EntityUid uid, SnapshotLight snapshotLight)
    {
        var light = _light.EnsureLight(uid);
        _light.SetEnabled(uid, snapshotLight.Enabled, light);
        _light.SetColor(uid, snapshotLight.Color, light);
        _light.SetRadius(uid, snapshotLight.Radius, light);
        _light.SetEnergy(uid, snapshotLight.Energy, light);
        _light.SetSoftness(uid, snapshotLight.Softness, light);
        _light.SetFalloff(uid, snapshotLight.Falloff, light);
        _light.SetCurveFactor(uid, snapshotLight.CurveFactor, light);
        _light.SetCastShadows(uid, snapshotLight.CastShadows, light);
    }
}
