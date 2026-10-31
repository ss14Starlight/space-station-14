using Content.Server._Starlight.Medical.Body.Systems;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Preferences;

namespace Content.IntegrationTests.Tests.Internals;

[TestFixture]
[TestOf(typeof(InternalsSystem))]
public sealed class AutoInternalsTests : GameTest
{
    [Test]
    // Starlight - start
    public async Task TestInternalsAutoActivateInSpaceForStationAndEntitySpawn()
    // Starlight - end
    {
        var pair = Pair;
        var server = pair.Server;

        var testMap = await pair.CreateTestMap();

        var stationSpawning = server.System<StationSpawningSystem>();
        var atmos = server.System<AtmosphereSystem>();
        var internals = server.System<InternalsSystem>();

        await server.WaitAssertion(() =>
        {
            var profile = new HumanoidCharacterProfile();
            // Starlight - start
            var stationSpawn = stationSpawning.SpawnPlayerMob(testMap.GridCoords, "TestInternalsDummy", profile, station: null);
            var entitySpawn = server.EntMan.Spawn("TestInternalsDummyEntity", testMap.MapCoords);
            // Starlight - end

            Assert.That(atmos.HasAtmosphere(testMap.Grid), Is.False, "Test map has atmosphere - test needs adjustment!");
            // Starlight - start
            Assert.Multiple(() =>
            {
                Assert.That(internals.AreInternalsWorking(stationSpawn), "Internals did not automatically connect after station spawning!");
                Assert.That(internals.AreInternalsWorking(entitySpawn), "Internals did not automatically connect after entity spawning!");
            });
            // Starlight - end

            // Starlight - start
            server.EntMan.DeleteEntity(stationSpawn);
            server.EntMan.DeleteEntity(entitySpawn);
            // Starlight - end
        });
    }

    [TestPrototypes]
    private const string Prototypes = @"
- type: playTimeTracker
  id: PlayTimeInternalsDummy

- type: startingGear
  id: InternalsDummyGear
  equipment:
    mask: ClothingMaskBreath
    suitstorage: OxygenTankFilled

- type: job
  id: TestInternalsDummy
  playTimeTracker: PlayTimeInternalsDummy
  startingGear: InternalsDummyGear

- type: entity
  id: TestInternalsDummyEntity
  parent: MobHuman
  components:
    - type: Loadout
      prototypes: [InternalsDummyGear]
";
}
