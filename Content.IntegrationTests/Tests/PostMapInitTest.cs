using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Utility;
using YamlDotNet.RepresentationModel;
using Content.Server.Administration.Systems;
using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Spawners.Components;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests;

[TestFixture]
public sealed class PostMapInitTest : GameTest
{
    #region Starlight
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
    };

    private static PoolSettings s_mapInitSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    private static readonly string[] _noSpawnMaps =
    [
        "StarlightCentCommG24",
        "StarlightCentCommSC17",
        "StarlightCentCommGNT9",
        "Dart"
    ];

    private static readonly string[] _grids =
    [
        AdminTestArenaSystem.ArenaMapPath
    ];

    /// <summary>
    /// A dictionary linking maps to collections of entity prototype ids that should be exempt from "DoNotMap" restrictions.
    /// </summary>
    /// <remarks>
    /// This declares that the listed entity prototypes are allowed to be present on the map
    /// despite being categorized as "DoNotMap", while any unlisted prototypes will still
    /// cause the test to fail.
    /// </remarks>
    private static readonly Dictionary<string, HashSet<EntProtoId>> _doNotMapWhitelistSpecific = new()
    {
        {"/Maps/_Starlight/Shuttles/ShuttleEvent/honki.yml", ["GoldenBikeHorn", "RubberStampClown"]},
        {"/Maps/Shuttles/ShuttleEvent/syndie_evacpod.yml", ["RubberStampSyndicate"]},
        {"/Maps/Shuttles/ShuttleEvent/instigator.yml", ["ShuttleGunFriendship"]},
        {"/Maps/_Starlight/Stations/Cork.yml", ["RubberStampSyndicate"]},
        {"/Maps/_Starlight/Shuttles/CC-NT/NTSF_Minos_Battlecruiser.yml", ["ShuttleGunPerforator"]},
        {"/Maps/_Starlight/Shuttles/Admeme/RecluseClassSHC.yml", ["RubberStampSyndicate"]},
        {"/Maps/_Starlight/Shuttles/Admeme/Signaleer.yml", ["RubberStampSyndicate"]},
        {"/Maps/_Starlight/Shuttles/ShuttleEvent/montague.yml", ["RubberStampSolgovLaw", "RubberStampSolgovRep", "RubberStampTSF", "RubberStampTSMC"]},
        {"/Maps/_Starlight/Shuttles/ShuttleEvent/syndie_evacpod.yml", ["RubberStampSyndicate"]},
        {"/Maps/_Starlight/Nonstations/nukieplanet.yml", ["RubberStampSyndicate"]},
        {"/Maps/_Starlight/Nonstations/nukiewestern.yml", ["RubberStampSyndicate"]},
        {"/Maps/_Starlight/Nonstations/geigerComplex.yml", ["RubberStampSyndicate"]},
        {"/Maps/_Starlight/Dungeon/syndie.yml", ["RubberStampSyndicate"]},
        {"/Maps/_Starlight/Shuttles/Admeme/scarletSHCdefenderFinal.yml", ["RubberStampSyndicate", "TraitorCodePaper"]},
        {"/Maps/_Starlight/Centcomms/CC_Outpost_SC17.yml", ["BoxFolderCentComEmpty", "BoxFolderCentCom", "RubberStampCentcom", "BoxFolderCentComThreePapers"]},
        {"/Maps/_Starlight/Centcomms/CC_Outpost_G24.yml", ["BoxFolderCentCom", "RubberStampCentcom"]},
        {"/Maps/_Starlight/Centcomms/CC_Outpost_GNT9.yml", ["BoxFolderCentCom", "RubberStampCAD", "RubberStampCCD", "RubberStampCDD", "RubberStampCED", "RubberStampCentcom", "RubberStampCID", "RubberStampCMD", "RubberStampCRD", "RubberStampCSD"]}
    };

    /// <summary>
    /// Maps listed here are given blanket freedom to contain "DoNotMap" entities. Use sparingly.
    /// </summary>
    /// <remarks>
    /// It is also possible to whitelist entire directories here. For example, adding
    /// "/Maps/Shuttles/**" will whitelist all shuttle maps.
    /// </remarks>
    private static readonly string[] _doNotMapWhitelist =
    [
        "/Maps/Shuttles/AdminSpawn/**", // admin gaming
        "/Maps/_Starlight/Shuttles/Admeme/Radiotower.yml", // Command stamps - listening post.
        "/Maps/_Starlight/Stations/Remix.yml", // All stamps - listening post (extreme edition).
    ];

    private static readonly ProtoId<EntityCategoryPrototype> _shouldMapCategory = "ShouldMapStation";

    /// <summary>
    /// Converts the above globs into regex so your eyes dont bleed trying to add filepaths.
    /// </summary>
    private static readonly Regex[] _doNotMapWhiteListRegexes = [.. _doNotMapWhitelist.Select(glob => new Regex(GlobToRegex(glob), RegexOptions.IgnoreCase | RegexOptions.Compiled))];

    private static readonly string[] _gameMaps = [.. GameDataScrounger.PrototypesOfKind<GameMapPrototype>().Where(x => x != PoolManager.TestMap)];
    private static readonly ResPath[] _allMapFiles = GameDataScrounger.FilesInDirectoryInVfs("/Maps/_Starlight", "*.yml");
    private static readonly ResPath[] _shuttleMapFiles = GameDataScrounger.FilesInDirectoryInVfs("/Maps/_Starlight/Shuttles", "*.yml");

    private static readonly ProtoId<EntityCategoryPrototype> _doNotMapCategory = "DoNotMap";
    #endregion

    private const bool SkipTestMaps = true;
    private const string TestMapsPath = "/Maps/Test/";

    /// <summary>
    /// Asserts that specific files have been saved as grids and not maps.
    /// </summary>
    [Test, TestCaseSource(nameof(_grids))] // Starlight-edit
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task GridsLoadableTest(string mapFile)
    {
        var pair = Pair;
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapLoader = entManager.System<MapLoaderSystem>();
        var mapSystem = entManager.System<SharedMapSystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var path = new ResPath(mapFile);

        await server.WaitPost(() =>
        {
            mapSystem.CreateMap(out var mapId);
            try
            {
                Assert.That(mapLoader.TryLoadGrid(mapId, path, out var grid));
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load map {mapFile}, was it saved as a map instead of a grid?", ex);
            }

            mapSystem.DeleteMap(mapId);
        });
    }

    /// <summary>
    /// Asserts that shuttles are loadable and have been saved as grids and not maps.
    /// </summary>
    [Test]
    [TestCaseSource(nameof(_shuttleMapFiles))] // Starlight-edit
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task ShuttlesLoadableTest(ResPath path)
    {
        var pair = Pair;
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapLoader = entManager.System<MapLoaderSystem>();
        var mapSystem = entManager.System<SharedMapSystem>();

        // Starlight-start
        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                mapSystem.CreateMap(out var mapId);
                try
                {
                    Assert.That(mapLoader.TryLoadGrid(mapId, path, out _),
                        $"Failed to load shuttle {path}, was it saved as a map instead of a grid?");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to load shuttle {path}, was it saved as a map instead of a grid?",
                        ex);
                }
                mapSystem.DeleteMap(mapId);
            }
        });
        // Starlight-end
    }

    [Test]
    [TestCaseSource(nameof(_allMapFiles))]
    public async Task NoSavedPostMapInitTest(ResPath map)
    {
        var pair = Pair;
        var server = pair.Server;

        var resourceManager = server.ResolveDependency<IResourceManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var loader = server.System<MapLoaderSystem>();

        var rootedPath = map.ToRootedPath();

        var isV7Map = false;

        if (SkipTestMaps && rootedPath.ToString().StartsWith(TestMapsPath, StringComparison.Ordinal))
            return; // We just pass immediately.

        if (!resourceManager.TryContentFileRead(rootedPath, out var fileStream))
            Assert.Fail($"Map not found: {rootedPath}");

        using var reader = new StreamReader(fileStream);
        var yamlStream = new YamlStream();

        yamlStream.Load(reader);

        var root = yamlStream.Documents[0].RootNode;
        var meta = root["meta"];
        var version = meta["format"].AsInt();

        // TODO MAP TESTS
        // Move this to some separate test?
        CheckDoNotMap(map, root, protoManager);

        if (version >= 7)
        {
            isV7Map = true;
        }
        else
        {
            var postMapInit = meta["postmapinit"].AsBool();
            Assert.That(postMapInit, Is.False, $"Map {map.Filename} was saved postmapinit");
        }

        var deps = server.ResolveDependency<IEntitySystemManager>().DependencyCollection;
        var ev = new BeforeEntityReadEvent();
        server.EntMan.EventBus.RaiseEvent(EventSource.Local, ev);

        if (isV7Map)
            Assert.That(IsPreInit(map, loader, deps, ev.RenamedPrototypes, ev.DeletedPrototypes));

        // Check that the test actually does manage to catch post-init maps and isn't just blindly passing everything.
        // To that end, create a new post-init map and try verify it.
        var mapSys = server.System<SharedMapSystem>();
        MapId id = default;
        await server.WaitPost(() => mapSys.CreateMap(out id, runMapInit: false));
        await server.WaitPost(() => server.EntMan.Spawn(null, new MapCoordinates(0, 0, id)));

        // First check that a pre-init version passes
        var path = new ResPath($"{nameof(NoSavedPostMapInitTest)}.yml");
        // Starlight-start
        using (Assert.EnterMultipleScope())
        {
            Assert.That(loader.TrySaveMap(id, path));
            Assert.That(IsPreInit(path, loader, deps, ev.RenamedPrototypes, ev.DeletedPrototypes));
        }
        // Starlight-end

        // and the post-init version fails.
        await server.WaitPost(() => mapSys.InitializeMap(id));
        // Starlight-start
        using (Assert.EnterMultipleScope())
        {
            Assert.That(loader.TrySaveMap(id, path));
            Assert.That(IsPreInit(path, loader, deps, ev.RenamedPrototypes, ev.DeletedPrototypes), Is.False);
        }
        // Starlight-end

        await server.WaitPost(() => mapSys.DeleteMap(id)); // Starlight-edit
    }

    /// <summary>
    /// Check that maps do not have any entities that belong to the DoNotMap entity category
    /// </summary>
    private static void CheckDoNotMap(ResPath map, YamlNode node, IPrototypeManager protoManager) // Starlight-edit
    {
        foreach (var regex in _doNotMapWhiteListRegexes)
        {
            if (regex.IsMatch(map.ToString()))
                return;
        }

        var yamlEntities = node["entities"];
        var dnmCategory = protoManager.Index(_doNotMapCategory);

        // Make a set containing all the specific whitelisted proto ids for this map
        HashSet<EntProtoId> unusedExemptions = _doNotMapWhitelistSpecific.TryGetValue(map.ToString(), out var exemptions) ? new(exemptions) : [];
        // Starlight-start
        using (Assert.EnterMultipleScope())
        {
            foreach (var yamlEntity in (YamlSequenceNode)yamlEntities)
            {
                var protoId = yamlEntity["proto"].AsString();

                // This doesn't properly handle prototype migrations, but thats not a significant issue.
                if (!protoManager.TryIndex(protoId, out var proto))
                    continue;

                Assert.That(!proto.Categories.Contains(dnmCategory) || IsWhitelistedForMap(protoId, map),
                    $"\nMap {map} contains entities in the DO NOT MAP category: {proto.Name} ({proto.ID})"); // SL change, helps for debugging

                // The proto id is used on this map, so remove it from the set
                unusedExemptions.Remove(protoId);
            }
        }
        // Starlight-end

        // If there are any proto ids left, they must not have been used in the map!
        Assert.That(unusedExemptions, Is.Empty,
            $"Map {map} has DO NOT MAP entities whitelisted that are not present in the map: {string.Join(", ", unusedExemptions)}");
    }

    private static bool IsPreInit(ResPath map, // Starlight-edit
        MapLoaderSystem loader,
        IDependencyCollection deps,
        Dictionary<string, string> renamedPrototypes,
        HashSet<string> deletedPrototypes)
    {
        if (!loader.TryReadFile(map, out var data))
        {
            Assert.Fail($"Failed to read {map}");
            return false;
        }

        var reader = new EntityDeserializer(deps,
            data,
            DeserializationOptions.Default,
            renamedPrototypes,
            deletedPrototypes);

        if (!reader.TryProcessData())
        {
            Assert.Fail($"Failed to process {map}");
            return false;
        }

        foreach (var mapId in reader.MapYamlIds)
        {
            var mapData = reader.YamlEntities[mapId];
            if (mapData.PostInit)
                return false;
        }

        return true;
    }

    [Test, TestCaseSource(nameof(_gameMaps))] // Starlight-edit
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    [PairConfig(nameof(s_mapInitSettings))] // Starlight-edit
    public async Task GameMapsLoadableTest(string mapProto)
    {
        var pair = Pair;
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapLoader = entManager.System<MapLoaderSystem>();
        var mapSystem = entManager.System<SharedMapSystem>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var ticker = entManager.EntitySysManager.GetEntitySystem<GameTicker>();
        var shuttleSystem = entManager.EntitySysManager.GetEntitySystem<ShuttleSystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();

        //setup new stopwatch
        var sw = new Robust.Shared.Timing.Stopwatch();
        sw.Start();
        MapId mapId = default;

        await server.WaitPost(() =>
        {
            try
            {
                var opts = DeserializationOptions.Default with { InitializeMaps = true };
                ticker.LoadGameMap(protoManager.Index<GameMapPrototype>(mapProto), out mapId, opts);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load map {mapProto}", ex);
            }

            //output
            TestContext.Out.WriteLine($"{sw.Elapsed.TotalMilliseconds} ms: Loaded map {mapProto}");
        });

        // Starlight start: the APC load check used to live in StationPowerTests.TestApcLoad and loaded every map again
        await AssertNoOverloadedApcs(mapProto, mapId);
        TestContext.Out.WriteLine($"{sw.Elapsed.TotalMilliseconds} ms: Checked APC load on {mapProto}");
        // Starlight end

        await server.WaitPost(() =>
        {
            mapSystem.CreateMap(out var shuttleMap);
            var largest = 0f;
            EntityUid? targetGrid = null;
            var memberQuery = entManager.GetEntityQuery<StationMemberComponent>();

            var grids = mapSystem.GetAllGrids(mapId).ToList();
            var gridUids = grids.Select(o => o.Owner).ToList();
            targetGrid = gridUids.First();

            foreach (var grid in grids)
            {
                var gridEnt = grid.Owner;
                if (!memberQuery.HasComponent(gridEnt))
                    continue;

                var area = grid.Comp.LocalAABB.Width * grid.Comp.LocalAABB.Height;

                if (area > largest)
                {
                    largest = area;
                    targetGrid = gridEnt;
                }
            }

            TestContext.Out.WriteLine($"{sw.Elapsed.TotalMilliseconds} ms: Found {grids.Count} grids on {mapProto}, target is {targetGrid}");

            // Test shuttle can dock.
            // This is done inside gamemap test because loading the map takes ages and we already have it.
            var station = entManager.GetComponent<StationMemberComponent>(targetGrid!.Value).Station;
            if (entManager.TryGetComponent<StationEmergencyShuttleComponent>(station, out var stationEvac))
            {
                var shuttlePath = stationEvac.EmergencyShuttlePath;
                // Starlight-start
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(mapLoader.TryLoadGrid(shuttleMap, shuttlePath, out var shuttle),
                                    $"Failed to load {shuttlePath}");

                    Assert.That(
                        shuttleSystem.TryFTLDock(shuttle!.Value.Owner,
                            entManager.GetComponent<ShuttleComponent>(shuttle!.Value.Owner),
                            targetGrid.Value),
                        $"Unable to dock {shuttlePath} to {mapProto}");
                }
                // Starlight-end
            }

            TestContext.Out.WriteLine($"{sw.Elapsed.TotalMilliseconds} ms: Shuttle docked on {mapProto}");

            mapSystem.DeleteMap(shuttleMap);
            TestContext.Out.WriteLine($"{sw.Elapsed.TotalMilliseconds} ms: Deleted shuttle map on {mapProto}");

            // Starlight-start
            // Spawn ATS.
            // This is done inside gamemap test so that ATS spawns can be recognized.
            {
                var tradePath = new ResPath("/Maps/_Starlight/trading_outpost.yml");
                Assert.That(mapLoader.TryLoadGrid(mapId, tradePath, out var trade),
                    $"Failed to load {tradePath}");
            }

            TestContext.Out.WriteLine($"{sw.Elapsed.TotalMilliseconds} ms: ATS spawned on {mapProto}");
            // Starlight-end

            if (entManager.HasComponent<StationJobsComponent>(station))
            {
                // Test that the map has valid latejoin spawn points or container spawn points
                if (!_noSpawnMaps.Contains(mapProto)) // Starlight-edit
                {
                    var lateSpawns = 0;

                    lateSpawns += GetCountLateSpawn<SpawnPointComponent>(gridUids, entManager);
                    lateSpawns += GetCountLateSpawn<ContainerSpawnPointComponent>(gridUids, entManager);

                    Assert.That(lateSpawns, Is.GreaterThan(0), $"Found no latejoin spawn points on {mapProto}");
                }

                // Test all availableJobs have spawnPoints
                // This is done inside gamemap test because loading the map takes ages and we already have it.
                var comp = entManager.GetComponent<StationJobsComponent>(station);
                var jobs = new HashSet<ProtoId<JobPrototype>>(comp.SetupAvailableJobs.Keys);

                var spawnPoints = entManager.EntityQuery<SpawnPointComponent>()
                    .Where(x => x.SpawnType == SpawnPointType.Job && x.Job != null)
                    .Select(x => x.Job.Value);

                jobs.ExceptWith(spawnPoints);

                spawnPoints = entManager.EntityQuery<ContainerSpawnPointComponent>()
                    .Where(x => x.SpawnType is SpawnPointType.Job or SpawnPointType.Unset && x.Job != null)
                    .Select(x => x.Job.Value);

                jobs.ExceptWith(spawnPoints);

                Assert.That(jobs, Is.Empty, $"There is no spawnpoints for {string.Join(", ", jobs)} on {mapProto}.");
            }

            TestContext.Out.WriteLine($"{sw.Elapsed.TotalMilliseconds} ms: Validated spawns on {mapProto}");

            try
            {
                mapSystem.DeleteMap(mapId);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to delete map {mapProto}", ex);
            }

            TestContext.Out.WriteLine($"{sw.Elapsed.TotalMilliseconds} ms: Deleted map {mapProto}");
        });

    }

    private static int GetCountLateSpawn<T>(List<EntityUid> gridUids, IEntityManager entManager)
        where T : ISpawnPoint, IComponent
    {
        var resultCount = 0;
        var queryPoint = entManager.AllEntityQueryEnumerator<T, TransformComponent>();
#nullable enable
        while (queryPoint.MoveNext(out var comp, out var xform)) // Starlight-edit
        {
            var spawner = (ISpawnPoint)comp;

            if (spawner.SpawnType is not SpawnPointType.LateJoin
            || xform.GridUid == null
            || !gridUids.Contains(xform.GridUid.Value))
            {
                continue;
            }
#nullable disable
            resultCount++;
            break;
        }

        return resultCount;
    }

    [Test]
    [TestCaseSource(nameof(_allMapFiles))] // Starlight-edit
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    [PairConfig(nameof(s_mapInitSettings))] // Starlight-edit
    public async Task NonGameMapsLoadableTest(ResPath mapPath)
    {
        var pair = Pair;
        var server = pair.Server;

        var mapLoader = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<MapLoaderSystem>();
        var resourceManager = server.ResolveDependency<IResourceManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();

        var gameMaps = protoManager.EnumeratePrototypes<GameMapPrototype>().Select(o => o.MapPath).ToHashSet();

        if (gameMaps.Contains(mapPath))
        {
            // TODO: You might be able to save like, 1-2 seconds of test time if you eliminate these before
            //       actually needing a pair.
            return;
        }

        var rootedPath = mapPath.ToRootedPath();

        if (SkipTestMaps && rootedPath.ToString().StartsWith(TestMapsPath, StringComparison.Ordinal))
        {
            return;
        }

        // Starlight-start
        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                // This bunch of files contains a random mixture of both map and grid files.
                // TODO MAPPING organize files
                var opts = MapLoadOptions.Default with
                {
                    DeserializationOptions = DeserializationOptions.Default with
                    {
                        InitializeMaps = true,
                        LogOrphanedGrids = false
                    }
                };

                HashSet<Entity<MapComponent>> maps;

                try
                {
                    Assert.That(mapLoader.TryLoadGeneric(mapPath, out maps, out _, opts));
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to load map {mapPath}", ex);
                }

                try
                {
                    foreach (var map in maps)
                    {
                        server.EntMan.DeleteEntity(map);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to delete map {mapPath}", ex);
                }
            }
        });
        // Starlight-end
    }

    /// <summary>
    /// Lets us the convert the filepaths to regex without eyeglaze trying to add new paths.
    /// </summary>
    private static string GlobToRegex(string glob)
    {
        var regex = Regex.Escape(glob)
            .Replace(@"\*\*", "**") // replace **
            .Replace(@"\*", "*")    // replace *
            .Replace("**", ".*")    // ** → match across folders
            .Replace("*", @"[^/]*") // * → match within a single folder
            .Replace(@"\?", ".");   // ? → any single character

        return $"^{regex}$";
    }

    #region Starlight

    private static bool IsWhitelistedForMap(EntProtoId protoId, ResPath map)
        => _doNotMapWhitelistSpecific.TryGetValue(map.ToString(), out var allowedProtos) && allowedProtos.Contains(protoId);

    /// <summary>
    /// Lets power ramp up on a freshly loaded station and checks that no APC on it starts overloaded.
    /// </summary>
    private async Task AssertNoOverloadedApcs(string mapProto, MapId mapId)
    {
        var entMan = Server.EntMan;
        var xform = entMan.System<TransformSystem>();

        // Wait long enough for power to ramp up, but before anything can trip
        await Pair.RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            var apcQuery = entMan.EntityQueryEnumerator<ApcComponent, PowerNetworkBatteryComponent, TransformComponent>();
            using (Assert.EnterMultipleScope())
            {
                var apcCount = 0;
                while (apcQuery.MoveNext(out var uid, out var apc, out var battery, out var apcXform))
                {
                    apcCount++;
                    if (apcXform.MapID != mapId)
                        continue;

                    var where = xform.TryGetMapOrGridCoordinates(uid, out var coord)
                        ? $" ({coord.Value.X}, {coord.Value.Y})"
                        : string.Empty;
                    Assert.That(apc.MaxLoad, Is.GreaterThanOrEqualTo(battery.CurrentSupply),
                        $"APC {uid} on {mapProto}{where} is overloaded {battery.CurrentSupply} / {apc.MaxLoad}");
                }

                Assert.That(apcCount, Is.GreaterThan(0), $"There's no APCs found on {mapProto}!!!");
            }
        });
    }
    #endregion
}
