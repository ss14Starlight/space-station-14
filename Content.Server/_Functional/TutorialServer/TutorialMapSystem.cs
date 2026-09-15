using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Atmos.Components;
using Content.Shared.Spawners.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Loads and unloads per-player tutorial maps (YAML grids or built practice rooms).
/// </summary>
public sealed partial class TutorialMapSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    // [Dependency] private MapSystem _map = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private TutorialNukeopsBaseSystem _nukeopsBase = default!;
    [Dependency] private TutorialPracticeRoomSystem _rooms = default!;
    [Dependency] private TutorialRoomTemplateSystem _templates = default!;
    [Dependency] private TutorialSalvageArenaSystem _salvageArenas = default!;
    [Dependency] private TutorialShuttleArenaSystem _shuttleArenas = default!;
    [Dependency] private TutorialDragonArenaSystem _dragonArenas = default!;

    /// <summary>
    /// Creates a private tutorial map for a role — shuttle/salvage/nukeops, then stamped
    /// room template, then last-resort procedural room, then plain YAML map.
    /// </summary>
    public bool TryLoadTutorialMap(
        TutorialRolePrototype role,
        out EntityUid mapUid,
        out EntityUid gridUid,
        out EntityCoordinates spawnCoords)
    {
        var loaded = false;
        mapUid = EntityUid.Invalid;
        gridUid = EntityUid.Invalid;
        spawnCoords = default;

        if (role.ShuttleArena != null)
            loaded = _shuttleArenas.TryBuildArena(role.ShuttleArena.Value, out mapUid, out gridUid, out spawnCoords);
        else if (role.SalvageArena != null)
            loaded = _salvageArenas.TryBuildArena(role.SalvageArena.Value, out mapUid, out gridUid, out spawnCoords);
        else if (role.DragonArena != null)
            loaded = _dragonArenas.TryBuildArena(role.DragonArena.Value, out mapUid, out gridUid, out spawnCoords);
        else if (role.NukeopsOutpost)
            loaded = _nukeopsBase.TryBuildOutpost(out mapUid, out gridUid, out spawnCoords);
        else if (role.RoomTemplate != null)
        {
            loaded = _templates.TryBuildFromTemplate(
                role.RoomTemplate.Value,
                ResolveCopyCount(role),
                out mapUid,
                out gridUid,
                out spawnCoords,
                CollectPracticePathTargets(role));
        }
        else if (role.Room != null)
        {
            // Last resort: one procedural chamber stamped into N identical copies.
            loaded = _templates.TryStampFromRoomPrototype(
                role.Room.Value,
                ResolveCopyCount(role),
                gateDoor: null,
                fillAtmosphere: true,
                out mapUid,
                out gridUid,
                out spawnCoords,
                practicePathTargets: CollectPracticePathTargets(role));
        }
        else if (TryLoadTutorialMap(role.Map, out mapUid, out gridUid, out spawnCoords))
        {
            loaded = true;
            // Procedural rooms fill their own air; a hand-authored map arrives as a vacuum unless
            // the mapper ran fixgridatmos before saving, which is not something a tutorial map
            // should be able to get wrong.
            _rooms.EnsureBreathableAtmosphere(gridUid);
        }

        if (loaded)
        {
            // Shuttle arenas use the primary grid as the flyable shuttle. TutorialInvisibleGridSupport
            // includes StationAnchor (switchedOn), which calls ShuttleSystem.Disable and leaves the
            // ship BodyType.Static forever — undock clears weld joints but thrusters still cannot
            // move it. Dock pads already get inherent gravity in TutorialShuttleArenaSystem.
            // Dragon arenas spawn the body in map space; only support the prey station grid.
            if (role.ShuttleArena == null)
                _rooms.EnsureGridSupport(gridUid);

            _rooms.EnableInherentGravity(gridUid);

            // Charge APCs on every grid (shuttle arenas / salvage debris included).
            var gridQuery = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
            while (gridQuery.MoveNext(out var otherGrid, out _, out var xform))
            {
                if (xform.MapUid != mapUid)
                    continue;
                _rooms.EnsureApcsCharged(otherGrid);
            }

            // Shuttle arenas need a powered helm/thrusters even when SimplifiedEnvironment is
            // false (cargo keeps live atmos for undock/space practice).
            if (role.ShuttleArena != null)
                ForcePowerMap(mapUid);

            // Role flag is authoritative over crop-stamped TutorialForcePowerGridComponent.
            if (role.SimplifiedEnvironment)
                ApplySimplifiedEnvironment(mapUid);
        }

        return loaded;
    }

    /// <summary>
    /// TEMPORARY: atmos freeze disabled — SimplifiedEnvironment only force-powers maps.
    /// Freezing grid atmos (fill-once / no LINDA) was causing odd behavior; re-enable freeze
    /// later via <see cref="FreezeAtmosInSimplifiedEnvironment"/> once that is sorted out.
    /// </summary>
    private const bool FreezeAtmosInSimplifiedEnvironment = false;

    /// <summary>
    /// Force-power every APC receiver on all grids of a tutorial map.
    /// Atmos freeze is temporarily skipped (see <see cref="FreezeAtmosInSimplifiedEnvironment"/>).
    /// </summary>
    public void ApplySimplifiedEnvironment(EntityUid mapUid)
    {
        if (!Exists(mapUid) || TerminatingOrDeleted(mapUid))
            return;

        ForcePowerMap(mapUid);

        // TEMPORARY: leave atmos simulating. Odd behavior was observed with freeze-on-load.
        if (!FreezeAtmosInSimplifiedEnvironment)
            return;

        // var gridQuery = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        // while (gridQuery.MoveNext(out var gridUid, out _, out var xform))
        // {
        //     if (xform.MapUid != mapUid)
        //         continue;
        //
        //     FreezeGridAtmosphere(gridUid);
        // }
    }

    /// <summary>
    /// Force-power every APC receiver on all grids of a tutorial map (does not freeze atmos).
    /// </summary>
    public void ForcePowerMap(EntityUid mapUid)
    {
        if (!Exists(mapUid) || TerminatingOrDeleted(mapUid))
            return;

        var gridQuery = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (gridQuery.MoveNext(out var gridUid, out _, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            ForcePowerGrid(gridUid);
        }
    }

    private void FreezeGridAtmosphere(EntityUid gridUid)
    {
        if (!TryComp<GridAtmosphereComponent>(gridUid, out var atmos))
            return;

        _atmos.SetAtmosphereSimulation((gridUid, atmos), false);
    }

    /// <summary>
    /// One stamped copy per practice chamber actually used by <see cref="TutorialRolePrototype.PracticeSpawns"/>
    /// (and any <see cref="TutorialGoalData.EnterRoom"/>). Goals no longer force empty transit rooms.
    /// </summary>
    public static int ResolveCopyCount(TutorialRolePrototype role)
    {
        var maxRoom = 0;
        foreach (var spawn in role.PracticeSpawns)
            maxRoom = Math.Max(maxRoom, spawn.Room);

        foreach (var goal in role.Goals)
        {
            if (goal.EnterRoom is { } enter)
                maxRoom = Math.Max(maxRoom, enter);
        }

        return Math.Max(1, maxRoom + 1);
    }

    /// <summary>
    /// Practice-kit tiles that must remain reachable after door vaulting (esp. single-chamber).
    /// </summary>
    public static List<(int Room, Vector2 Offset)> CollectPracticePathTargets(TutorialRolePrototype role)
    {
        var targets = new List<(int Room, Vector2 Offset)>(role.PracticeSpawns.Count);
        foreach (var spawn in role.PracticeSpawns)
            targets.Add((spawn.Room, spawn.Offset));
        return targets;
    }

    private void ForcePowerGrid(EntityUid gridUid)
    {
        var query = EntityQueryEnumerator<ApcPowerReceiverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var receiver, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;
            // Leave intentionally unpowered receivers alone (e.g. TutorialAirlockMaint pry doors).
            if (receiver.PowerDisabled)
                continue;
            _power.SetNeedsPower(uid, false);
        }
    }

    /// <summary>
    /// Creates a new map and loads the tutorial grid/map path onto it.
    /// Uses <see cref="MapLoaderSystem.TryLoadGeneric"/> so both grid YAML (StubPractice)
    /// and full map YAML (e.g. wizardsden) load without a failed Grid-category attempt.
    /// </summary>
    public bool TryLoadTutorialMap(ResPath path, out EntityUid mapUid, out EntityUid gridUid, out EntityCoordinates spawnCoords)
    {
        mapUid = EntityUid.Invalid;
        gridUid = EntityUid.Invalid;
        spawnCoords = default;

        // LogOrphanedGrids off: loading a curriculum grid onto its own map is exactly what that
        // option warns about, and the error it logs fails any test that starts a tutorial.
        var opts = new MapLoadOptions
        {
            DeserializationOptions = DeserializationOptions.Default with
            {
                InitializeMaps = true,
                LogOrphanedGrids = false,
            },
        };

        if (!_mapLoader.TryLoadGeneric(path, out var result, opts) || result.Grids.Count == 0)
        {
            Log.Error($"TutorialMapSystem failed to load {path}");
            if (result != null)
                _mapLoader.Delete(result);
            return false;
        }

        gridUid = result.Grids.First().Owner;
        if (result.Maps.Count > 0)
            mapUid = result.Maps.First().Owner;
        else if (Transform(gridUid).MapUid is { Valid: true } parentMap)
            mapUid = parentMap;
        else
        {
            Log.Error($"TutorialMapSystem loaded {path} but found no map for grid {gridUid}");
            _mapLoader.Delete(result);
            mapUid = EntityUid.Invalid;
            gridUid = EntityUid.Invalid;
            return false;
        }

        spawnCoords = ResolveSpawnCoords(gridUid);
        return true;
    }

    private EntityCoordinates ResolveSpawnCoords(EntityUid gridUid)
    {
        var query = EntityQueryEnumerator<TutorialSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == gridUid)
                return xform.Coordinates;
        }

        // Prefer late-join spawn points on the grid.
        var spawnQuery = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>(); // Starlight edit
        while (spawnQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == gridUid)
                return xform.Coordinates;
        }

        return new EntityCoordinates(gridUid, new System.Numerics.Vector2(0.5f, 0.5f));
    }

    public void UnloadTutorialMap(EntityUid mapUid)
    {
        if (!Exists(mapUid) || TerminatingOrDeleted(mapUid))
            return;

        if (!HasComp<MapComponent>(mapUid))
        {
            // Might have been passed a grid; delete its map.
            var xform = Transform(mapUid);
            if (xform.MapUid is { } parentMap)
                QueueDel(parentMap);
            else
                QueueDel(mapUid);
            return;
        }

        QueueDel(mapUid);
    }
}
