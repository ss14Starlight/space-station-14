using Content.IntegrationTests.Fixtures;
using Content.Shared.Damage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Humanoid;

[TestFixture]
public sealed class FelionoidStaminaTest : GameTest
{
    private static readonly string[] FelionoidPrototypes =
    [
        "MobFelionoid",
        "MobNeoFelionoid",
    ];

    [Test]
    public async Task FelionoidsHaveTenPercentMoreStaminaCapacity()
    {
        var pair = Pair;
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var felionoids = new EntityUid[FelionoidPrototypes.Length];

        try
        {
            await server.WaitPost(() =>
            {
                for (var i = 0; i < FelionoidPrototypes.Length; i++)
                {
                    felionoids[i] = entityManager.SpawnEntity(FelionoidPrototypes[i], MapCoordinates.Nullspace);
                }
            });
            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                for (var i = 0; i < FelionoidPrototypes.Length; i++)
                {
                    var stamina = entityManager.GetComponent<StaminaComponent>(felionoids[i]);
                    Assert.That(stamina.BaseCritThreshold, Is.EqualTo(110f), FelionoidPrototypes[i]);
                    Assert.That(stamina.CritThreshold, Is.EqualTo(110f), FelionoidPrototypes[i]);
                }
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                foreach (var felionoid in felionoids)
                {
                    if (felionoid.IsValid())
                        entityManager.DeleteEntity(felionoid);
                }
            });
            await pair.RunUntilSynced();
        }
    }
}
