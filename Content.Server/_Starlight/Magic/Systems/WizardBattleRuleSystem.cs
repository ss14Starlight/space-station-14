using Content.Server._Starlight.Magic.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Maps;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Content.Shared.Station;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;
using Robust.Shared.Maths;
using System.Linq;
using System.Numerics;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Robust.Shared.Random;
using Content.Server.Spawners.Components;

namespace Content.Server._Starlight.Magic.Systems;

/// <summary>
/// Handles loading the two wizard shuttles for the Wizard Battle game rule.
/// </summary>
public sealed class WizardBattleRuleSystem : GameRuleSystem<WizardBattleRuleComponent>
{
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AntagSelectLocationEvent>(OnAntagSelectLocation);
    }

    protected override void Added(EntityUid uid, WizardBattleRuleComponent comp, GameRuleComponent rule, GameRuleAddedEvent args)
    {

        // Create a temporary map to load the shuttles
        var tempMap = _map.CreateMap(out var tempMapId);

        // Load red faction shuttle on the temporary map
        var redOpts = DeserializationOptions.Default with { InitializeMaps = true };
        if (!_mapLoader.TryLoadGrid(tempMapId, comp.ShuttlePathRed, out var redGrid, redOpts))
        {
            Log.Error($"Failed to load red wizard shuttle from {comp.ShuttlePathRed}!");
            ForceEndSelf(uid, rule);
            return;
        }

        // Load blue faction shuttle on the temporary map
        var blueOpts = DeserializationOptions.Default with { InitializeMaps = true };
        if (!_mapLoader.TryLoadGrid(tempMapId, comp.ShuttlePathBlue, out var blueGrid, blueOpts))
        {
            Log.Error($"Failed to load blue wizard shuttle from {comp.ShuttlePathBlue}!");
            ForceEndSelf(uid, rule);
            return;
        }

        // Store the loaded grids in the component
        comp.RedShuttle = redGrid.Value.Owner;
        comp.BlueShuttle = blueGrid.Value.Owner;
        comp.TempMapId = tempMapId;

        base.Added(uid, comp, rule, args);
    }

    protected override void Started(EntityUid uid, WizardBattleRuleComponent comp, GameRuleComponent rule, GameRuleStartedEvent args)
    {
        // Find the station
        var stationQuery = EntityQueryEnumerator<StationDataComponent>();
        if (!stationQuery.MoveNext(out var stationUid, out var stationData))
        {
            Log.Error("No station found for Wizard Battle!");
            return;
        }

        // Get the map ID from the station's largest grid
        var largestGrid = _station.GetLargestGrid((stationUid, stationData));
        if (largestGrid == null)
        {
            Log.Error("Station has no largest grid!");
            return;
        }

        var mapId = Transform(largestGrid.Value).MapID;
        var stationPos = _transform.GetWorldPosition(largestGrid.Value);

        // Move the shuttles to the station's map
        if (comp.RedShuttle.HasValue)
        {
            _transform.SetParent(comp.RedShuttle.Value, _mapManager.GetMapEntityId(mapId));
            var redPos = stationPos + comp.RedOffset;
            _transform.SetWorldPosition(comp.RedShuttle.Value, redPos);
        }

        if (comp.BlueShuttle.HasValue)
        {
            _transform.SetParent(comp.BlueShuttle.Value, _mapManager.GetMapEntityId(mapId));
            var bluePos = stationPos + comp.BlueOffset;
            _transform.SetWorldPosition(comp.BlueShuttle.Value, bluePos);
        }

        // Delete the temporary map
        if (comp.TempMapId.HasValue)
        {
            _map.DeleteMap(comp.TempMapId.Value);
        }

        // Notify RuleGridsComponent about the loaded grids
        var grids = new List<EntityUid>();
        if (comp.RedShuttle.HasValue) grids.Add(comp.RedShuttle.Value);
        if (comp.BlueShuttle.HasValue) grids.Add(comp.BlueShuttle.Value);
        var ev = new RuleLoadedGridsEvent(mapId, grids);
        RaiseLocalEvent(uid, ref ev);

        // Add spawners to the red shuttle
        if (comp.RedShuttle.HasValue)
        {
            var redGridUid = Transform(comp.RedShuttle.Value).GridUid;

            if (redGridUid == null)
            {
                Log.Error("RedShuttle GridUid is null. Cannot spawn entities.");
                return;
            }

            var redSpawner = EntityManager.SpawnEntity("SpawnPointGhostArchmageRed", new EntityCoordinates(comp.RedShuttle.Value, Vector2.Zero));
            _transform.SetParent(redSpawner, comp.RedShuttle.Value);
            Log.Debug($"Red spawner parent set to: {Transform(redSpawner).ParentUid}, GridUid: {Transform(redSpawner).GridUid}");

            if (Transform(redSpawner).GridUid == redGridUid)
            {
                _transform.AttachToGridOrMap(redSpawner);
                Log.Debug($"Red spawner anchored to grid: {Transform(redSpawner).GridUid}");
            }
            else
            {
                Log.Error($"Red spawner grid mismatch: Spawner GridUid={Transform(redSpawner).GridUid}, Shuttle GridUid={redGridUid}");
            }

            var apprenticeSpawner = EntityManager.SpawnEntity("SpawnPointGhostApprentice", new EntityCoordinates(comp.RedShuttle.Value, new Vector2(1, 0)));
            _transform.SetParent(apprenticeSpawner, comp.RedShuttle.Value);
            Log.Debug($"Apprentice spawner parent set to: {Transform(apprenticeSpawner).ParentUid}, GridUid: {Transform(apprenticeSpawner).GridUid}");

            if (Transform(apprenticeSpawner).GridUid == redGridUid)
            {
                _transform.AttachToGridOrMap(apprenticeSpawner);
                Log.Debug($"Apprentice spawner anchored to grid: {Transform(apprenticeSpawner).GridUid}");
            }
            else
            {
                Log.Error($"Apprentice spawner grid mismatch: Spawner GridUid={Transform(apprenticeSpawner).GridUid}, Shuttle GridUid={redGridUid}");
            }
        }
        else
        {
            Log.Error("RedShuttle is null. Cannot spawn entities.");
        }

        // Add spawners to the blue shuttle
        if (comp.BlueShuttle.HasValue)
        {
            var blueSpawner = EntityManager.SpawnEntity("SpawnPointGhostArchmageBlue", new EntityCoordinates(comp.BlueShuttle.Value, Vector2.Zero));
            _transform.SetParent(blueSpawner, comp.BlueShuttle.Value);
            Log.Debug($"Blue spawner parent set to: {Transform(blueSpawner).ParentUid}");

            if (Transform(blueSpawner).GridUid == Transform(comp.BlueShuttle.Value).GridUid)
            {
                _transform.AttachToGridOrMap(blueSpawner);
                Log.Debug($"Blue spawner anchored to grid: {Transform(blueSpawner).GridUid}");
            }
            else
            {
                Log.Error($"Blue spawner grid mismatch: Spawner GridUid={Transform(blueSpawner).GridUid}, Shuttle GridUid={Transform(comp.BlueShuttle.Value).GridUid}");
            }

            var apprenticeSpawnerBlue = EntityManager.SpawnEntity("SpawnPointGhostApprentice", new EntityCoordinates(comp.BlueShuttle.Value, new Vector2(1, 0)));
            _transform.SetParent(apprenticeSpawnerBlue, comp.BlueShuttle.Value);
            Log.Debug($"Apprentice spawner (blue) parent set to: {Transform(apprenticeSpawnerBlue).ParentUid}");

            if (Transform(apprenticeSpawnerBlue).GridUid == Transform(comp.BlueShuttle.Value).GridUid)
            {
                _transform.AttachToGridOrMap(apprenticeSpawnerBlue);
                Log.Debug($"Apprentice spawner (blue) anchored to grid: {Transform(apprenticeSpawnerBlue).GridUid}");
            }
            else
            {
                Log.Error($"Apprentice spawner (blue) grid mismatch: Spawner GridUid={Transform(apprenticeSpawnerBlue).GridUid}, Shuttle GridUid={Transform(comp.BlueShuttle.Value).GridUid}");
            }
        }

        base.Started(uid, comp, rule, args);
    }

    private void OnAntagSelectLocation(ref AntagSelectLocationEvent args)
    {
        if (!TryComp<WizardBattleRuleComponent>(args.GameRule, out var comp))
            return;

        EntityUid? shuttle = null;

        // Determine shuttle based on the antag definition
        if (args.Def.StartingGear == "ArchmageGearRed")
        {
            shuttle = comp.RedShuttle;
        }
        else if (args.Def.StartingGear == "ArchmageGearBlue")
        {
            shuttle = comp.BlueShuttle;
        }
        else if (args.Def.MindRoles != null && args.Def.MindRoles.Contains("MindRoleApprentice"))
        {
            // For apprentices, randomly choose a shuttle
            var shuttles = new List<EntityUid>();
            if (comp.RedShuttle.HasValue) shuttles.Add(comp.RedShuttle.Value);
            if (comp.BlueShuttle.HasValue) shuttles.Add(comp.BlueShuttle.Value);
            if (shuttles.Count > 0)
                shuttle = _random.Pick(shuttles);
        }

        if (!shuttle.HasValue)
            return;

        // Find the spawn point entity on the shuttle
        var spawnQuery = EntityQueryEnumerator<SpawnPointComponent>();
        while (spawnQuery.MoveNext(out var spawnUid, out _))
        {
            if (Transform(spawnUid).ParentUid != shuttle.Value)
                continue;

            // Use the first SpawnPoint on the shuttle
            var coords = new EntityCoordinates(shuttle.Value, Transform(spawnUid).LocalPosition);
            args.Coordinates.Add(_transform.ToMapCoordinates(coords));
            return;
        }

        // If no spawn point found, pick a random position on the shuttle
        if (TryComp<MapGridComponent>(shuttle.Value, out var grid))
        {
            var bounds = grid.LocalAABB;
            var randomPos = new Vector2(
                _random.NextFloat(bounds.Left, bounds.Right),
                _random.NextFloat(bounds.Bottom, bounds.Top));
            var coords = new EntityCoordinates(shuttle.Value, randomPos);
            args.Coordinates.Add(_transform.ToMapCoordinates(coords));
        }
    }
}