using Content.IntegrationTests.Fixtures;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Salvage;

[TestFixture]
[TestOf(typeof(SharedShuttleSystem))]
public sealed class SalvageFtlInitializationTest : GameTest
{
    [Test]
    public async Task UninitializedDestinationIsRejectedButInitializedPausedMapIsValid()
    {
        var server = Pair.Server;
        var entManager = server.EntMan;
        var mapSystem = entManager.System<SharedMapSystem>();
        var shuttleSystem = entManager.System<SharedShuttleSystem>();

        await server.WaitAssertion(() =>
        {
            var sourceMap = mapSystem.CreateMap(out _);
            var targetMap = mapSystem.CreateMap(out var targetMapId, runMapInit: false);
            var shuttle = entManager.SpawnEntity(null, new EntityCoordinates(sourceMap, default));
            entManager.AddComponent<FTLDestinationComponent>(targetMap);

            Assert.Multiple(() =>
            {
                Assert.That(mapSystem.IsInitialized(targetMapId), Is.False);
                Assert.That(shuttleSystem.CanFTLTo(shuttle, targetMapId, shuttle), Is.False);
            });

            mapSystem.SetPaused(targetMapId, false);

            Assert.That(mapSystem.IsPaused(targetMapId), Is.True);

            mapSystem.InitializeMap(targetMapId);
            mapSystem.SetPaused(targetMapId, true);

            Assert.Multiple(() =>
            {
                Assert.That(mapSystem.IsInitialized(targetMapId), Is.True);
                Assert.That(mapSystem.IsPaused(targetMapId), Is.True);
                Assert.That(shuttleSystem.CanFTLTo(shuttle, targetMapId, shuttle), Is.True);
            });

            mapSystem.SetPaused(targetMapId, false);

            Assert.That(mapSystem.IsPaused(targetMapId), Is.False);
        });
    }
}
