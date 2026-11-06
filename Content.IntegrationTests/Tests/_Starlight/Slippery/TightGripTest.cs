#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared._Starlight.Slippery;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight.Slippery;

[TestFixture]
public sealed class TightGripTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, DummyTicker = false };

    [Test]
    public async Task FelionoidKeepsHandItemsWhenKnockedDown()
    {
        var pair = Pair;
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var handsSystem = server.System<SharedHandsSystem>();
        var stunSystem = server.System<SharedStunSystem>();
        var mapSystem = server.System<SharedMapSystem>();
        var map = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        try
        {
            EntityUid felionoid = default;
            EntityUid item = default;
            HandsComponent hands = null!;

            await server.WaitPost(() =>
            {
                felionoid = entityManager.SpawnEntity("MobFelionoid", map.GridCoords);
                item = entityManager.SpawnEntity("Crowbar", map.GridCoords);
                hands = entityManager.GetComponent<HandsComponent>(felionoid);

                Assert.That(entityManager.HasComponent<TightGripComponent>(felionoid), Is.True);
                Assert.That(handsSystem.TryPickup(felionoid, item, hands.ActiveHandId!), Is.True);
            });

            await pair.RunTicksSync(5);

            await server.WaitPost(() =>
            {
                Assert.That(stunSystem.TryKnockdown(felionoid, TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(entityManager.HasComponent<KnockedDownComponent>(felionoid), Is.True);
                Assert.That(handsSystem.GetActiveItem((felionoid, hands)), Is.EqualTo(item));
            });
        }
        finally
        {
            await server.WaitPost(() => mapSystem.DeleteMap(map.MapId));
            await pair.RunTicksSync(5);
        }
    }
}
