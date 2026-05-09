using Content.Server.Chat.Systems;
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

        // Determine if this console is on a valid drop pod grid
        var xform = Transform(uid);
        var onDropPod = xform.GridUid != null && HasComp<DropPodComponent>(xform.GridUid.Value);
        var alreadyLaunched = xform.GridUid != null
            && TryComp<DropPodComponent>(xform.GridUid.Value, out var dropPod)
            && dropPod.Launched;

        var canLaunch = onDropPod
            && !alreadyLaunched
            && (_timing.CurTime - comp.LastLaunchTime) >= comp.Cooldown;

        // Gather all non-blacklisted warp points
        var beacons = new List<DropPodBeaconEntry>();
        var beaconQuery = AllEntityQuery<WarpPointComponent, MetaDataComponent, TransformComponent>();
        while (beaconQuery.MoveNext(out var beaconUid, out var warp, out var meta, out _))
        {
            var name = warp.Location ?? meta.EntityName;
            if (string.IsNullOrEmpty(name))
                continue;

            if (IsBlacklisted(name, comp.BeaconBlacklist))
                continue;

            beacons.Add(new DropPodBeaconEntry
            {
                Beacon = GetNetEntity(beaconUid),
                Name = name,
            });
        }

        var state = new DropPodConsoleBuiState
        {
            Beacons = beacons,
            CanLaunch = canLaunch,
            AlreadyLaunched = alreadyLaunched,
        };

        _ui.SetUiState(uid, DropPodConsoleUiKey.Key, state);
    }

    private void OnDeployMessage(Entity<DropPodConsoleComponent> ent, ref DropPodConsoleDeployMessage args)
    {
        var (uid, comp) = ent;

        // Validate console is on a drop pod grid
        var xform = Transform(uid);
        if (xform.GridUid == null || !TryComp<DropPodComponent>(xform.GridUid.Value, out var dropPod))
        {
            Log.Warning($"DropPodConsole {ToPrettyString(uid)} is not on a DropPod grid.");
            return;
        }

        if (dropPod.Launched)
            return;

        // Cooldown check
        if ((_timing.CurTime - comp.LastLaunchTime) < comp.Cooldown)
            return;

        // Resolve the target beacon
        var beaconEnt = GetEntity(args.SelectedBeacon);
        if (!TryComp<WarpPointComponent>(beaconEnt, out var targetWarp))
        {
            Log.Warning($"DropPodConsole {ToPrettyString(uid)}: selected entity {ToPrettyString(beaconEnt)} has no WarpPointComponent.");
            return;
        }

        var beaconMeta = MetaData(beaconEnt);
        var beaconName = targetWarp.Location ?? beaconMeta.EntityName;

        // Double-check the beacon is not blacklisted (server-side authoritative validation)
        if (IsBlacklisted(beaconName, comp.BeaconBlacklist))
        {
            Log.Warning($"DropPodConsole {ToPrettyString(uid)}: attempted to target blacklisted beacon '{beaconName}'.");
            return;
        }

        var beaconXform = Transform(beaconEnt);
        if (beaconXform.MapUid == null)
            return;

        // The drop pod grid must have a ShuttleComponent to FTL
        var podGrid = xform.GridUid.Value;
        if (!TryComp<ShuttleComponent>(podGrid, out var shuttle))
        {
            Log.Warning($"DropPodConsole {ToPrettyString(uid)}: drop pod grid {ToPrettyString(podGrid)} has no ShuttleComponent.");
            return;
        }

        // Broadcast global warning announcement
        var announcement = Loc.GetString("drop-pod-console-launch-announcement",
            ("beaconName", beaconName),
            ("seconds", (int)comp.AnnouncementLeadTime));
        _chat.DispatchGlobalAnnouncement(
            announcement,
            sender: Loc.GetString("drop-pod-console-sender"),
            colorOverride: Color.Red);

        // Mark launched and record time
        dropPod.Launched = true;
        comp.LastLaunchTime = _timing.CurTime;

        // Store the station grid so we can merge into it on arrival
        dropPod.TargetStationGrid = beaconXform.GridUid;

        // Target coordinates: near the beacon but not directly on it, avoiding space edges
        var targetCoords = GetDropPodTargetCoords(beaconEnt);

        // FTL the drop pod grid to crash directly on the beacon
        _shuttle.FTLToCoordinates(
            podGrid,
            shuttle,
            targetCoords,
            Angle.Zero,
            startupTime: comp.AnnouncementLeadTime,
            hyperspaceTime: 0f);

        UpdateUiState(ent);
    }

    private EntityCoordinates GetDropPodTargetCoords(EntityUid beaconEnt)
    {
        var beaconXform = Transform(beaconEnt);
        var beaconWorldPos = _transform.GetWorldPosition(beaconXform);
        var mapUid = beaconXform.MapUid!.Value;
        var beaconMapCoords = new MapCoordinates(beaconWorldPos, beaconXform.MapID);

        // If the beacon is on a grid, try random offsets that keep us away from space
        if (_mapManager.TryFindGridAt(beaconMapCoords, out var gridUid, out var gridComp))
        {
            const int maxTries = 8;
            for (var i = 0; i < maxTries; i++)
            {
                var offsetAngle = new Angle(_random.NextDouble() * Math.Tau);
                var offsetDist = _random.NextFloat(2.5f, 5f);
                var offsetVec = offsetAngle.RotateVec(new Vector2(offsetDist, 0f));
                var testWorldPos = beaconWorldPos + offsetVec;

                if (IsPositionSafeFromSpace(gridUid, gridComp, testWorldPos, 2))
                {
                    // Snap to tile origin (corner) so pod tile (i,j) maps to station tile (base+i, base+j)
                    // and all pod entities land at tile centers rather than tile corners
                    var tileIdx = _mapSystem.WorldToTile(gridUid, gridComp, testWorldPos);
                    var snappedPos = Vector2.Transform(
                        new Vector2(tileIdx.X * gridComp.TileSize, tileIdx.Y * gridComp.TileSize),
                        _transform.GetWorldMatrix(gridUid));
                    return new EntityCoordinates(mapUid, snappedPos);
                }
            }
        }

        // Fallback: snap to nearest tile at the exact beacon position
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
