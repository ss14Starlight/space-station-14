using Content.IntegrationTests.Fixtures;
using Content.Shared._Starlight.Shadekin;
using Content.Shared._Starlight.Shadekin.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.IntegrationTests.Tests._Starlight.Shadekin;

[TestFixture]
[TestOf(typeof(ShadekinSystem))]
public sealed class ShadekinLightExposureTests : GameTest
{
    /// <summary>
    /// Tests that a lit point light contributes to a nearby entity's light exposure, and that marking it as a
    /// dark light or having it suppressed by a shadegen takes it back out of the count.
    /// </summary>
    [Test]
    public async Task LightExposureCountsOnlyNormalLights()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();

        var shadekin = server.System<ShadekinSystem>();
        var pointLight = server.System<SharedPointLightSystem>();

        EntityUid light = default;
        EntityUid target = default;

        await server.WaitAssertion(() =>
        {
            light = server.EntMan.SpawnAtPosition(null, map.GridCoords);
            target = server.EntMan.SpawnAtPosition(null, map.GridCoords.Offset(new Vector2(1, 0)));

            var lightComp = pointLight.EnsureLight(light);
            pointLight.SetRadius(light, 5f, lightComp);
            pointLight.SetEnergy(light, 2f, lightComp);
            pointLight.SetEnabled(light, true, lightComp);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(shadekin.GetLightExposure(target), Is.GreaterThan(0f), "A lit light next to us should light us up.");

            server.EntMan.AddComponent<DarkLightComponent>(light);
            Assert.That(shadekin.GetLightExposure(target), Is.Zero, "A dark light should not light us up.");
            server.EntMan.RemoveComponent<DarkLightComponent>(light);

            server.EntMan.AddComponent<ShadegenAffectedComponent>(light);
            Assert.That(shadekin.GetLightExposure(target), Is.Zero, "A shadegen suppressed light should not light us up.");
            server.EntMan.RemoveComponent<ShadegenAffectedComponent>(light);

            pointLight.SetEnabled(light, false);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(shadekin.GetLightExposure(target), Is.Zero, "A disabled light should not light us up.");

            server.EntMan.DeleteEntity(light);
            server.EntMan.DeleteEntity(target);
        });
    }

    /// <summary>
    /// Tests that a shadegen in range zeroes out light exposure no matter what else is around.
    /// </summary>
    [Test]
    public async Task ShadegenInRangeZeroesExposure()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();

        var shadekin = server.System<ShadekinSystem>();
        var pointLight = server.System<SharedPointLightSystem>();

        EntityUid light = default;
        EntityUid target = default;
        EntityUid shadegen = default;

        await server.WaitAssertion(() =>
        {
            light = server.EntMan.SpawnAtPosition(null, map.GridCoords);
            target = server.EntMan.SpawnAtPosition(null, map.GridCoords.Offset(new Vector2(1, 0)));

            var lightComp = pointLight.EnsureLight(light);
            pointLight.SetRadius(light, 5f, lightComp);
            pointLight.SetEnergy(light, 2f, lightComp);
            pointLight.SetEnabled(light, true, lightComp);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(shadekin.GetLightExposure(target), Is.GreaterThan(0f));

            shadegen = server.EntMan.SpawnAtPosition(null, map.GridCoords);
            server.EntMan.AddComponent<ShadegenComponent>(shadegen);

            Assert.That(shadekin.GetLightExposure(target), Is.Zero, "A shadegen in range should make it pitch black.");

            server.EntMan.DeleteEntity(shadegen);
            server.EntMan.DeleteEntity(light);
            server.EntMan.DeleteEntity(target);
        });
    }

    /// <summary>
    /// Tests that a light further away than its own radius contributes nothing, so the light tree query isn't
    /// picking up lights that can't reach us.
    /// </summary>
    [Test]
    public async Task LightOutOfRangeIsIgnored()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();

        var shadekin = server.System<ShadekinSystem>();
        var pointLight = server.System<SharedPointLightSystem>();

        EntityUid light = default;
        EntityUid target = default;

        await server.WaitAssertion(() =>
        {
            light = server.EntMan.Spawn(null, new MapCoordinates(0, 0, map.MapId));
            target = server.EntMan.Spawn(null, new MapCoordinates(20, 0, map.MapId));

            var lightComp = pointLight.EnsureLight(light);
            pointLight.SetRadius(light, 5f, lightComp);
            pointLight.SetEnergy(light, 2f, lightComp);
            pointLight.SetEnabled(light, true, lightComp);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(shadekin.GetLightExposure(target), Is.Zero);

            server.EntMan.DeleteEntity(light);
            server.EntMan.DeleteEntity(target);
        });
    }
}
