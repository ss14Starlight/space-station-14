using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Body;

[TestFixture]
[TestOf(typeof(OrganMissingSystem))]
public sealed class OrganMissingTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> _bloodlossDamageType = "Bloodloss";
    private static readonly ProtoId<DamageTypePrototype> _poisonDamageType = "Poison";

    [TestPrototypes] private const string Prototypes = @"
-
    type: entity
    id: OrganMissingTestDummy
    components:
    -
        type: Body
        prototype: Human
    -
        type: Damageable
    -
        type: MobState
        allowedStates:
        - Alive
-
    type: entity
    id: OrganMissingTestItem
    parent: BaseItem
";

    [Test]
    public async Task MissingConfiguredOrgansApplyConfiguredDamage()
    {
        var pair = Pair;
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var bodySystem = entityManager.System<SharedBodySystem>();
        var containerSystem = entityManager.System<SharedContainerSystem>();
        var damageable = entityManager.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        EntityUid body = default;
        await server.WaitAssertion(() => body = entityManager.SpawnEntity("OrganMissingTestDummy", map.MapCoords));

        await server.WaitRunTicks(70);
        await server.WaitAssertion(() => Assert.That(damageable.GetAllDamage(body).DamageDict, Is.Empty));

        await server.WaitAssertion(() =>
        {
            Assert.That(bodySystem.TryGetOrgansWithComponent<OrganHeartComponent>(body, out var hearts), Is.True);
            Assert.That(bodySystem.TryGetOrgansWithComponent<OrganLiverComponent>(body, out var livers), Is.True);
            Assert.That(bodySystem.TryGetOrgansWithComponent<OrganKidneysComponent>(body, out var kidneys), Is.True);
            var heartId = hearts![0].Owner;
            Assert.That(containerSystem.TryGetContainingContainer((heartId, null, null), out var heartContainer), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(bodySystem.RemoveOrgan(hearts![0], entityManager.GetComponent<OrganComponent>(hearts[0])),
                    Is.True);
                Assert.That(bodySystem.RemoveOrgan(livers![0], entityManager.GetComponent<OrganComponent>(livers[0])),
                    Is.True);
                Assert.That(bodySystem.RemoveOrgan(kidneys![0], entityManager.GetComponent<OrganComponent>(kidneys[0])),
                    Is.True);
            });

            var replacement = entityManager.SpawnEntity("OrganMissingTestItem", map.MapCoords);
            Assert.That(containerSystem.Insert(replacement, heartContainer!), Is.True);
        });

        await PoolManager.WaitUntil(server, () => damageable.GetTotalDamage(body) > FixedPoint2.Zero);

        await server.WaitAssertion(() =>
        {
            var damage = damageable.GetAllDamage(body).DamageDict;
            var bloodloss = prototypeManager.Index(_bloodlossDamageType).ID;
            var poison = prototypeManager.Index(_poisonDamageType).ID;

            Assert.Multiple(() =>
            {
                Assert.That(damage[bloodloss], Is.EqualTo(FixedPoint2.New(1)));
                Assert.That(damage[poison], Is.EqualTo(FixedPoint2.New(0.2f)));
            });
        });
    }
}
