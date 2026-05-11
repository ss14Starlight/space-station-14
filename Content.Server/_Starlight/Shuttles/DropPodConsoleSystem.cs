using Content.Server.Chat.Systems;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared._Starlight.Shuttles.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Maps;
using Content.Shared.Shuttles.Components;
using Content.Shared.Throwing;
using Content.Shared.UserInterface;
using Content.Shared.Warps;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Content.Server._Starlight.Shuttles;

public sealed class DropPodConsoleSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DropPodConsoleComponent, AfterActivatableUIOpenEvent>(OnConsoleOpened);
        SubscribeLocalEvent<DropPodComponent, FTLCompletedEvent>(OnDropPodArrived);

        Subs.BuiEvents<DropPodConsoleComponent>(DropPodConsoleUiKey.Key, subs =>
        {
            subs.Event<DropPodConsoleDeployMessage>(OnDeployMessage);
        });
    }

    private void OnDropPodArrived(Entity<DropPodComponent> ent, ref FTLCompletedEvent args)
    {
        var podGrid = ent.Owner;
        if (!TryComp<MapGridComponent>(podGrid, out var podGridComp))
            return;

        // Freeze physics immediately so Smimsh forces can't further move/rotate the pod
        _shuttle.Disable(podGrid);
        // Do NOT zero the rotation here — MergeIntoStation reads pod-local coords before reparenting

        var podXformOnArrival = Transform(podGrid);
        var podCoords = podXformOnArrival.Coordinates;
        var podWorldPos = _transform.GetWorldPosition(podXformOnArrival);
        var mapId = podXformOnArrival.MapID;

        // Play loud impact sound at landing site
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/metal_slam5.ogg"), podCoords, AudioParams.Default.WithVolume(12f));
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/explosion3.ogg"), podCoords, AudioParams.Default.WithVolume(10f));

        // Camera shake — everyone within 20 tiles gets their screen kicked
        var epicenter = new MapCoordinates(podWorldPos, mapId);
        var shakeFilter = Filter.Empty().AddInRange(epicenter, 20f, _playerManager, EntityManager);
        foreach (var player in shakeFilter.Recipients)
        {
            if (player.AttachedEntity is not { } shakeTarget)
                continue;
            var delta = _transform.GetWorldPosition(shakeTarget) - podWorldPos;
            if (delta.EqualsApprox(Vector2.Zero))
                delta = new Vector2(0.01f, 0f);
            var dist = delta.Length();
            var intensity = 5f * (1f - dist / 20f);
            if (intensity > 0.01f)
                _recoil.KickCamera(shakeTarget, delta.Normalized() * intensity);
        }

        // Damage and knock back all nearby humanoids; deal structural damage to station entities in blast radius
        var bluntDamage = new DamageSpecifier();
        bluntDamage.DamageDict.Add("Blunt", 50);
        var structuralDamage = new DamageSpecifier();
        structuralDamage.DamageDict.Add("Blunt", 500);

        var nearby = _lookup.GetEntitiesInRange(podCoords, 6f,
            LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries);
        foreach (var target in nearby)
        {
            if (target == podGrid) continue;
            var targetXform = Transform(target);

            if (HasComp<HumanoidAppearanceComponent>(target))
            {
                // People inside the pod (on the pod grid) or buckled to a seat are protected
                if (targetXform.GridUid == podGrid)
                    continue;
                if (TryComp<BuckleComponent>(target, out var buckle) && buckle.Buckled)
                    continue;

                _damageable.TryChangeDamage(target, bluntDamage, ignoreResistances: false);
                var dir = _transform.GetWorldPosition(targetXform) - podWorldPos;
                if (dir == Vector2.Zero)
                    dir = new Vector2(_random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f));
                _throwing.TryThrow(target, Vector2.Normalize(dir), 8f);
            }
            else if (targetXform.GridUid != podGrid)
            {
                // Skip cables and wires — they have very low HP and shouldn't be destroyed by impact
                if (HasComp<CableComponent>(target))
                    continue;
                _damageable.TryChangeDamage(target, structuralDamage, ignoreResistances: true);
            }
        }

        // Merge pod into station grid so tiles and walls become part of the station
        var stationGridUid = ent.Comp.TargetStationGrid;
        if (stationGridUid != null
            && !TerminatingOrDeleted(stationGridUid.Value)
            && TryComp<MapGridComponent>(stationGridUid.Value, out var stationGridComp))
        {
            MergeIntoStation(podGrid, podGridComp, stationGridUid.Value, stationGridComp);
        }
        else
        {
            _shuttle.Disable(podGrid);
        }
    }

    /// <summary>
    /// Copies pod tiles onto the station grid and reparents all pod entities, correcting for any
    /// pod rotation so everything lands axis-aligned with the station grid.
    /// </summary>
    private void MergeIntoStation(
        EntityUid podGrid, MapGridComponent podComp,
        EntityUid stationGrid, MapGridComponent stationComp)
    {
        // Build matrices BEFORE any rotation changes so world positions of children are still valid.
        var podWorldMatrix = _transform.GetWorldMatrix(podGrid);
        Matrix3x2.Invert(podWorldMatrix, out var podInvMatrix);
        var stationWorldMatrix = _transform.GetWorldMatrix(stationGrid);
        Matrix3x2.Invert(stationWorldMatrix, out var stationInvMatrix);
        var podWorldRot = Transform(podGrid).WorldRotation;

        // Find which station tile the pod's local origin (0,0) falls in.
        // Use Round (not Floor) to handle floating-point imprecision from FTL placement.
        var podOriginWorld = _transform.GetWorldPosition(podGrid);
        var podOriginStation = Vector2.Transform(podOriginWorld, stationInvMatrix);
        var baseTile = new Vector2i(
            (int)Math.Round(podOriginStation.X / stationComp.TileSize),
            (int)Math.Round(podOriginStation.Y / stationComp.TileSize));

        // 1. Copy tiles using tile-index arithmetic — completely rotation-independent.
        //    Pod tile (i,j) always goes to station tile (base.X+i, base.Y+j).
        var tilesToSet = new List<(Vector2i GridIndices, Tile Tile)>();
        foreach (var tileRef in _mapSystem.GetAllTiles(podGrid, podComp))
        {
            var stationTileIdx = baseTile + tileRef.GridIndices;
            foreach (var stale in _mapSystem.GetAnchoredEntities(stationGrid, stationComp, stationTileIdx).ToList())
                QueueDel(stale);
            tilesToSet.Add((stationTileIdx, tileRef.Tile));
        }
        _mapSystem.SetTiles((stationGrid, stationComp), tilesToSet);

        // 2. Reparent pod children, correcting for any pod rotation.
        var podXform = Transform(podGrid);
        var toMove = new List<EntityUid>();
        var childEnum = podXform.ChildEnumerator;
        while (childEnum.MoveNext(out var child))
            toMove.Add(child);

        foreach (var child in toMove)
        {
            if (TerminatingOrDeleted(child)) continue;
            var childXform = Transform(child);
            var wasAnchored = childXform.Anchored;

            // Read the entity's pod-local rotation BEFORE reparenting (it will change after).
            var podLocalRot = childXform.LocalRotation;

            // Convert entity world position → pod-local → station-local.
            // This un-rotates the position relative to the pod, giving a clean axis-aligned placement.
            var entityWorldPos = _transform.GetWorldPosition(childXform);
            var entityPodLocal = Vector2.Transform(entityWorldPos, podInvMatrix);
            var entityStationLocal = entityPodLocal + new Vector2(
                baseTile.X * stationComp.TileSize,
                baseTile.Y * stationComp.TileSize);

            _transform.SetCoordinates(child, new EntityCoordinates(stationGrid, entityStationLocal));
            // Restore pod-local rotation (SetCoordinates doesn't preserve it when changing parent).
            _transform.SetLocalRotation(child, podLocalRot);

            if (wasAnchored)
                _transform.AnchorEntity(child, Transform(child));
        }

        // 3. Remove the now-empty pod grid.
        QueueDel(podGrid);
    }

    private void OnConsoleOpened(Entity<DropPodConsoleComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        UpdateUiState(ent);
    }

    private void UpdateUiState(Entity<DropPodConsoleComponent> ent)
    {
        var (uid, comp) = ent;

        var xform = Transform(uid);
        var onDropPod = xform.GridUid != null && HasComp<DropPodComponent>(xform.GridUid.Value);
        var alreadyLaunched = xform.GridUid != null
            && TryComp<DropPodComponent>(xform.GridUid.Value, out var dropPod)
            && dropPod.Launched;
        var canLaunch = onDropPod
            && !alreadyLaunched
            && (_timing.CurTime - comp.LastLaunchTime) >= comp.Cooldown;

        // Gather ALL beacons (including blacklisted) to compute centroid and find station grid
        var allBeacons = new List<(EntityUid uid, Vector2 worldPos)>();
        var beaconQuery = AllEntityQuery<WarpPointComponent, MetaDataComponent>();
        while (beaconQuery.MoveNext(out var beaconUid, out var warp, out var meta))
        {
            allBeacons.Add((beaconUid, _transform.GetWorldPosition(beaconUid)));
        }

        var available = new HashSet<DropPodDirection> { DropPodDirection.North, DropPodDirection.East, DropPodDirection.South, DropPodDirection.West };
        var stationCenter = Vector2.Zero;
        NetEntity? stationGrid = null;
        if (allBeacons.Count > 0)
        {
            var center = Vector2.Zero;
            foreach (var (_, pos) in allBeacons)
                center += pos;
            center /= allBeacons.Count;
            stationCenter = center;

            // Find station grid from the first beacon's grid
            var firstBeaconXform = Transform(allBeacons[0].uid);
            if (firstBeaconXform.GridUid.HasValue)
                stationGrid = GetNetEntity(firstBeaconXform.GridUid.Value);
        }

        _ui.SetUiState(uid, DropPodConsoleUiKey.Key, new DropPodConsoleBuiState
        {
            AvailableDirections = available,
            CanLaunch = canLaunch,
            AlreadyLaunched = alreadyLaunched,
            StationGrid = stationGrid,
            StationWorldCenter = stationCenter,
        });
    }

    private void OnDeployMessage(Entity<DropPodConsoleComponent> ent, ref DropPodConsoleDeployMessage args)
    {
        var (uid, comp) = ent;

        var xform = Transform(uid);
        if (xform.GridUid == null || !TryComp<DropPodComponent>(xform.GridUid.Value, out var dropPod))
        {
            Log.Warning($"DropPodConsole {ToPrettyString(uid)} is not on a DropPod grid.");
            return;
        }

        if (dropPod.Launched)
            return;

        if ((_timing.CurTime - comp.LastLaunchTime) < comp.Cooldown)
            return;

        // Gather all beacons for centroid; valid (non-blacklisted) beacons for landing candidates
        var allBeaconsList = new List<(EntityUid uid, Vector2 worldPos)>();
        var validBeacons = new List<(EntityUid uid, Vector2 worldPos, string name)>();
        var blacklistedPositions = new List<Vector2>();
        var beaconQuery = AllEntityQuery<WarpPointComponent, MetaDataComponent>();
        while (beaconQuery.MoveNext(out var beaconUid, out var warp, out var meta))
        {
            var name = warp.Location ?? meta.EntityName;
            var worldPos = _transform.GetWorldPosition(beaconUid);
            allBeaconsList.Add((beaconUid, worldPos));
            if (!string.IsNullOrEmpty(name) && IsBlacklisted(name, comp.BeaconBlacklist))
                blacklistedPositions.Add(worldPos);
            else if (!string.IsNullOrEmpty(name))
                validBeacons.Add((beaconUid, worldPos, name));
        }

        if (validBeacons.Count == 0)
            return;

        // Compute centroid from all beacons
        var center = Vector2.Zero;
        foreach (var (_, pos) in allBeaconsList)
            center += pos;
        center /= allBeaconsList.Count;

        // Copy direction to a local so it can be captured in a lambda (args is ref)
        var chosenDir = args.Direction;
        var candidates = validBeacons
            .Where(b => ClassifyDirection(b.worldPos - center) == chosenDir)
            .ToList();

        // Fallback: if direction has no candidates (stale state), pick any
        if (candidates.Count == 0)
            candidates = validBeacons;

        var picked = _random.Pick(candidates);
        var targetBeacon = picked.uid;
        var targetWorldPos = picked.worldPos;
        var beaconXform = Transform(targetBeacon);
        if (beaconXform.MapUid == null)
            return;

        var podGrid = xform.GridUid.Value;
        if (!TryComp<ShuttleComponent>(podGrid, out var shuttle))
        {
            Log.Warning($"DropPodConsole {ToPrettyString(uid)}: drop pod grid {ToPrettyString(podGrid)} has no ShuttleComponent.");
            return;
        }

        // Announce the general direction — never the specific beacon name
        var dirStr = args.Direction switch
        {
            DropPodDirection.North => Loc.GetString("drop-pod-direction-north"),
            DropPodDirection.East  => Loc.GetString("drop-pod-direction-east"),
            DropPodDirection.South => Loc.GetString("drop-pod-direction-south"),
            DropPodDirection.West  => Loc.GetString("drop-pod-direction-west"),
            _                      => Loc.GetString("drop-pod-direction-north"),
        };
        var announcement = Loc.GetString("drop-pod-console-launch-announcement",
            ("direction", dirStr),
            ("seconds", (int)comp.AnnouncementLeadTime));
        _chat.DispatchGlobalAnnouncement(
            announcement,
            sender: Loc.GetString("drop-pod-console-sender"),
            colorOverride: Color.Red);

        dropPod.Launched = true;
        comp.LastLaunchTime = _timing.CurTime;
        dropPod.TargetStationGrid = beaconXform.GridUid;

        // Exact landing coords — random 20-45 tile offset, kept away from blacklisted beacons
        var targetCoords = GetDropPodTargetCoords(targetBeacon, targetWorldPos, blacklistedPositions);

        _shuttle.FTLToCoordinates(
            podGrid,
            shuttle,
            targetCoords,
            Angle.Zero,
            startupTime: comp.AnnouncementLeadTime,
            hyperspaceTime: 0f);

        UpdateUiState(ent);
    }

    private static DropPodDirection ClassifyDirection(Vector2 delta)
    {
        // Classify into N/E/S/W based on which axis dominates
        if (MathF.Abs(delta.Y) >= MathF.Abs(delta.X))
            return delta.Y >= 0 ? DropPodDirection.North : DropPodDirection.South;
        return delta.X >= 0 ? DropPodDirection.East : DropPodDirection.West;
    }

    private EntityCoordinates GetDropPodTargetCoords(EntityUid beaconEnt, Vector2 beaconWorldPos, List<Vector2> blacklistedPositions)
    {
        var beaconXform = Transform(beaconEnt);
        var mapUid = beaconXform.MapUid!.Value;
        var beaconMapCoords = new MapCoordinates(beaconWorldPos, beaconXform.MapID);

        const float MinBlacklistDist = 20f;
        const float MaxBlacklistDist = 45f;

        if (_mapManager.TryFindGridAt(beaconMapCoords, out var gridUid, out var gridComp))
        {
            const int maxTries = 24;
            for (var i = 0; i < maxTries; i++)
            {
                var offsetAngle = new Angle(_random.NextDouble() * Math.Tau);
                var offsetDist = _random.NextFloat(3f, 8f);
                var offsetVec = offsetAngle.RotateVec(new Vector2(offsetDist, 0f));
                var testWorldPos = beaconWorldPos + offsetVec;

                // Must be on solid ground
                if (!IsPositionSafeFromSpace(gridUid, gridComp, testWorldPos, 2))
                    continue;

                // Must be at least MinBlacklistDist tiles from every blacklisted beacon
                var tooClose = false;
                foreach (var blackPos in blacklistedPositions)
                {
                    if ((testWorldPos - blackPos).Length() < MinBlacklistDist)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose)
                    continue;

                var tileIdx = _mapSystem.WorldToTile(gridUid, gridComp, testWorldPos);
                var snappedPos = Vector2.Transform(
                    new Vector2(tileIdx.X * gridComp.TileSize, tileIdx.Y * gridComp.TileSize),
                    _transform.GetWorldMatrix(gridUid));
                return new EntityCoordinates(mapUid, snappedPos);
            }

            // Fallback: pick any solid tile near the beacon that isn't blacklisted-adjacent
            for (var i = 0; i < maxTries; i++)
            {
                var offsetAngle = new Angle(_random.NextDouble() * Math.Tau);
                var offsetDist = _random.NextFloat(3f, 8f);
                var offsetVec = offsetAngle.RotateVec(new Vector2(offsetDist, 0f));
                var testWorldPos = beaconWorldPos + offsetVec;
                if (!IsPositionSafeFromSpace(gridUid, gridComp, testWorldPos, 2))
                    continue;
                var tileIdx = _mapSystem.WorldToTile(gridUid, gridComp, testWorldPos);
                var snappedPos = Vector2.Transform(
                    new Vector2(tileIdx.X * gridComp.TileSize, tileIdx.Y * gridComp.TileSize),
                    _transform.GetWorldMatrix(gridUid));
                return new EntityCoordinates(mapUid, snappedPos);
            }
        }

        // Last-resort fallback: snap to beacon tile
        if (_mapManager.TryFindGridAt(beaconMapCoords, out var fallbackGrid, out var fallbackGridComp))
        {
            var tileIdx = _mapSystem.WorldToTile(fallbackGrid, fallbackGridComp, beaconWorldPos);
            var snappedPos = Vector2.Transform(
                new Vector2(tileIdx.X * fallbackGridComp.TileSize, tileIdx.Y * fallbackGridComp.TileSize),
                _transform.GetWorldMatrix(fallbackGrid));
            return new EntityCoordinates(mapUid, snappedPos);
        }

        return new EntityCoordinates(mapUid, beaconWorldPos);
    }

    private bool IsPositionSafeFromSpace(EntityUid gridUid, MapGridComponent grid, Vector2 worldPos, int checkRadius)
    {
        var tileIdx = _mapSystem.WorldToTile(gridUid, grid, worldPos);
        for (var dx = -checkRadius; dx <= checkRadius; dx++)
        {
            for (var dy = -checkRadius; dy <= checkRadius; dy++)
            {
                var neighbor = tileIdx + new Vector2i(dx, dy);
                var tileRef = _mapSystem.GetTileRef(gridUid, grid, neighbor);
                if (_turf.IsSpace(tileRef))
                    return false;
            }
        }
        return true;
    }

    private static bool IsBlacklisted(string beaconName, List<string> blacklist)
    {
        foreach (var entry in blacklist)
        {
            if (beaconName.Contains(entry, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
