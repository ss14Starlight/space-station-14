using System.Collections.Generic;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Server.Power.NodeGroups;
using Content.Server.Power.Pow3r;
using Content.Shared.Maps;
using Content.Shared.NodeContainer;
using Content.Shared.Power.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Starlight.Power;

public sealed class GridPowerTests
{
    private const string EmptyMap = "Empty";

    private static readonly ResPath[] GridPaths =
    [
        new("/Maps/_Starlight/Ruins/Salv_Sus.yml"),
        new("/Maps/_Starlight/Salvage/Salv_Cargo_01.yml"),
        new("/Maps/_Starlight/Salvage/Salv_Cargo_02.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/CBURN.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/CBURN_Q.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/Chaplain_GRID.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/DSshuttle.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/ERTShuttle.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/EngiAtmos_GRID.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/GammaWeaponry.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/Janitor_GRID.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/Medical_GRID.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/PsiArmory.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/SecMed_GRID.yml"),
        new("/Maps/_Starlight/Shuttles/KillerTamashi-SecShuttle.yml"),
        new("/Maps/_Starlight/Shuttles/LancePirates.yml"),
        new("/Maps/_Starlight/Shuttles/LynatiKr20/SmugglerMex.yml"),
        new("/Maps/_Starlight/Shuttles/Mining/breaker.yml"),
        new("/Maps/_Starlight/Shuttles/NSSV_MetaClass.yml"),
        new("/Maps/_Starlight/Shuttles/NT-Experimental-Botany-shuttle.yml"),
        new("/Maps/_Starlight/Shuttles/NTSV_HarrierClass.yml"),
        new("/Maps/_Starlight/Shuttles/Radiotower.yml"),
        new("/Maps/_Starlight/Shuttles/Reach.yml"),
        new("/Maps/_Starlight/Shuttles/RecluseClassSHC.yml"),
        new("/Maps/_Starlight/Shuttles/Salvage/pioneer.yml"),
        new("/Maps/_Starlight/Shuttles/Salvage/pioneer_fuel.yml"),
        new("/Maps/_Starlight/Shuttles/Security/SP-4C3.yml"),
        new("/Maps/_Starlight/Shuttles/ShuttleEvent/ShadowBorgiGrid.yml"),
        new("/Maps/_Starlight/Shuttles/ShuttleEvent/UnknownShuttleFireResponse.yml"),
        new("/Maps/_Starlight/Shuttles/ShuttleEvent/abductor_shuttle.yml"),
        new("/Maps/_Starlight/Shuttles/Signaleer.yml"),
        new("/Maps/_Starlight/Shuttles/VoxATS.yml"),
        new("/Maps/_Starlight/Shuttles/blackhorse.yml"),
        new("/Maps/_Starlight/Shuttles/cargo_plasma.yml"),
        new("/Maps/_Starlight/Shuttles/cargo_prism.yml"),
        new("/Maps/_Starlight/Shuttles/cargo_silica.yml"),
        new("/Maps/_Starlight/Shuttles/emergency_cluster.yml"),
        new("/Maps/_Starlight/Shuttles/emergency_delta.yml"),
        new("/Maps/_Starlight/Shuttles/emergency_manor.yml"),
        new("/Maps/_Starlight/Shuttles/emergency_ming.yml"),
        new("/Maps/_Starlight/Shuttles/emergency_prism.yml"),
        new("/Maps/_Starlight/Shuttles/emergency_silica.yml"),
        new("/Maps/_Starlight/Shuttles/emergency_spacemall.yml"),
        new("/Maps/_Starlight/Shuttles/emergency_starboard.yml"),
        new("/Maps/_Starlight/Shuttles/lotteryShuttleAdmeme.yml"),
        new("/Maps/_Starlight/Shuttles/quantum_ark.yml"),
        new("/Maps/_Starlight/Shuttles/quantum_ark_event.yml"),
        new("/Maps/_Starlight/Shuttles/scarletSHCdefenderFinal.yml"),
        new("/Maps/_Starlight/Shuttles/sec_patrol_one.yml"),
        new("/Maps/_Starlight/Shuttles/sec_patrol_two.yml"),
        new("/Maps/_Starlight/Shuttles/security_prism.yml"),
        new("/Maps/_Starlight/Shuttles/oasis_briggle.yml"),
        new("/Maps/_Starlight/Shuttles/ss_ana.yml"),
        new("/Maps/_Starlight/Test/SL_admin_test_arena.yml"),
        new("/Maps/_Starlight/Shuttles/mothership.yml"),
    ];

    [Test, TestCaseSource(nameof(GridPaths))]
    public async Task TestGridApcLoad(ResPath gridFilePath)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { });
        var server = pair.Server;

        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var ticker = entMan.System<GameTicker>();
        var xform = entMan.System<TransformSystem>();
        var loader = entMan.System<MapLoaderSystem>();
        var mapSystem = entMan.System<MapSystem>();

        MapId mapId = MapId.Nullspace;

        // Load the map and grid
        await server.WaitAssertion(() =>
        {
            Assert.That(protoMan.TryIndex<GameMapPrototype>(EmptyMap, out var mapProto));
            var opts = DeserializationOptions.Default with { InitializeMaps = true };
            ticker.LoadGameMap(mapProto, out mapId, opts);
            var loadedGrid = loader.TryLoadGrid(mapId, gridFilePath, out var grid);
            Assert.That(loadedGrid, "Failed to load grid");
        });

        // Wait long enough for power to ramp up, but before anything can trip
        await pair.RunSeconds(2);

        // Check that no APCs start overloaded
        var apcQuery = entMan.EntityQueryEnumerator<ApcComponent, PowerNetworkBatteryComponent>();
        Assert.Multiple(() =>
        {
            while (apcQuery.MoveNext(out var uid, out var apc, out var battery))
            {
                // Uncomment the following line to log starting APC load to the console
                //Console.WriteLine($"ApcLoad:{gridFilePath}:{uid}:{battery.CurrentSupply}");
                if (xform.TryGetMapOrGridCoordinates(uid, out var coord))
                {
                    Assert.That(apc.MaxLoad, Is.GreaterThanOrEqualTo(battery.CurrentSupply),
                            $"APC {uid} on {gridFilePath} ({coord.Value.X}, {coord.Value.Y}) is overloaded {battery.CurrentSupply} / {apc.MaxLoad}");
                }
                else
                {
                    Assert.That(apc.MaxLoad, Is.GreaterThanOrEqualTo(battery.CurrentSupply),
                            $"APC {uid} on {gridFilePath} is overloaded {battery.CurrentSupply} / {apc.MaxLoad}");
                }
            }
        });

        await server.WaitAssertion(() =>
        {
            if (mapId != MapId.Nullspace)
                mapSystem.DeleteMap(mapId!);
        });

        await pair.CleanReturnAsync();
    }
}
