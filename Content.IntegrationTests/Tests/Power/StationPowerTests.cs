using System.Collections.Generic;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.NodeGroups;
using Content.Server.Power.Pow3r;
using Content.Shared.Maps;
using Content.Shared.Power.Components;
using Content.Shared.NodeContainer;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Power;

public sealed class StationPowerTests
{
    /// <summary>
    /// How long the station should be able to survive on stored power if nothing is changed from round start.
    /// </summary>
    private const float MinimumPowerDurationSeconds = 10 * 60;

    // Starlight
    private const float MaximumPowerDurationSeconds = 12.5f * 60;

    private static readonly string[] GameMaps =
    [
        // Starlight-gripe - why does upstream replicate their map list here, when they already have it for other tests?
        /* Starlight-comment start - we removed these maps from our repo to save on size
        "Bagel",
        "Box",
        "Elkridge",
        "Fland",
        "Marathon",
        "Oasis",
        "Packed",
        "Plasma",
        "Relic",
        "Snowball",
        "Reach",
        "Exo",
        */// Starlight-comment end
        //Starlight, do not accept any upstream maps into this list, we are keeping them out for package size and just general management reasons
        #region Starlight
        "StarlightBarratry",
        "StarlightCork",
        "StarlightKiloton",
        "StarlightLagan",
        "StarlightLobster",
        "StarlightManor",
        "StarlightLeth",
        "StarlightMing",
        "StarlightOrwell",
        "StarlightPrism",
        "StarlightStarboard",
        "StarlightBagel",
        "StarlightBox",
        "StarlightCog",
        "StarlightElkridge",
        "StarlightFland",
        "StarlightHotel",
        "StarlightOasis",
        "StarlightPacked",
        "StarlightReach",
        "StarlightSaltern",
        "StarlightSilica",
        "StarlightSpaceMall",
        "StarlightCluster",
        "StarlightStationBuilding",
        "StarlightPlasma",
        "StarlightSepultum",
        "StarlightBoxcars"
        #endregion
    ];

    [Explicit]
    [Test, TestCaseSource(nameof(GameMaps))]
    public async Task TestStationStartingPowerWindow(string mapProtoId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });
        var server = pair.Server;

        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var ticker = entMan.System<GameTicker>();
        var batterySys = entMan.System<BatterySystem>();

        // Load the map
        await server.WaitAssertion(() =>
        {
            Assert.That(protoMan.TryIndex<GameMapPrototype>(mapProtoId, out var mapProto));
            var opts = DeserializationOptions.Default with { InitializeMaps = true };
            ticker.LoadGameMap(mapProto, out var mapId, opts);
        });

        // Let powernet set up
        await server.WaitRunTicks(1);

        // Starlight start - track grid per network to filter cross-grid load
        var networks = new Dictionary<PowerState.Network, (float Charge, int BatteryCount, EntityUid Grid)>();
        var batteryQuery = entMan.EntityQueryEnumerator<PowerNetworkBatteryComponent, BatteryComponent, NodeContainerComponent>();
        while (batteryQuery.MoveNext(out var uid, out _, out var battery, out var nodeContainer))
        {
            if (!nodeContainer.Nodes.TryGetValue("output", out var node))
                continue;
            if (node.NodeGroup is not IBasePowerNet group)
                continue;
            networks.TryGetValue(group.NetworkNode, out var entry);
            var currentCharge = batterySys.GetCharge((uid, battery));
            var grid = entMan.GetComponent<TransformComponent>(uid).GridUid ?? EntityUid.Invalid;
            networks[group.NetworkNode] = (entry.Charge + currentCharge, entry.BatteryCount + 1, grid);
        }
        var maxNetwork   = networks.MaxBy(n => n.Value.Charge);
        var totalStartingCharge = maxNetwork.Value.Charge;
        var stationGrid  = maxNetwork.Value.Grid; // grid of the biggest network = station grid
        // Starlight end

        // Find how much charge all the APC-connected devices would like to use per second.
        // Starlight: only receivers on the station grid that are connected to an APC provider.
        var totalAPCLoad = 0f;
        var receiverQuery = entMan.EntityQueryEnumerator<ApcPowerReceiverComponent, TransformComponent>();
        while (receiverQuery.MoveNext(out _, out var receiver, out var rxf))
        {
            if (receiver.Provider == null)
                continue;
            if ((rxf.GridUid ?? EntityUid.Invalid) != stationGrid)
                continue;
            totalAPCLoad += receiver.Load;
        }
        // Starlight start - statistics
        var gridStats = new Dictionary<EntityUid, (float Load, float Charge, int ReceiverCount, int BatteryCount)>();

        var receiverStatQuery = entMan.EntityQueryEnumerator<ApcPowerReceiverComponent, TransformComponent>();
        while (receiverStatQuery.MoveNext(out _, out var recv, out var xf))
        {
            var grid = xf.GridUid ?? EntityUid.Invalid;
            gridStats.TryGetValue(grid, out var s);
            gridStats[grid] = (s.Load + recv.Load, s.Charge, s.ReceiverCount + 1, s.BatteryCount);
        }

        var battStatQuery = entMan.EntityQueryEnumerator<PowerNetworkBatteryComponent, BatteryComponent, NodeContainerComponent>();
        while (battStatQuery.MoveNext(out var uid, out _, out var bat, out var nodeContainer))
        {
            if (!nodeContainer.Nodes.TryGetValue("output", out var node))
                continue;
            if (node.NodeGroup is not IBasePowerNet)
                continue;
            var grid = entMan.GetComponent<TransformComponent>(uid).GridUid ?? EntityUid.Invalid;
            gridStats.TryGetValue(grid, out var s);
            gridStats[grid] = (s.Load, s.Charge + batterySys.GetCharge((uid, bat)), s.ReceiverCount, s.BatteryCount + 1);
        }

        var totLoad = 0f;
        var totCharge = 0f;
        var totRcvrs = 0;
        var totBatts = 0;

        Console.WriteLine($"\n=== Grid Power Stats: {mapProtoId} ===");
        Console.WriteLine($"  {"Grid",-8} | {"Rcvrs",5} | {"Batts",5} | {"Load (W)",12} | {"Charge (J)",12} | {"Est. dur (s)",12}");
        Console.WriteLine($"  {new string('-', 72)}");
        foreach (var (grid, s) in gridStats.OrderByDescending(kv => kv.Value.Load))
        {
            var estDur = s.Load > 0f ? s.Charge / s.Load : float.PositiveInfinity;
            var marker = grid == stationGrid ? "*" : " ";
            var label  = grid == EntityUid.Invalid ? "none" : $"{grid}";
            Console.WriteLine($"{marker} {label,-8} | {s.ReceiverCount,5} | {s.BatteryCount,5} | {s.Load,12:F0} | {s.Charge,12:F0} | {estDur,12:F0}");
            totLoad   += s.Load;
            totCharge += s.Charge;
            totRcvrs  += s.ReceiverCount;
            totBatts  += s.BatteryCount;
        }
        Console.WriteLine($"  {new string('-', 72)}");
        var totEstDur = totLoad > 0f ? totCharge / totLoad : float.PositiveInfinity;
        Console.WriteLine($"  {"Total",-8} | {totRcvrs,5} | {totBatts,5} | {totLoad,12:F0} | {totCharge,12:F0} | {totEstDur,12:F0}");

        // --- Per power-network breakdown ---
        Console.WriteLine($"\n--- Power networks (sorted by charge, * = selected by test) ---");
        Console.WriteLine($"  {"#",-4} | {"Grid",8} | {"Batts",5} | {"Charge (J)",12} | {"Load (W)",12} | {"MaxSup (W)",12}");
        Console.WriteLine($"  {new string('-', 70)}");
        var netIndex = 0;
        foreach (var (net, entry) in networks.OrderByDescending(kv => kv.Value.Charge))
        {
            var marker    = entry.Charge == totalStartingCharge ? "*" : " ";
            var gridLabel = entry.Grid == EntityUid.Invalid ? "none" : $"{entry.Grid}";
            Console.WriteLine($"{marker} {netIndex,3} | {gridLabel,8} | {entry.BatteryCount,5} | {entry.Charge,12:F0} | {net.LastCombinedLoad,12:F0} | {net.LastCombinedMaxSupply,12:F0}");
            netIndex++;
        }

        // --- Connection status + top consumers ---
        var connLoad = 0f; var connCount = 0;
        var disconnLoad = 0f; var disconnCount = 0;
        var noNeedLoad = 0f; var noNeedCount = 0;

        var protoLoad = new Dictionary<string, (float Load, int ConnCount, int DisconnCount)>();

        var diagQuery = entMan.EntityQueryEnumerator<ApcPowerReceiverComponent, MetaDataComponent>();
        while (diagQuery.MoveNext(out _, out var recv, out var meta))
        {
            var proto = meta.EntityPrototype?.ID ?? "(no prototype)";
            protoLoad.TryGetValue(proto, out var p);

            if (!recv.NeedsPower)
            {
                noNeedLoad += recv.Load;
                noNeedCount++;
                protoLoad[proto] = (p.Load + recv.Load, p.ConnCount, p.DisconnCount);
                continue;
            }

            if (recv.Provider != null)
            {
                connLoad += recv.Load;
                connCount++;
                protoLoad[proto] = (p.Load + recv.Load, p.ConnCount + 1, p.DisconnCount);
            }
            else
            {
                disconnLoad += recv.Load;
                disconnCount++;
                protoLoad[proto] = (p.Load + recv.Load, p.ConnCount, p.DisconnCount + 1);
            }
        }

        Console.WriteLine($"\n--- Receiver connection status ---");
        Console.WriteLine($"  {"Status",-30} | {"Count",5} | {"Load (W)",12}");
        Console.WriteLine($"  {new string('-', 55)}");
        Console.WriteLine($"  {"Connected   (Provider != null)",-30} | {connCount,5} | {connLoad,12:F0}");
        Console.WriteLine($"  {"Unconnected (Provider == null)",-30} | {disconnCount,5} | {disconnLoad,12:F0}");
        Console.WriteLine($"  {"NeedsPower=false (ignored)",-30} | {noNeedCount,5} | {noNeedLoad,12:F0}");

        Console.WriteLine($"\n--- Top 20 consumers by prototype (NeedsPower=true) ---");
        Console.WriteLine($"  {"Prototype",-45} | {"Con",5} | {"Dis",5} | {"Load (W)",12} | {"Avg (W)",9}");
        Console.WriteLine($"  {new string('-', 85)}");
        foreach (var (proto, p) in protoLoad.OrderByDescending(kv => kv.Value.Load).Take(20))
        {
            var total = p.ConnCount + p.DisconnCount;
            var avg = total > 0 ? p.Load / total : 0f;
            Console.WriteLine($"  {proto,-45} | {p.ConnCount,5} | {p.DisconnCount,5} | {p.Load,12:F0} | {avg,9:F0}");
        }
        // Starlight end

        var estimatedDuration = totalStartingCharge / totalAPCLoad;
        var requiredStoredPower = totalAPCLoad * MinimumPowerDurationSeconds;
        var maximumStoredPower  = totalAPCLoad * MaximumPowerDurationSeconds; // Starlight
        Assert.Multiple(() =>
        {
            Assert.That(estimatedDuration, Is.GreaterThanOrEqualTo(MinimumPowerDurationSeconds),
                $"Initial power for {mapProtoId} does not last long enough! Needs at least {MinimumPowerDurationSeconds}s " +
                $"but estimated to last only {estimatedDuration}s!");
            Assert.That(totalStartingCharge, Is.GreaterThanOrEqualTo(requiredStoredPower),
                $"Needs at least {requiredStoredPower - totalStartingCharge} more stored power!");
            // Starlight start
            Assert.That(estimatedDuration, Is.LessThanOrEqualTo(MaximumPowerDurationSeconds),
                $"Initial power for {mapProtoId} lasts too long! Max allowed {MaximumPowerDurationSeconds}s " +
                $"but estimated to last {estimatedDuration}s — remove some stored power!");
            Assert.That(totalStartingCharge, Is.LessThanOrEqualTo(maximumStoredPower),
                $"Has {totalStartingCharge - maximumStoredPower} too much stored power!");
            // Starlight end
        });

        await pair.CleanReturnAsync();
    }

    [Test, TestCaseSource(nameof(GameMaps))]
    public async Task TestApcLoad(string mapProtoId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });
        var server = pair.Server;

        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var ticker = entMan.System<GameTicker>();
        var xform = entMan.System<TransformSystem>();

        // Load the map
        await server.WaitAssertion(() =>
        {
            Assert.That(protoMan.TryIndex<GameMapPrototype>(mapProtoId, out var mapProto));
            var opts = DeserializationOptions.Default with { InitializeMaps = true };
            ticker.LoadGameMap(mapProto, out var mapId, opts);
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
                //Console.WriteLine($"ApcLoad:{mapProtoId}:{uid}:{battery.CurrentSupply}");
                if (xform.TryGetMapOrGridCoordinates(uid, out var coord))
                {
                    Assert.That(apc.MaxLoad, Is.GreaterThanOrEqualTo(battery.CurrentSupply),
                            $"APC {uid} on {mapProtoId} ({coord.Value.X}, {coord.Value.Y}) is overloaded {battery.CurrentSupply} / {apc.MaxLoad}");
                }
                else
                {
                    Assert.That(apc.MaxLoad, Is.GreaterThanOrEqualTo(battery.CurrentSupply),
                            $"APC {uid} on {mapProtoId} is overloaded {battery.CurrentSupply} / {apc.MaxLoad}");
                }
            }
        });

        await pair.CleanReturnAsync();
    }
}
