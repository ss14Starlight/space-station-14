using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Physics;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Functional.TutorialServer;

public sealed partial class TutorialRoomTemplateSystem
{
    private static readonly EntProtoId TutorialVaultDoorProto = "TutorialVaultDoor";

    [Dependency] private SharedDoorSystem _doors = default!;
    [Dependency] private TurfSystem _turf = default!;

    /// <summary>
    /// Seals crop airlocks that are not on the walk path from each chamber center to its
    /// inter-chamber gate openings (and practice-kit tiles). Doors on that path are removed
    /// (open passage) so players never need to path through vaults, tables-as-detours, or
    /// carved windows. Walls / windows are never deleted.
    /// </summary>
    private void SealSuperfluousDoorsAndEnsurePaths(
        EntityUid gridUid,
        MapGridComponent grid,
        int width,
        int height,
        int copyCount,
        TutorialRoomDoorSide stampDirection,
        int gateLateral,
        TutorialRoomLayoutComponent layout,
        IReadOnlyList<(int Room, Vector2 Offset)>? practicePathTargets = null)
    {
        var keepOpenTiles = CollectCriticalDoorTiles(
            gridUid, grid, width, height, copyCount, stampDirection, gateLateral, layout,
            practicePathTargets);
        ReplaceNonGateDoors(gridUid, grid, keepOpenTiles);
    }

    /// <summary>
    /// Tiles that hold doors needed to walk from chamber centers to stamp-axis gate openings
    /// and (for single-chamber / practice kits) to practice spawn offsets.
    /// </summary>
    private HashSet<Vector2i> CollectCriticalDoorTiles(
        EntityUid gridUid,
        MapGridComponent grid,
        int width,
        int height,
        int copyCount,
        TutorialRoomDoorSide stampDirection,
        int gateLateral,
        TutorialRoomLayoutComponent layout,
        IReadOnlyList<(int Room, Vector2 Offset)>? practicePathTargets)
    {
        var keepOpen = new HashSet<Vector2i>();

        for (var i = 0; i < copyCount; i++)
        {
            var origin = GetChamberOrigin(i, width, height, copyCount, stampDirection);
            var center = layout.ChamberCenters[i];
            var start = new Vector2i((int) MathF.Floor(center.X), (int) MathF.Floor(center.Y));

            if (i < copyCount - 1)
            {
                var forward = GetForwardOpening(origin, width, height, stampDirection, gateLateral);
                AddDoorTilesOnPath(gridUid, grid, start, forward, keepOpen);
            }

            if (i > 0)
            {
                var backward = GetBackwardOpening(origin, width, height, stampDirection, gateLateral);
                AddDoorTilesOnPath(gridUid, grid, start, backward, keepOpen);
            }
        }

        // Single-chamber stamps have no inter-chamber gates, so without practice targets every
        // crop airlock would vault — trapping players away from R&D / bar kit / etc.
        if (practicePathTargets != null)
        {
            foreach (var (room, offset) in practicePathTargets)
            {
                var chamber = Math.Clamp(room, 0, copyCount - 1);
                var center = layout.ChamberCenters[chamber];
                var start = new Vector2i((int) MathF.Floor(center.X), (int) MathF.Floor(center.Y));
                var goalPos = center + offset;
                var goal = new Vector2i((int) MathF.Floor(goalPos.X), (int) MathF.Floor(goalPos.Y));
                if (start == goal)
                    continue;
                AddDoorTilesOnPath(gridUid, grid, start, goal, keepOpen);
            }
        }

        return keepOpen;
    }

    private void AddDoorTilesOnPath(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i start,
        Vector2i goal,
        HashSet<Vector2i> keepOpen)
    {
        // Prefer a path that stays on open floor (doors closed/blocked).
        if (TryFindPath(gridUid, grid, start, goal, doorMode: DoorPathMode.Blocked, out _))
            return;

        // Otherwise path through doors and mark those door tiles as must-stay-open.
        if (!TryFindPath(gridUid, grid, start, goal, doorMode: DoorPathMode.Passable, out var path))
        {
            Log.Warning(
                $"Tutorial stamp: no walk path from {start} to gate opening {goal} even with doors open; leaving crop doors as-is for that link");
            return;
        }

        foreach (var tile in path)
        {
            if (TileHasDoor(gridUid, grid, tile))
                keepOpen.Add(tile);
        }
    }

    private void ReplaceNonGateDoors(
        EntityUid gridUid,
        MapGridComponent grid,
        HashSet<Vector2i> keepOpenTiles)
    {
        var toProcess = new List<(EntityUid Uid, Vector2 LocalPos, Angle Rotation, bool Anchored, Vector2i Tile)>();
        var query = EntityQueryEnumerator<DoorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            if (HasComp<TutorialGateDoorComponent>(uid))
                continue;

            if (_metaQuery.TryGetComponent(uid, out var meta) &&
                meta.EntityPrototype?.ID == TutorialVaultDoorProto.Id)
                continue;

            var tile = new Vector2i(
                (int) MathF.Floor(xform.LocalPosition.X),
                (int) MathF.Floor(xform.LocalPosition.Y));
            toProcess.Add((uid, xform.LocalPosition, xform.LocalRotation, xform.Anchored, tile));
        }

        foreach (var (uid, localPos, rotation, anchored, tile) in toProcess)
        {
            // Critical path: remove the door so the passage stays open and obvious.
            if (keepOpenTiles.Contains(tile))
            {
                Del(uid);
                continue;
            }

            // Side rooms / RD office / maint: sealed vault — clearly not the tutorial path.
            Del(uid);

            var spawned = Spawn(TutorialVaultDoorProto, new EntityCoordinates(gridUid, localPos));
            _transform.SetLocalRotation(spawned, rotation);

            if (anchored && TryComp<MapGridComponent>(gridUid, out var destGrid))
            {
                var childXform = _xformQuery.GetComponent(spawned);
                if (!childXform.Anchored && childXform.GridUid == gridUid)
                    _transform.AnchorEntity((spawned, childXform), (gridUid, destGrid));
            }

            SealVaultDoor(spawned);
        }
    }

    private void SealVaultDoor(EntityUid door)
    {
        if (TryComp<DoorComponent>(door, out var doorComp))
        {
            doorComp.CanPry = false;
            Dirty(door, doorComp);
            // Keep Closed (not Welded): HighSec collision + bolts are enough; Welded on airlock
            // parents was unreliable for blocking movement after stamp replace.
            if (doorComp.State != DoorState.Closed && doorComp.State != DoorState.Welded)
                _doors.SetState(door, DoorState.Closed, doorComp);
        }

        if (TryComp<DoorBoltComponent>(door, out var bolt) && !bolt.BoltsDown)
            _doors.SetBoltsDown((door, bolt), true);
    }

    private enum DoorPathMode : byte
    {
        /// <summary>Doors block movement (find a path that does not need them).</summary>
        Blocked,

        /// <summary>Doors count as walkable (discover which doors are required).</summary>
        Passable,
    }

    private bool TryFindPath(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i start,
        Vector2i goal,
        DoorPathMode doorMode,
        out List<Vector2i> path)
    {
        path = new List<Vector2i>();
        if (!IsTileInBounds(gridUid, grid, start) || !IsTileInBounds(gridUid, grid, goal))
            return false;

        if (!IsWalkableTile(gridUid, grid, start, doorMode) ||
            !IsWalkableTile(gridUid, grid, goal, doorMode))
            return false;

        var cameFrom = new Dictionary<Vector2i, Vector2i>();
        var queue = new Queue<Vector2i>();
        queue.Enqueue(start);
        cameFrom[start] = start;

        var dirs = new[]
        {
            new Vector2i(1, 0),
            new Vector2i(-1, 0),
            new Vector2i(0, 1),
            new Vector2i(0, -1),
        };

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == goal)
            {
                ReconstructPath(cameFrom, start, goal, path);
                return true;
            }

            foreach (var dir in dirs)
            {
                var next = cur + dir;
                if (cameFrom.ContainsKey(next))
                    continue;

                if (!IsWalkableTile(gridUid, grid, next, doorMode))
                    continue;

                cameFrom[next] = cur;
                queue.Enqueue(next);
            }
        }

        return false;
    }

    private static void ReconstructPath(
        Dictionary<Vector2i, Vector2i> cameFrom,
        Vector2i start,
        Vector2i goal,
        List<Vector2i> path)
    {
        var cur = goal;
        while (cur != start)
        {
            path.Add(cur);
            cur = cameFrom[cur];
        }

        path.Add(start);
        path.Reverse();
    }

    private bool IsTileInBounds(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var tileRef = _map.GetTileRef(gridUid, grid, tile);
        return !tileRef.Tile.IsEmpty;
    }

    private bool TileHasDoor(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        foreach (var ent in _map.GetAnchoredEntities((gridUid, grid), tile))
        {
            if (HasComp<TutorialGateDoorComponent>(ent))
                continue;

            if (HasComp<DoorComponent>(ent))
                return true;
        }

        return false;
    }

    private bool IsWalkableTile(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        DoorPathMode doorMode)
    {
        if (!IsTileInBounds(gridUid, grid, tile))
            return false;

        var hasPassableDoor = false;
        foreach (var ent in _map.GetAnchoredEntities((gridUid, grid), tile))
        {
            if (HasComp<TutorialGateDoorComponent>(ent))
                return true;

            if (!HasComp<DoorComponent>(ent))
                continue;

            if (doorMode == DoorPathMode.Blocked)
                return false;

            hasPassableDoor = true;
        }

        // Path search treating doors as open — ignore door physics on this tile.
        if (hasPassableDoor)
            return true;

        // Full mob mask so furniture (tables) is avoided and paths stay in open floor.
        return !_turf.IsTileBlocked(gridUid, tile, CollisionGroup.MobMask);
    }
}
