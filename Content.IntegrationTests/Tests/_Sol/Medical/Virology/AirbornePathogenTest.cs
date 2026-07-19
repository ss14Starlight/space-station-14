using Content.Server._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests._Sol.Medical.Virology;

[TestFixture]
public sealed class AirbornePathogenTest
{
    [Test]
    public async Task AddAirborneLoadCreatesGridStore()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            // Spawn a human; they should be on a grid in the test map.
            var mob = entMan.Spawn("MobHuman");
            var xform = entMan.GetComponent<TransformComponent>(mob);
            if (xform.GridUid is not { } grid)
            {
                Assert.Ignore("No grid available for airborne tile test.");
                return;
            }

            var gridSys = entMan.System<GridPathogenAtmosphereSystem>();
            gridSys.AddAirborneLoad(mob, "SolPathogenFlu", 3f);

            Assert.That(entMan.HasComponent<GridPathogenAtmosphereComponent>(grid), Is.True);
            Assert.That(gridSys.GetAirborneLoad(grid, entMan.System<SharedMapSystem>()
                .GetTileRef(grid, entMan.GetComponent<MapGridComponent>(grid), xform.Coordinates).GridIndices), Is.GreaterThan(0f));

            var removed = gridSys.RemoveAirborneLoad(grid, entMan.System<SharedMapSystem>()
                .GetTileRef(grid, entMan.GetComponent<MapGridComponent>(grid), xform.Coordinates).GridIndices, 10f);
            Assert.That(removed, Is.GreaterThan(0f));
        });

        await pair.CleanReturnAsync();
    }
}
