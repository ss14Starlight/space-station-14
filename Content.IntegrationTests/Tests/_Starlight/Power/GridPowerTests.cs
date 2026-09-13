using Content.IntegrationTests.Fixtures;
using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Starlight.Power;

[Parallelizable(ParallelScope.All)]
public sealed class GridPowerTests : GameTest
{
    private const string EmptyMap = "Empty";

    private static readonly ResPath[] _gridPaths =
    [
        // NT-CC
        new("/Maps/_Starlight/Shuttles/CC-NT/CBURN.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/CBURN_Q.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/Chaplain_GRID.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/Decimus_Shuttle_Magnus.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/DSshuttle.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/EngiAtmos_GRID.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/ERTShuttle.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/GammaWeaponry.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/Janitor_GRID.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/Medical_GRID.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/NSSV_MetaClass.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/NT-Experimental-Botany-shuttle.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/NTSF_Minos_Battlecruiser.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/NTSV_HarrierClass.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/PsiArmory.yml"),
        new("/Maps/_Starlight/Shuttles/CC-NT/SecMed_GRID.yml"),

        // Secure Terminal
        new("/Maps/_Starlight/Shuttles/SecureTerminal/ERT-Full.yml"),
        new("/Maps/_Starlight/Shuttles/SecureTerminal/ERT-Small-Engi.yml"),
        new("/Maps/_Starlight/Shuttles/SecureTerminal/ERT-Small-Jani.yml"),
        new("/Maps/_Starlight/Shuttles/SecureTerminal/ERT-Small-Med.yml"),
        new("/Maps/_Starlight/Shuttles/SecureTerminal/ERT-Small-Sec.yml"),

        // Security
        new("/Maps/_Starlight/Shuttles/Security/oasis_briggle.yml"),
        new("/Maps/_Starlight/Shuttles/Security/pursuit.yml"),
        new("/Maps/_Starlight/Shuttles/Security/security_prism.yml"),

        // Salvage
        new("/Maps/_Starlight/Ruins/Salv_Sus.yml"),
        new("/Maps/_Starlight/Salvage/Salv_Cargo_01.yml"),
        new("/Maps/_Starlight/Salvage/Salv_Cargo_02.yml"),
        new("/Maps/_Starlight/Shuttles/Salvage/expeditioneer.yml"),

        // Mining
        new("/Maps/_Starlight/Shuttles/Mining/asteroidcracker.yml"),

        // Cargo
        new("/Maps/_Starlight/Shuttles/Cargo/cargo_plasma.yml"),
        new("/Maps/_Starlight/Shuttles/Cargo/cargo_prism.yml"),
        new("/Maps/_Starlight/Shuttles/Cargo/cargo_silica.yml"),
        new("/Maps/_Starlight/Shuttles/Cargo/cargo_syndicate.yml"),
        new("/Maps/_Starlight/Shuttles/Cargo/cargo_novolobster.yml"),

        // Evac
        new("/Maps/_Starlight/Shuttles/Evac/emergency_cluster.yml"),
        new("/Maps/_Starlight/Shuttles/Evac/emergency_delta.yml"),
        new("/Maps/_Starlight/Shuttles/Evac/emergency_hotel.yml"),
        new("/Maps/_Starlight/Shuttles/Evac/emergency_lox.yml"),
        new("/Maps/_Starlight/Shuttles/Evac/emergency_manor.yml"),
        new("/Maps/_Starlight/Shuttles/Evac/emergency_ming.yml"),
        new("/Maps/_Starlight/Shuttles/Evac/emergency_prism.yml"),
        new("/Maps/_Starlight/Shuttles/Evac/emergency_raven.yml"),
        new("/Maps/_Starlight/Shuttles/Evac/emergency_silica.yml"),
        new("/Maps/_Starlight/Shuttles/Evac/emergency_spacemall.yml"),
        new("/Maps/_Starlight/Shuttles/Evac/emergency_starboard.yml"),
        new("/Maps/_Starlight/Shuttles/Evac/emergency_syndicate.yml"),

        // Shipyard
        new("/Maps/_Starlight/Shuttles/Shipyard/barge.yml"),
        new("/Maps/_Starlight/Shuttles/Shipyard/breaker.yml"),
        new("/Maps/_Starlight/Shuttles/Shipyard/Bumblebee.yml"),
        new("/Maps/_Starlight/Shuttles/Shipyard/Comet.yml"),
        new("/Maps/_Starlight/Shuttles/Shipyard/GasTransport.yml"),
        new("/Maps/_Starlight/Shuttles/Shipyard/Honeybee.yml"),
        new("/Maps/_Starlight/Shuttles/Shipyard/JSS_MED_Apotherkerin.yml"),
        new("/Maps/_Starlight/Shuttles/Shipyard/Mini_Ingeniator.yml"),
        new("/Maps/_Starlight/Shuttles/Shipyard/Munchies.yml"),
        new("/Maps/_Starlight/Shuttles/Shipyard/pioneer.yml"),
        new("/Maps/_Starlight/Shuttles/Shipyard/prospector.yml"),
        new("/Maps/_Starlight/Shuttles/Shipyard/pts.yml"),
        new("/Maps/_Starlight/Shuttles/Shipyard/SpaceTruck.yml"),

        // Syndicate
        new("/Maps/_Starlight/Shuttles/Nukeops/blackhorse.yml"),
        new("/Maps/_Starlight/Shuttles/Nukeops/widow.yml"),
        new("/Maps/_Starlight/Shuttles/Nukeops/omen.yml"),
        new("/Maps/_Starlight/Shuttles/Nukeops/leyline.yml"),
        new("/Maps/_Starlight/Shuttles/ShuttleEvent/syndie_evacpod.yml"),

        // Other Antagonists
        new("/Maps/_Starlight/Shuttles/mothership.yml"), // Xenoborgs
        new("/Maps/_Starlight/Shuttles/ShuttleEvent/abductor_shuttle.yml"), // Abductors

        // Events / Admemes
        new("/Maps/_Starlight/Shuttles/ShuttleEvent/ShadowBorgiGrid.yml"),
        new("/Maps/_Starlight/Shuttles/ShuttleEvent/UnknownShuttleFireResponse.yml"),
        new("/Maps/_Starlight/Shuttles/ShuttleEvent/incorporation.yml"),
        new("/Maps/_Starlight/Shuttles/ShuttleEvent/montague.yml"),
        new("/Maps/_Starlight/Shuttles/ShuttleEvent/romeo.yml"),
        new("/Maps/_Starlight/Shuttles/ShuttleEvent/VisitorInquisitor.yml"),

        new("/Maps/_Starlight/Shuttles/Admeme/LancePirates.yml"),
        new("/Maps/_Starlight/Shuttles/Admeme/lotteryShuttleAdmeme.yml"),
        new("/Maps/_Starlight/Shuttles/Admeme/quantum_ark.yml"),
        new("/Maps/_Starlight/Shuttles/Admeme/quantum_ark_event.yml"),
        new("/Maps/_Starlight/Shuttles/Admeme/Radiotower.yml"),
        new("/Maps/_Starlight/Shuttles/Admeme/RecluseClassSHC.yml"),
        new("/Maps/_Starlight/Shuttles/Admeme/scarletSHCdefenderFinal.yml"),
        new("/Maps/_Starlight/Shuttles/Admeme/Signaleer.yml"),
        new("/Maps/_Starlight/Shuttles/Admeme/SmugglerMex.yml"),
        new("/Maps/_Starlight/Shuttles/Admeme/ss_ana.yml"),
        new("/Maps/_Starlight/Shuttles/Admeme/VoxATS.yml"),

        new("/Maps/_Starlight/MedTak/MedTak-AV-40.yml"),
        new("/Maps/_Starlight/MedTak/MedTakPointAlpha.yml"),

        new("/Maps/_Starlight/Test/SL_admin_test_arena.yml"),
    ];

    [Test, TestCaseSource(nameof(_gridPaths))]
    public async Task TestGridApcLoad(ResPath gridFilePath)
    {
        var pair = Pair;
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
    }
}
