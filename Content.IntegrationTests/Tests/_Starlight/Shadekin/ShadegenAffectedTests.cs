using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Shadekin;
using Content.Shared._Starlight.Shadekin.Components;
using Robust.Shared.GameObjects;
using System.Numerics;

namespace Content.IntegrationTests.Tests._Starlight.Shadekin;

[TestFixture]
[TestOf(typeof(ShadegenSystem))]
public sealed class ShadegenAffectedTests : GameTest
{
    /// <summary>
    /// A fresh shadegen refreshes on its first tick, and deleting one queues a refresh, so this only needs slack.
    /// </summary>
    private const int RefreshTicks = 5;

    /// <summary>
    /// Tests that shadegens keep their own affected markers, and release them when deleted. Regression tests:
    /// the shared update queue used to let shadegens strip one another's markers, and deleting the last shadegen
    /// used to leave a light suppressed for the rest of the round.
    /// </summary>
    [Test]
    public async Task TwoShadegensDoNotClobberEachOther()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        var pointLight = server.System<SharedPointLightSystem>();

        EntityUid farLight = default;
        EntityUid nearLight = default;
        EntityUid nearShadegen = default;
        EntityUid farShadegen = default;

        await server.WaitAssertion(() =>
        {
            nearLight = server.EntMan.SpawnAtPosition(null, map.GridCoords);
            farLight = server.EntMan.SpawnAtPosition(null, map.GridCoords.Offset(new Vector2(30, 0)));

            foreach (var light in new[] { nearLight, farLight })
            {
                var lightComp = pointLight.EnsureLight(light);
                pointLight.SetRadius(light, 5f, lightComp);
                pointLight.SetEnabled(light, true, lightComp);
            }

            nearShadegen = server.EntMan.SpawnAtPosition(null, map.GridCoords);
            server.EntMan.AddComponent<ShadegenComponent>(nearShadegen);

            farShadegen = server.EntMan.SpawnAtPosition(null, map.GridCoords.Offset(new Vector2(30, 0)));
            server.EntMan.AddComponent<ShadegenComponent>(farShadegen);
        });

        await server.WaitRunTicks(RefreshTicks);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.HasComponent<ShadegenAffectedComponent>(nearLight), Is.True,
                    "The light covered by the first shadegen should stay marked.");
                Assert.That(server.EntMan.HasComponent<ShadegenAffectedComponent>(farLight), Is.True,
                    "The light covered by the second shadegen should stay marked.");
            });

            server.EntMan.DeleteEntity(nearShadegen);
        });

        await server.WaitRunTicks(RefreshTicks);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.HasComponent<ShadegenAffectedComponent>(nearLight), Is.False,
                    "Deleting the last shadegen should release the nearby light.");
                Assert.That(server.EntMan.HasComponent<ShadegenAffectedComponent>(farLight), Is.True,
                    "Deleting one shadegen should not release another shadegen's light.");
            });

            server.EntMan.DeleteEntity(farShadegen);
        });

        await server.WaitRunTicks(RefreshTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<ShadegenAffectedComponent>(farLight), Is.False,
                "Deleting the last shadegen should release every light it was suppressing.");

            server.EntMan.DeleteEntity(nearLight);
            server.EntMan.DeleteEntity(farLight);
        });
    }

    /// <summary>
    /// Tests that a light outside every shadegen's range is never marked, so the radius-enlarged tree bounds
    /// aren't leaking far away lights into the affected set.
    /// </summary>
    [Test]
    public async Task LightOutsideRangeIsNotAffected()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        var pointLight = server.System<SharedPointLightSystem>();

        EntityUid light = default;
        EntityUid shadegen = default;

        await server.WaitAssertion(() =>
        {
            // Well outside the 8 tile default range, but with a radius big enough that its tree bounds still reach.
            light = server.EntMan.SpawnAtPosition(null, map.GridCoords.Offset(new Vector2(20, 0)));
            var lightComp = pointLight.EnsureLight(light);
            pointLight.SetRadius(light, 20f, lightComp);
            pointLight.SetEnabled(light, true, lightComp);

            shadegen = server.EntMan.SpawnAtPosition(null, map.GridCoords);
            server.EntMan.AddComponent<ShadegenComponent>(shadegen);
        });

        await server.WaitRunTicks(RefreshTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<ShadegenAffectedComponent>(light), Is.False,
                "A light further away than the shadegen's range should not be marked.");

            server.EntMan.DeleteEntity(shadegen);
            server.EntMan.DeleteEntity(light);
        });
    }
}
