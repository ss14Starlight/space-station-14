using System.Numerics;
using Content.Server.MapText;
using Content.Server.Movement.Components;
using Content.Server.Power.Components;
using Content.Server.Power.Nodes;
using Content.Shared._Starlight.Light;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Snapshots;
using Content.Shared.Buckle.Components;
using Content.Shared.Camera;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.IdentityManagement;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Photography;
using Content.Shared.SSDIndicator;
using Content.Shared.Sprite;
using Content.Shared.MapText;
using Content.Shared.Parallax;
using Content.Shared.Players.RateLimiting;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.NodeContainer;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Flash.Components;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Wires;
using Robust.Shared;
using Robust.Shared.Containers;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Snapshots;

/// <summary>
/// Prints snapshots from <see cref="SnapshotCaptureComponent"/> items. Records what the photographer could see,
/// validating all client-sent data; arbitrary client data is never trusted.
/// </summary>
public sealed partial class ServerSnapshotCaptureSystem : SnapshotCaptureSystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SnapshotPersistenceSystem _snapshotPersistence = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedIlluminationSystem _illumination = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedRoofSystem _roof = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private EntityQuery<EyeCursorOffsetComponent> _cursorOffsetQuery = default!;
    [Dependency] private EntityQuery<EyeComponent> _eyeQuery = default!;
    [Dependency] private EntityQuery<MapComponent> _mapQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private EntityQuery<MapLightComponent> _mapLightQuery = default!;
    [Dependency] private EntityQuery<ParallaxComponent> _parallaxQuery = default!;
    [Dependency] private EntityQuery<RoofComponent> _roofQuery = default!;
    [Dependency] private EntityQuery<ImplicitRoofComponent> _implicitRoofQuery = default!;
    [Dependency] private EntityQuery<OccluderComponent> _occluderQuery = default!;
    [Dependency] private EntityQuery<BuckleComponent> _buckleQuery = default!;
    [Dependency] private EntityQuery<StealthComponent> _stealthQuery = default!;
    [Dependency] private EntityQuery<VirtualItemComponent> _virtualItemQuery = default!;
    [Dependency] private EntityQuery<AppearanceComponent> _appearanceQuery = default!;
    [Dependency] private EntityQuery<MapTextComponent> _mapTextQuery = default!;
    [Dependency] private EntityQuery<CableVisComponent> _cableVisQuery = default!;
    [Dependency] private EntityQuery<NodeContainerComponent> _nodeContainerQuery = default!;
    [Dependency] private EntityQuery<SSDIndicatorComponent> _ssdIndicatorQuery = default!;
    [Dependency] private EntityQuery<StandingStateComponent> _standingQuery = default!;
    [Dependency] private SharedPlayerRateLimitManager _rateLimit = default!;
    [Dependency] private EntityQuery<ActorComponent> _actorQuery = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private const string CaptureRateLimitKey = "SnapshotCapture";

    public override void Initialize()
    {
        base.Initialize();

        _rateLimit.Register(CaptureRateLimitKey,
            new RateLimitRegistration(StarlightCCVars.SnapshotViewRateLimitPeriod,
                StarlightCCVars.SnapshotViewRateLimitCount,
                null));
    }

    /// <summary>
    /// Hard caps for one capture. These bound server work and payload size; they are not CVars because
    /// they protect correctness, not admin tuning.
    /// </summary>
    private const int MaxEntities = 256;
    private const int MaxNearbyLights = 64;
    private const int MaxOccluders = 512;

    /// <summary>
    /// How far the photographer can see when deciding which tiles made it into the shot.
    /// </summary>
    private const float SightRange = 20f;

    /// <summary>
    /// Stealthed entities fainter than this are left out of the snapshot.
    /// </summary>
    private const float MinStealthVisibility = 0.5f;

    /// <summary>
    /// Points within a tile checked for line of sight: the middle, edge midpoints and corners. Kept inside the tile so
    /// a ray grazing a wall's corner can't count the tile behind it as seen.
    /// </summary>
    private static readonly Vector2[] _tileSamples =
    [
        new(0.5f, 0.5f),
        new(0.5f, 0.15f), new(0.5f, 0.85f), new(0.15f, 0.5f), new(0.85f, 0.5f),
        new(0.15f, 0.15f), new(0.85f, 0.15f), new(0.15f, 0.85f), new(0.85f, 0.85f),
    ];

    private readonly HashSet<EntityUid> _found = [];

    private readonly Dictionary<EntityUid, int> _captured = [];

    private readonly HashSet<EntityUid> _supportCaptured = [];

    private readonly List<Entity<SharedPointLightComponent, TransformComponent>> _nearbyLights = [];

    private readonly Dictionary<EntityUid, Vector2i> _tileOfCache = [];

    private readonly HashSet<Vector2i> _supportTiles = [];

    private readonly HashSet<Vector2i> _checkedTiles = [];

    private readonly HashSet<Vector2i> _visibleTiles = [];

    /// <summary>
    /// The viewfinder's extended view needs a larger PVS range, like binoculars.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnGetPvsScale(Entity<SnapshotCaptureComponent> ent, ref HeldRelayedEvent<GetEyePvsScaleRelayedEvent> args)
    {
        if (ent.Comp.Aiming && _cursorOffsetQuery.TryComp(ent, out var cursorOffset))
            args.Args.Scale += cursorOffset.PvsIncrease;
    }

    /// <summary>
    /// Prints the snapshot for a validated capture request and hands it to the photographer.
    /// Drops the print when persistence serialization exceeds the size limit.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnCaptured(Entity<SnapshotCaptureComponent> ent, ref SnapshotCapturedEvent args)
    {
        if (_actorQuery.TryComp(args.User, out var actor)
            && _rateLimit.CountAction(actor.PlayerSession, CaptureRateLimitKey) != RateLimitStatus.Allowed)
        {
            _popup.PopupClient(Loc.GetString("snapshot-capture-rate-limited"), args.User, args.User);
            return;
        }

        SpawnFlashLight(ent.Owner, ent.Comp);

        if (Capture(args.User, args.Center, ent.Comp.HalfExtents, args.Rotation, args.AnimationFrames, args.IconSmoothStates) is not { } data)
        {
            _popup.PopupClient(Loc.GetString("snapshot-capture-failed"), args.User, args.User);
            return;
        }

        if (args.Focus is { } focus && _captured.ContainsKey(focus))
        {
            data.SubjectNameText = Loc.GetString(
                HasComp<PhotographComponent>(focus)
                    ? "photograph-name-text-photograph"
                    : "photograph-name-text",
                ("entity", Identity.Entity(focus, EntityManager)));
            data.SubjectDescription = HasComp<PhotographComponent>(focus)
                ? null
                : _examine.GetExamineText(focus, args.User, out _)?.ToMarkup();
        }

        var snapshot = SpawnAttachedTo(ent.Comp.Snapshot, Transform(ent).Coordinates);
        if (!_snapshotPersistence.SetData(snapshot, data))
        {
            Del(snapshot);
            _popup.PopupClient(Loc.GetString("snapshot-capture-failed"), args.User, args.User);
            return;
        }

        _hands.PickupOrDrop(args.User, snapshot, dropNear: true);
    }

    /// <summary>
    /// Spawns a white point light at the camera while a flash fires, so the photograph records the flash.
    /// </summary>
    private void SpawnFlashLight(EntityUid capturer, SnapshotCaptureComponent capture)
    {
        var insertedActive = ItemSlotsTryGetFlash(capturer, out var flashUid)
            && HasComp<ActiveFlashComponent>(flashUid);
        var builtInActive = HasComp<ActiveFlashComponent>(capturer)
            && TryComp<FlashComponent>(capturer, out _);
        if (!insertedActive && !builtInActive)
            return;

        SpawnAttachedTo(capture.FlashEffect, Transform(capturer).Coordinates);
    }

    private bool ItemSlotsTryGetFlash(EntityUid capturer, out EntityUid flashUid)
    {
        var item = _itemSlotsSystem.GetItemOrNull(capturer, "flash");
        flashUid = item ?? default;
        return item is { } uid && TryComp<FlashComponent>(uid, out _);
    }

    /// <summary>
    /// Records the scene visible to the photographer centered on <paramref name="shotCenter"/> with camera rotation
    /// <paramref name="rotation"/>. Returns null when the shot is on another map or unusable. Client animation data is
    /// validated against the capture limits and ignored when oversized.
    /// </summary>
    public SnapshotData? Capture(
        EntityUid viewer,
        MapCoordinates shotCenter,
        Vector2i halfExtents,
        Angle rotation = default,
        List<SnapshotSpriteAnimation>? animationFrames = null,
        List<SnapshotSpriteLayerStateCapture>? iconSmoothStates = null)
    {
        var viewerCoords = TransformSystem.GetMapCoordinates(viewer);
        var mapId = shotCenter.MapId;
        if (mapId == MapId.Nullspace || viewerCoords.MapId != mapId)
            return null;

        _tileOfCache.Clear();
        var visMask = _eyeQuery.TryComp(viewer, out var eye) ? eye.VisibilityMask : EyeComponent.DefaultVisibilityMask;

        var snapshot = new SnapshotData { HalfExtents = halfExtents };

        // Same frame the viewfinder showed, so the snapshot matches what was aimed at.
        var frame = GetFrame(shotCenter, halfExtents, rotation);
        var frameRotation = frame.Rotation;
        var frameToWorld = frame.FrameToWorld;
        var worldToFrame = frame.WorldToFrame;
        var centerTile = frame.CenterTile;
        var center = centerTile + new Vector2(0.5f);

        // The map ambient remains the background light. roof color is captured individually for each tile.
        var worldCenter = Vector2.Transform(center, frameToWorld);
        snapshot.AmbientLight = _mapLightQuery.TryComp(_map.GetMapOrInvalid(mapId), out var mapLight)
            ? mapLight.AmbientLightColor
            : Color.Black;

        // A tile is in the snapshot if the photographer can see any part of it, not just its middle, so partly visible
        // tiles next to walls aren't blacked out.
        var visible = _visibleTiles;
        visible.Clear();
        for (var x = -halfExtents.X; x <= halfExtents.X; x++)
        {
            for (var y = -halfExtents.Y; y <= halfExtents.Y; y++)
            {
                var offset = new Vector2i(x, y);
                var index = centerTile + offset;
                if (CanSeeAnyOfTile(viewerCoords, index, frameToWorld, worldToFrame, mapId))
                    visible.Add(offset);
            }
        }

        Dictionary<EntityUid, Dictionary<int, int>>? animationByEntity = null;
        if (animationFrames is { Count: > MaxSnapshotAnimationFrames })
        {
            Log.Warning($"Ignored {animationFrames.Count} snapshot animation frames because the request exceeded the limit.");
        }
        else if (animationFrames is { Count: > 0 })
        {
            animationByEntity = [];
            foreach (var animation in animationFrames)
            {
                if (animation.Layer < 0 || animation.Layer >= MaxSnapshotAnimationLayers
                    || animation.Frame < 0 || animation.Frame > MaxSnapshotAnimationFrameIndex
                    || !TryGetEntity(animation.Entity, out var source)
                    || source is not { } sourceUid)
                {
                    continue;
                }

                if (!animationByEntity.TryGetValue(sourceUid, out var layers))
                    animationByEntity[sourceUid] = layers = [];

                layers.TryAdd(animation.Layer, animation.Frame);
            }
        }

        Dictionary<EntityUid, Dictionary<int, string>>? iconSmoothStatesByEntity = null;
        if (iconSmoothStates is { Count: > MaxSnapshotIconSmoothStates })
        {
            Log.Warning($"Ignored {iconSmoothStates.Count} snapshot icon-smoothing states because the request exceeded the limit.");
        }
        else if (iconSmoothStates is { Count: > 0 })
        {
            iconSmoothStatesByEntity = [];
            foreach (var iconSmoothState in iconSmoothStates)
            {
                if (iconSmoothState.Layer < 0
                    || iconSmoothState.Layer >= MaxSnapshotAnimationLayers
                    || string.IsNullOrEmpty(iconSmoothState.State)
                    || iconSmoothState.State.Length > MaxSnapshotIconSmoothStateLength
                    || !TryGetEntity(iconSmoothState.Entity, out var source)
                    || source is not { } sourceUid)
                {
                    continue;
                }

                if (!iconSmoothStatesByEntity.TryGetValue(sourceUid, out var layers))
                    iconSmoothStatesByEntity[sourceUid] = layers = [];

                layers.TryAdd(iconSmoothState.Layer, iconSmoothState.State);
            }
        }

        // Visible entities add their tile so they aren't blacked out by tile-level visibility sampling.
        // Reuse the visible set rather than copying it.
        var tilesInShot = visible;

        _found.Clear();
        _captured.Clear();
        _supportCaptured.Clear();
        var shotBounds = new Box2(centerTile - halfExtents, centerTile + halfExtents + Vector2i.One);
        var frameBox = shotBounds.Enlarged(1f);
        _lookup.GetEntitiesIntersecting(
            mapId,
            TransformBounds(frameBox, frameToWorld),
            _found,
            LookupFlags.Uncontained);

        var visibleEntityCount = 0;
        var supportTiles = _supportTiles;
        supportTiles.Clear();
        foreach (var uid in _found)
        {
            var xform = Transform(uid);
            var worldPosition = TransformSystem.GetWorldPosition(xform);
            var local = Vector2.Transform(worldPosition, worldToFrame);
            var tileOffset = TileOf(local) - centerTile;
            var inFrame = Math.Abs(tileOffset.X) <= halfExtents.X && Math.Abs(tileOffset.Y) <= halfExtents.Y;
            if (!inFrame)
                continue;

            if (!ShouldInclude(uid, visMask, null))
                continue;

            // Walls (and other tile-filling occluders) are visible if any part of their tile is visible,
            // not just their center. At an angle down a hall the center ray clips the neighbouring
            // wall's corner even though the wall's edge is clearly on camera. The tile samples
            // already checked that (and reuse saves a ray per wall).
            var isOccluder = _occluderQuery.TryComp(uid, out var occluder) && occluder.Enabled;
            if (isOccluder && tilesInShot.Contains(tileOffset))
            {
                // Fall through and capture.
            }
            else if (!_examine.InRangeUnOccluded(
                    viewerCoords,
                    new MapCoordinates(worldPosition, mapId),
                    SightRange,
                    blocker => blocker == uid))
                continue;

            if (MetaData(uid).EntityPrototype is not { } proto)
                continue;

            if (visibleEntityCount >= MaxEntities)
            {
                if (visibleEntityCount == MaxEntities)
                    Log.Warning($"Snapshot capture truncated at {MaxEntities} entities.");
                continue;
            }

            if (!visible.Contains(tileOffset))
            {
                tilesInShot.Add(tileOffset);
            }

            _captured[uid] = snapshot.Entities.Count;
            visibleEntityCount++;
            List<SnapshotSpriteLayerFrame>? capturedFrames = null;
            if (animationByEntity != null && animationByEntity.TryGetValue(uid, out var entityFrames))
            {
                capturedFrames = new List<SnapshotSpriteLayerFrame>(entityFrames.Count);
                foreach (var (layer, frameIndex) in entityFrames)
                    capturedFrames.Add(new SnapshotSpriteLayerFrame(layer, frameIndex));
            }

            List<SnapshotSpriteLayerState>? capturedIconSmoothStates = null;
            if (iconSmoothStatesByEntity != null && iconSmoothStatesByEntity.TryGetValue(uid, out var entityIconSmoothStates))
            {
                capturedIconSmoothStates = new List<SnapshotSpriteLayerState>(entityIconSmoothStates.Count);
                foreach (var (layer, state) in entityIconSmoothStates)
                    capturedIconSmoothStates.Add(new SnapshotSpriteLayerState(layer, state));
            }

            var appearance = GetAppearance(uid);
            if (_cableVisQuery.TryComp(uid, out var cableVis)
                && TryGetCableConnectedMask(uid, cableVis, out var connectedMask))
            {
                appearance ??= [];
                appearance[WireVisVisuals.ConnectedMask] = connectedMask;
            }

            snapshot.Entities.Add(new SnapshotEntity
            {
                Prototype = proto.ID,
                Offset = local - center,
                Rotation = TransformSystem.GetWorldRotation(xform) + frameRotation,
                Anchored = xform.Anchored,
                Ssd = _ssdIndicatorQuery.TryComp(uid, out var ssdIndicator) && ssdIndicator.IsSSD,
                Appearance = appearance,
                AnimationFrames = capturedFrames,
                IconSmoothStates = capturedIconSmoothStates,
                Light = GetLight(uid),
                MapText = GetMapText(uid),
                Humanoid = GetHumanoid(uid, visMask),
                RandomSprite = GetRandomSprite(uid),
            });
        }

        CaptureNearbyLights(snapshot, mapId, worldCenter, centerTile, shotBounds, worldToFrame,
            frameRotation, viewerCoords, visMask, supportTiles);

        foreach (var (uid, index) in _captured)
        {
            if (_buckleQuery.TryComp(uid, out var buckle)
                && buckle.BuckledTo is { } strap
                && _captured.TryGetValue(strap, out var strapIndex))
            {
                snapshot.Entities[index].BuckledTo = strapIndex;
            }
        }

        tilesInShot.UnionWith(supportTiles);
        // Frames are small and almost always on a single grid
        EntityUid tileGridUid = default;
        MapGridComponent? tileGrid = null;
        var haveGrid = false;
        var haveGridRotation = false;
        var gridRotation = default(Angle);
        foreach (var offset in tilesInShot)
        {
            var worldPosition = Vector2.Transform(centerTile + offset + new Vector2(0.5f), frameToWorld);
            if (!haveGrid)
            {
                if (!_map.TryFindGridAt(new MapCoordinates(worldPosition, mapId), out tileGridUid, out tileGrid)
                    || tileGrid == null)
                {
                    continue;
                }

                haveGrid = true;
            }
            else if (tileGrid is null)
            {
                haveGrid = false;
                haveGridRotation = false;
                if (!_map.TryFindGridAt(new MapCoordinates(worldPosition, mapId), out tileGridUid, out tileGrid)
                    || tileGrid == null)
                {
                    continue;
                }

                haveGrid = true;
            }

            var tileIndex = _map.WorldToTile(tileGridUid, tileGrid, worldPosition);
            if (!_map.TryGetTileRef(tileGridUid, tileGrid, tileIndex, out var tileRef)
                || tileRef.Tile.IsEmpty
                || !_tileDef.TryGetDefinition(tileRef.Tile.TypeId, out var tileDef))
            {
                // Frame straddles a grid edge; re-resolve for this tile.
                if (!_map.TryFindGridAt(new MapCoordinates(worldPosition, mapId), out tileGridUid, out tileGrid)
                    || tileGrid == null)
                {
                    continue;
                }

                haveGrid = true;
                haveGridRotation = false;
                tileIndex = _map.WorldToTile(tileGridUid, tileGrid, worldPosition);
                if (!_map.TryGetTileRef(tileGridUid, tileGrid, tileIndex, out tileRef)
                    || tileRef.Tile.IsEmpty
                    || !_tileDef.TryGetDefinition(tileRef.Tile.TypeId, out tileDef))
                {
                    continue;
                }
            }

            if (!haveGridRotation)
            {
                gridRotation = TransformSystem.GetWorldRotation(tileGridUid);
                haveGridRotation = true;
            }

            snapshot.Tiles.Add(new SnapshotTile(offset,
                tileDef.ID,
                tileRef.Tile.Variant,
                RotateTile(tileRef.Tile.RotationMirroring, gridRotation + frameRotation),
                GetRoof((tileGridUid, tileGrid), tileIndex)));
        }

        for (var x = -halfExtents.X; x <= halfExtents.X; x++)
        {
            for (var y = -halfExtents.Y; y <= halfExtents.Y; y++)
            {
                var offset = new Vector2i(x, y);
                if (!tilesInShot.Contains(offset))
                    snapshot.Hidden.Add(offset);
            }
        }

        snapshot.WorldCenter = worldCenter;
        snapshot.Parallax = _parallaxQuery.TryComp(_map.GetMapOrInvalid(mapId), out var parallax)
            ? parallax.Parallax
            : null;

        var viewerLocal = Vector2.Transform(viewerCoords.Position, worldToFrame);
        snapshot.Viewer = viewerLocal - center;
        AddSeenOccluders(snapshot, mapId, viewerCoords, viewerLocal, centerTile, frameToWorld, worldToFrame, tilesInShot);

        return snapshot;
    }

    private bool TryGetCableConnectedMask(EntityUid uid, CableVisComponent cableVis, out WireVisDirFlags mask)
    {
        mask = WireVisDirFlags.None;
        if (!_nodeContainerQuery.TryComp(uid, out var container)
            || !container.Nodes.TryGetValue(cableVis.Node, out var node)
            || node is not CableNode cableNode)
        {
            return false;
        }

        var transform = Transform(uid);
        if (!TryComp<MapGridComponent>(transform.GridUid, out var grid))
            return false;

        var gridUid = transform.GridUid!.Value;
        var tile = _map.TileIndicesFor((gridUid, grid), transform.Coordinates);
        foreach (var reachable in cableNode.ReachableNodes)
        {
            if (reachable is not CableNode)
                continue;

            var otherTransform = Transform(reachable.Owner);
            var otherTile = _map.TileIndicesFor((gridUid, grid), otherTransform.Coordinates);
            var diff = otherTile - tile;

            mask |= diff switch
            {
                (0, 1) => WireVisDirFlags.North,
                (0, -1) => WireVisDirFlags.South,
                (1, 0) => WireVisDirFlags.East,
                (-1, 0) => WireVisDirFlags.West,
                _ => WireVisDirFlags.None
            };
        }

        return true;
    }

    /// <summary>
    /// Records the sight-blocking tiles between the photographer and the frame, including ones just outside it that
    /// cast shadows into it, so the viewer can draw shadows like the game's field of view.
    /// </summary>
    private void AddSeenOccluders(SnapshotData snapshot,
        MapId mapId,
        MapCoordinates viewer,
        Vector2 viewerLocal,
        Vector2i centerTile,
        Matrix3x2 frameToWorld,
        Matrix3x2 worldToFrame,
        HashSet<Vector2i> visibleTiles)
    {
        var frameBox = new Box2(centerTile - snapshot.HalfExtents, centerTile + snapshot.HalfExtents + Vector2i.One);
        var area = frameBox.ExtendToContain(viewerLocal).Enlarged(1f);

        _found.Clear();
        _lookup.GetEntitiesIntersecting(mapId, TransformBounds(area, frameToWorld), _found, LookupFlags.Static);

        var checkedTiles = _checkedTiles;
        checkedTiles.Clear();
        foreach (var uid in _found)
        {
            if (snapshot.Occluders.Count >= MaxOccluders)
            {
                if (snapshot.Occluders.Count == MaxOccluders)
                    Log.Warning($"Snapshot capture truncated at {MaxOccluders} occluders.");
                break;
            }

            if (!_occluderQuery.TryComp(uid, out var occluder) || !occluder.Enabled)
                continue;

            var tile = TileOf(uid, worldToFrame);
            if (!checkedTiles.Add(tile))
                continue;

            // In-frame tiles were already ray-tested for the snapshot itself; reuse that.
            if (visibleTiles.Contains(tile - centerTile)
                || CanSeeAnyOfTile(viewer, tile, frameToWorld, worldToFrame, viewer.MapId))
                snapshot.Occluders.Add(tile - centerTile);
        }
    }

    private static byte RotateTile(byte rotationMirroring, Angle rotation)
    {
        var quarterTurns = GetQuarterTurns(rotation);
        var tileRotation = ((rotationMirroring & 0b11) + quarterTurns) % 4;
        return (byte) (tileRotation | (rotationMirroring & 0b100));
    }

    private static int GetQuarterTurns(Angle rotation)
    {
        var turns = (int) Math.Round(rotation.Theta / (Math.PI / 2));
        return ((turns % 4) + 4) % 4;
    }

    private bool ShouldInclude(EntityUid uid, int visMask, EntityUid? grid)
    {
        if (uid == grid || _mapQuery.HasComp(uid) || _gridQuery.HasComp(uid))
            return false;

        if (!IsVisibleTo(uid, visMask))
            return false;

        return !_container.IsEntityOrParentInContainer(uid);
    }

    /// <summary>
    /// PVS layer rules to not accidentally capture ghosts and such.
    /// </summary>
    private bool IsVisibleTo(EntityUid uid, int visMask)
    {
        var meta = MetaData(uid);
        if ((visMask & meta.VisibilityMask) != meta.VisibilityMask)
            return false;

        return !_stealthQuery.TryComp(uid, out var stealth)
            || !stealth.Enabled
            || _stealth.GetVisibility(uid, stealth) >= MinStealthVisibility;
    }

    private Color? GetRoof(Entity<MapGridComponent> grid, Vector2i index)
    {
        if (_roofQuery.TryComp(grid, out var roof))
            return _roof.GetColor((grid, grid.Comp, roof), index);

        return _implicitRoofQuery.TryComp(grid, out var implicitRoof) ? implicitRoof.Color : null;
    }

    /// <summary>
    /// Records a character's appearance, clothing and held items.
    /// </summary>
    private SnapshotHumanoid? GetHumanoid(EntityUid uid, int visMask)
    {
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var appearance)
            || string.IsNullOrEmpty(appearance.Species.Id))
            return null;

        var humanoid = new SnapshotHumanoid
        {
            Species = appearance.Species,
            Markings = new(appearance.MarkingSet),
            CustomBaseLayers = new(appearance.CustomBaseLayers),
            PermanentlyHidden = [.. appearance.PermanentlyHidden],
            HiddenLayers = new(appearance.HiddenLayers),
            HideLayersOnEquip = [.. appearance.HideLayersOnEquip],
            MarkingsDisplacement = new(appearance.MarkingsDisplacement),
            Sex = appearance.Sex,
            Gender = appearance.Gender,
            SkinColor = appearance.SkinColor,
            EyeColor = appearance.EyeColor,
            EyeGlowing = appearance.EyeGlowing,
            Width = appearance.Width,
            Height = appearance.Height,
            UndergarmentTop = appearance.UndergarmentTop,
            UndergarmentBottom = appearance.UndergarmentBottom,
            Standing = !_standingQuery.TryGetComponent(uid, out var standing) || standing.Standing,
        };

        if (_inventory.TryGetSlots(uid, out var slots))
        {
            foreach (var slot in slots)
            {
                // Pockets and the like don't show on the sprite, so their contents stay private even if someone
                // tried to read image data illicitly.
                if (slot.StripHidden || !_inventory.TryGetSlotEntity(uid, slot.Name, out var worn))
                    continue;

                if (GetItem(worn.Value, visMask) is { } item)
                    humanoid.Worn[slot.Name] = item;
            }
        }

        foreach (var hand in _hands.EnumerateHands(uid))
        {
            if (!_hands.TryGetHeldItem(uid, hand, out var held)
                || _virtualItemQuery.HasComp(held)
                || GetItem(held.Value, visMask) is not { } item)
            {
                continue;
            }

            humanoid.Held[hand] = item;
            if (_hands.TryGetHand(uid, hand, out var handData))
                humanoid.HeldHandLocations[hand] = handData.Value.Location;
        }

        return humanoid;
    }

    private SnapshotItem? GetItem(EntityUid uid, int visMask)
    {
        if (!IsVisibleTo(uid, visMask) || MetaData(uid).EntityPrototype is not { } proto)
            return null;

        return new SnapshotItem(proto.ID, GetAppearance(uid), GetRandomSprite(uid));
    }

    /// <summary>
    /// Copies the RandomSprite selection so greyscale tints (tools etc.) rebuild with the same color.
    /// </summary>
    private Dictionary<string, (string State, Color? Color)>? GetRandomSprite(EntityUid uid)
    {
        if (!TryComp<RandomSpriteComponent>(uid, out var component) || component.Selected.Count == 0)
            return null;

        return new Dictionary<string, (string State, Color? Color)>(component.Selected);
    }

    /// <summary>
    /// Copies the appearance data the server already networks.
    /// </summary>
    private Dictionary<Enum, object>? GetAppearance(EntityUid uid)
    {
        if (!_appearanceQuery.TryComp(uid, out var appearance) || !appearance.NetSyncEnabled)
            return null;

        if (EntityManager.GetComponentState(EntityManager.EventBus, appearance, null, GameTick.Zero)
                is not AppearanceComponentState { Data.Count: > 0 } state)
        {
            return null;
        }

        return new Dictionary<Enum, object>(state.Data);
    }

    private MapTextComponentState? GetMapText(EntityUid uid)
    {
        if (!_mapTextQuery.TryComp(uid, out var mapText))
            return null;

        return EntityManager.GetComponentState(EntityManager.EventBus, mapText, null, GameTick.Zero) as MapTextComponentState;
    }

    private SnapshotLight? GetLight(EntityUid uid)
    {
        if (!_light.TryGetLight(uid, out var light))
            return null;

        return new SnapshotLight(light.Enabled,
            light.Color,
            light.Radius,
            light.Energy,
            light.Softness,
            light.Falloff,
            light.CurveFactor,
            light.CastShadows,
            light.Offset);
    }

    /// <summary>
    /// Captures off-frame lights that still illuminate the frame as support-only entities.
    /// Reuses <see cref="_nearbyLights"/> instead of allocating a fresh lookup list per shot.
    /// </summary>
    private void CaptureNearbyLights(
        SnapshotData snapshot,
        MapId mapId,
        Vector2 worldCenter,
        Vector2i centerTile,
        Box2 frameBox,
        Matrix3x2 worldToFrame,
        Angle frameRotation,
        MapCoordinates _,
        int visMask,
        HashSet<Vector2i> supportTiles)
    {
        var maxLightRadius = _cfg.GetCVar(CVars.MaxLightRadius);
        if (!float.IsFinite(maxLightRadius) || maxLightRadius <= 0f)
            return;

        var frameRadius = new Vector2(snapshot.HalfExtents.X + 0.5f, snapshot.HalfExtents.Y + 0.5f).Length();
        _nearbyLights.Clear();
        _illumination.GetLightsInRange(
            new MapCoordinates(worldCenter, mapId),
            frameRadius + maxLightRadius + 1f,
            _nearbyLights);

        var center = Vector2.Transform(worldCenter, worldToFrame);
        var capturedCount = 0;
        foreach (var light in _nearbyLights)
        {
            var uid = light.Owner;
            if (capturedCount >= MaxNearbyLights)
            {
                if (capturedCount == MaxNearbyLights)
                    Log.Warning($"Snapshot capture truncated at {MaxNearbyLights} nearby lights.");
                break;
            }

            if (_captured.ContainsKey(uid) || _supportCaptured.Contains(uid))
                continue;

            var entityWorldPosition = TransformSystem.GetWorldPosition(light.Comp2);
            var worldRotation = TransformSystem.GetWorldRotation(light.Comp2);
            var lightWorldPosition = entityWorldPosition + worldRotation.RotateVec(light.Comp1.Offset);
            var lightLocalPosition = Vector2.Transform(lightWorldPosition, worldToFrame);
            var closest = Vector2.Clamp(lightLocalPosition, frameBox.BottomLeft, frameBox.TopRight);
            var radius = Math.Min(light.Comp1.Radius, maxLightRadius);
            if (!float.IsFinite(radius)
                || radius <= 0f
                || Vector2.DistanceSquared(lightLocalPosition, closest) > radius * radius)
            {
                continue;
            }

            if (!ShouldInclude(uid, visMask, null)
                || MetaData(uid).EntityPrototype is not { } proto)
            {
                continue;
            }

            // Support-only lights do not need to be directly visible to the viewer: they are rebuilt as
            // invisible illumination sources, so a partition that hides the lamp itself does not prevent it
            // from contributing light to the visible frame.

            snapshot.Entities.Add(new SnapshotEntity
            {
                Prototype = proto.ID,
                Offset = Vector2.Transform(entityWorldPosition, worldToFrame) - center,
                Rotation = worldRotation + frameRotation,
                Anchored = light.Comp2.Anchored,
                SupportOnly = true,
                Light = new SnapshotLight(
                    light.Comp1.Enabled,
                    light.Comp1.Color,
                    light.Comp1.Radius,
                    light.Comp1.Energy,
                    light.Comp1.Softness,
                    light.Comp1.Falloff,
                    light.Comp1.CurveFactor,
                    light.Comp1.CastShadows,
                    light.Comp1.Offset),
            });

            _supportCaptured.Add(uid);
            supportTiles.Add(TileOf(Vector2.Transform(entityWorldPosition, worldToFrame)) - centerTile);

            capturedCount++;
        }
    }

    /// <summary>
    /// Whether any part of a tile is visible from <paramref name="viewer"/>, sampling the tile at 9 points.
    /// </summary>
    private bool CanSeeAnyOfTile(MapCoordinates viewer, Vector2i index, Matrix3x2 frameToWorld, Matrix3x2 worldToFrame, MapId mapId)
    {
        foreach (var sample in _tileSamples)
        {
            var point = new MapCoordinates(Vector2.Transform(index + sample, frameToWorld), mapId);

            // Whatever stands on the tile itself shouldn't hide it.
            if (_examine.InRangeUnOccluded(viewer,
                    point,
                    SightRange,
                    (Index: index, WorldToFrame: worldToFrame),
                    (uid, state) => TileOf(uid, state.WorldToFrame) == state.Index))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Frame tile for an entity, cached because every occluder ray would otherwise recompute it.
    /// </summary>
    private Vector2i TileOf(EntityUid uid, Matrix3x2 worldToFrame)
    {
        // Called for every occluder hit of every tile ray
        if (_tileOfCache.TryGetValue(uid, out var tile))
            return tile;

        tile = TileOf(Vector2.Transform(TransformSystem.GetWorldPosition(uid), worldToFrame));
        _tileOfCache[uid] = tile;
        return tile;
    }

    private static Vector2i TileOf(Vector2 local)
        => new((int)MathF.Floor(local.X), (int)MathF.Floor(local.Y));
}
