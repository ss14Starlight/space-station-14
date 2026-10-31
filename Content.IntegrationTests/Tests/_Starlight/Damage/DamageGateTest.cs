#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Damage.Components;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Damage;

[TestFixture]
[TestOf(typeof(DamageGateComponent))]
public sealed class DamageGateTest : GameTest
{
    private const string CappedEntity = "TestDamageGateCapped";
    private const string ThresholdEntity = "TestDamageGateThresholds";
    private const string CooldownEntity = "TestDamageGateCooldown";
    private const string BluntDamageType = "Blunt";
    private const string HeatDamageType = "Heat";

    [TestPrototypes] private const string Prototypes = $"""
        - type: entity
          id: {CappedEntity}
          components:
          - type: Damageable
          - type: DamageGate
            maxDamagePerHit: 25

        - type: entity
          id: {ThresholdEntity}
          components:
          - type: Damageable
          - type: Destructible
            thresholds:
            - trigger:
                !type:DamageTrigger
                damage: 50
            - trigger:
                !type:DamageTrigger
                damage: 100
          - type: DamageGate
            limitToNextDestructionThreshold: true
            thresholdDamageCooldown: 1

        - type: entity
          id: {CooldownEntity}
          components:
          - type: Damageable
          - type: DamageGate
            damageCooldown: 1
        """;

    [Test]
    public async Task CapsPositiveDamagePerCallWithoutBlockingHealing()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var previousDamageModifier = server.CfgMan.GetCVar(CCVars.PlaytestAllDamageModifier);

        EntityUid entity = default;
        DamageableSystem damageable = null!;
        DamageTypePrototype blunt = null!;
        DamageTypePrototype heat = null!;

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CCVars.PlaytestAllDamageModifier, 2f);
            var entityManager = server.ResolveDependency<IEntityManager>();
            var prototypeManager = server.ResolveDependency<IPrototypeManager>();
            entity = entityManager.SpawnEntity(CappedEntity, map.MapCoords);
            damageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
            blunt = prototypeManager.Index<DamageTypePrototype>(BluntDamageType);
            heat = prototypeManager.Index<DamageTypePrototype>(HeatDamageType);
        });

        await server.WaitAssertion(() =>
        {
            var damage = new DamageSpecifier(blunt, 60) + new DamageSpecifier(heat, 40);
            damageable.ChangeDamage(entity, damage, ignoreResistances: true);

            var damageByType = damageable.GetAllDamage(entity).DamageDict;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageable.GetTotalDamage(entity), Is.EqualTo(FixedPoint2.New(25)));
                Assert.That(damageByType[blunt.ID], Is.EqualTo(FixedPoint2.New(15)));
                Assert.That(damageByType[heat.ID], Is.EqualTo(FixedPoint2.New(10)));
            }

            damageable.ChangeDamage(entity, new DamageSpecifier(blunt, -10), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            Assert.That(damageable.GetTotalDamage(entity), Is.EqualTo(FixedPoint2.New(15)));
        });

        await server.WaitPost(() => server.CfgMan.SetCVar(CCVars.PlaytestAllDamageModifier, previousDamageModifier));
    }

    [Test]
    public async Task LimitsDamageToNextThresholdAndStartsThresholdCooldown()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid entity = default;
        DamageableSystem damageable = null!;
        DamageTypePrototype blunt = null!;

        await server.WaitPost(() =>
        {
            var entityManager = server.ResolveDependency<IEntityManager>();
            var prototypeManager = server.ResolveDependency<IPrototypeManager>();
            entity = entityManager.SpawnEntity(ThresholdEntity, map.MapCoords);
            damageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
            blunt = prototypeManager.Index<DamageTypePrototype>(BluntDamageType);
        });

        await server.WaitAssertion(() =>
        {
            damageable.ChangeDamage(entity, new DamageSpecifier(blunt, 5), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            var damageAfterFirstSmallHit = damageable.GetTotalDamage(entity);

            damageable.ChangeDamage(entity, new DamageSpecifier(blunt, 5), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            var damageAfterSecondSmallHit = damageable.GetTotalDamage(entity);

            var damage = new DamageSpecifier(blunt, 1000);
            damageable.ChangeDamage(entity, damage, ignoreResistances: true, ignoreGlobalModifiers: true);
            var damageAfterFirstHit = damageable.GetTotalDamage(entity);

            damageable.ChangeDamage(entity, new DamageSpecifier(blunt, 5), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            var damageDuringThresholdCooldown = damageable.GetTotalDamage(entity);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageAfterFirstSmallHit, Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(damageAfterSecondSmallHit, Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(damageAfterFirstHit, Is.EqualTo(FixedPoint2.New(50)));
                Assert.That(damageDuringThresholdCooldown, Is.EqualTo(FixedPoint2.New(50)));
            }
        });

        await server.WaitRunTicks(120);

        await server.WaitAssertion(() =>
        {
            var damage = new DamageSpecifier(blunt, 1000);
            damageable.ChangeDamage(entity, damage, ignoreResistances: true, ignoreGlobalModifiers: true);
            var damageAfterSecondHit = damageable.GetTotalDamage(entity);
            Assert.That(damageAfterSecondHit, Is.EqualTo(FixedPoint2.New(100)));
        });
    }

    [Test]
    public async Task CooldownBlocksDamageButAllowsHealing()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid entity = default;
        DamageableSystem damageable = null!;
        DamageTypePrototype blunt = null!;

        await server.WaitPost(() =>
        {
            var entityManager = server.ResolveDependency<IEntityManager>();
            var prototypeManager = server.ResolveDependency<IPrototypeManager>();
            entity = entityManager.SpawnEntity(CooldownEntity, map.MapCoords);
            damageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
            blunt = prototypeManager.Index<DamageTypePrototype>(BluntDamageType);
        });

        await server.WaitAssertion(() =>
        {
            damageable.ChangeDamage(entity, new DamageSpecifier(blunt, 10), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            damageable.ChangeDamage(entity, new DamageSpecifier(blunt, 10), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            var damageAfterCooldownHit = damageable.GetTotalDamage(entity);

            damageable.ChangeDamage(entity, new DamageSpecifier(blunt, -5), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            var damageAfterHealing = damageable.GetTotalDamage(entity);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageAfterCooldownHit, Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(damageAfterHealing, Is.EqualTo(FixedPoint2.New(5)));
            }
        });

        await server.WaitRunTicks(120);

        await server.WaitAssertion(() =>
        {
            damageable.ChangeDamage(entity, new DamageSpecifier(blunt, 10), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            Assert.That(damageable.GetTotalDamage(entity), Is.EqualTo(FixedPoint2.New(15)));
        });
    }
}
