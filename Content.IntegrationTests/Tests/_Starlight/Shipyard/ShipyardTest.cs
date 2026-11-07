using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Utility;
using Content.Server._Starlight.Shipyard.Systems;
using Content.Server.Cargo.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Shipyard.Events;
using Content.Shared._Starlight.Shipyard.Prototypes;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Shuttles.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests._Starlight.Shipyard;

[TestOf(typeof(ShipyardSystem))]
[TestOf(typeof(ShipyardConsoleSystem))]
public sealed class ShipyardTest : GameTest
{
    private static readonly string[] _vessels = GameDataScrounger.PrototypesOfKind<VesselPrototype>();

    private const string ConsoleProto = "ComputerShipyard";
    private const string StationProto = "SLShipyardTestStation";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: SLShipyardTestStation
  parent: [ BaseStation, BaseStationCargo ]
";

    private static PoolSettings s_purchaseSettings => new() { Connected = true, Dirty = true };

    private static readonly HashSet<string> _knownUnderpriced =
    [
        "PTS",          // 21200 (~23900)
        "Ingeniator",   // 85000 (~254200)
        "Munchies",     // 38250 (~40800)
        "CargoGas",     // 21500 (~27400)
        "Pioneer",      // 39200 (~40300)
        "Honeybee",     // 30000 (~45600)
        "Apotherkerin", // 40000 (~58900)
        "Bumblebee",    // 60000 (~102700)
        "SpaceTruck",   // 14999 (~25000)
    ];

    [Test, TestCaseSource(nameof(_vessels))]
    public async Task VesselIsBuyableShuttle(string vesselId)
    {
        var mapLoader = Server.System<MapLoaderSystem>();
        var mapSystem = Server.System<SharedMapSystem>();
        var pricing = Server.System<PricingSystem>();

        var vessel = SProtoMan.Index<VesselPrototype>(vesselId);
        var loaded = false;
        var isShuttle = false;
        var appraisal = 0.0;

        await Server.WaitPost(() =>
        {
            mapSystem.CreateMap(out var mapId);
            try
            {
                loaded = mapLoader.TryLoadGrid(mapId, vessel.ShuttlePath, out var grid);
                if (!loaded)
                    return;

                isShuttle = SEntMan.HasComponent<ShuttleComponent>(grid!.Value.Owner);
                appraisal = pricing.AppraiseGrid(grid.Value.Owner);
            }
            finally
            {
                mapSystem.DeleteMap(mapId);
            }
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(vessel.Price, Is.GreaterThan(0), $"Vessel {vesselId} has no price.");
            Assert.That(vessel.Delay, Is.GreaterThanOrEqualTo(0f), $"Vessel {vesselId} has a negative arrival delay.");
            Assert.That(loaded, $"Vessel {vesselId} failed to load {vessel.ShuttlePath} as a grid.");
            Assert.That(isShuttle, $"Vessel {vesselId} ({vessel.ShuttlePath}) has no {nameof(ShuttleComponent)} and could never fly in.");
        }

        if (_knownUnderpriced.Contains(vesselId))
        {
            TestContext.Out.WriteLine($"Vessel {vesselId} is a known arbitrage: costs {vessel.Price}, contents appraise for {appraisal:F0}.");
            return;
        }

        Assert.That(appraisal, Is.LessThanOrEqualTo(vessel.Price),
            $"Vessel {vesselId} costs {vessel.Price} but its contents appraise for {appraisal:F0}. "
            + $"Raise its price, or add it to {nameof(_knownUnderpriced)} if this is intended.");
    }

    [Test]
    [PairConfig(nameof(s_purchaseSettings))]
    public async Task ConsolePurchaseChecksAccessAndFunds()
    {
        var map = await Pair.CreateTestMap();
        var stationSystem = Server.System<StationSystem>();
        var cargo = Server.System<CargoSystem>();
        var access = Server.System<AccessReaderSystem>();
        var shipyard = Server.System<ShipyardSystem>();

        var vessel = SProtoMan.EnumeratePrototypes<VesselPrototype>().MinBy(v => v.Price)!;

        EntityUid station = default;
        EntityUid console = default;
        EntityUid buyer = default;

        await Server.WaitPost(() =>
        {
            station = SEntMan.SpawnEntity(StationProto, MapCoordinates.Nullspace);
            stationSystem.AddGridToStation(station, map.Grid);

            console = SEntMan.SpawnEntity(ConsoleProto, map.GridCoords);
            buyer = SEntMan.SpawnEntity(null, map.GridCoords);

            var bank = SEntMan.GetComponent<StationBankAccountComponent>(station);
            foreach (var (account, balance) in bank.Accounts.ToList())
                cargo.UpdateBankAccount((station, bank), -balance, account);
        });

        int Balance()
        {
            var bank = SEntMan.GetComponent<StationBankAccountComponent>(station);
            return bank.Accounts.GetValueOrDefault(bank.PrimaryAccount);
        }

        int ShipyardGrids()
        {
            if (shipyard.ShipyardMapId is not { } shipyardMap)
                return 0;

            var count = 0;
            var query = SEntMan.EntityQueryEnumerator<MapGridComponent, TransformComponent>();
            while (query.MoveNext(out _, out _, out var xform))
            {
                if (xform.MapID == shipyardMap)
                    count++;
            }

            return count;
        }

        void Purchase()
        {
            var msg = new ShipyardConsolePurchaseMessage(vessel.ID) { Actor = buyer };
            SEntMan.EventBus.RaiseLocalEvent(console, msg);
        }

        await Server.WaitAssertion(() =>
        {
            var bank = SEntMan.GetComponent<StationBankAccountComponent>(station);
            cargo.UpdateBankAccount((station, bank), vessel.Price * 2, bank.PrimaryAccount);

            Purchase();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Balance(), Is.EqualTo(vessel.Price * 2), "A buyer without access was charged.");
                Assert.That(ShipyardGrids(), Is.Zero, "A buyer without access got a shuttle.");
            }

            access.SetActive((console, SEntMan.GetComponent<AccessReaderComponent>(console)), false);

            cargo.UpdateBankAccount((station, bank), -vessel.Price - 1, bank.PrimaryAccount);
            Purchase();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Balance(), Is.EqualTo(vessel.Price - 1), "A purchase went through without enough funds.");
                Assert.That(ShipyardGrids(), Is.Zero, "A shuttle spawned without enough funds.");
            }

            cargo.UpdateBankAccount((station, bank), 1 + 100, bank.PrimaryAccount);
            Purchase();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Balance(), Is.EqualTo(100), "The station was not charged the vessel price.");
                Assert.That(ShipyardGrids(), Is.EqualTo(1), "The purchased shuttle did not appear on the shipyard map.");
            }
        });
    }
}
