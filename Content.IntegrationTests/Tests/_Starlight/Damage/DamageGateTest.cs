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
    private const string UncappedEntity = "TestDamageGateUncapped";
    private const string ThresholdEntity = "TestDamageGateThresholds";
    private const string CooldownEntity = "TestDamageGateCooldown";
    private const string BluntDamageType = "Blunt";
    private const string HeatDamageType = "Heat";
    private const string PrototypeIndent = "  ";

    [TestPrototypes] private const string Prototypes = $"""
        - type: entity
        {PrototypeIndent}id: {CappedEntity}
        {PrototypeIndent}components:
        {PrototypeIndent}- type: Damageable
        {PrototypeIndent}- type: DamageGate
        {PrototypeIndent}{PrototypeIndent}maxDamagePerHit: 25

        - type: entity
        {PrototypeIndent}id: {UncappedEntity}
        {PrototypeIndent}components:
        {PrototypeIndent}- type: Damageable

        - type: entity
        {PrototypeIndent}id: {ThresholdEntity}
        {PrototypeIndent}components:
        {PrototypeIndent}- type: Damageable
        {PrototypeIndent}- type: Destructible
        {PrototypeIndent}{PrototypeIndent}thresholds:
        {PrototypeIndent}{PrototypeIndent}- trigger:
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}{PrototypeIndent}!type:DamageTrigger
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}{PrototypeIndent}damage: 50
        {PrototypeIndent}{PrototypeIndent}- trigger:
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}{PrototypeIndent}!type:DamageTrigger
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}{PrototypeIndent}damage: 100
        {PrototypeIndent}- type: DamageGate
        {PrototypeIndent}{PrototypeIndent}limitToNextDestructionThreshold: true
        {PrototypeIndent}{PrototypeIndent}thresholdDamageCooldown: 1

        - type: entity
        {PrototypeIndent}id: {CooldownEntity}
        {PrototypeIndent}components:
        {PrototypeIndent}- type: Damageable
        {PrototypeIndent}- type: DamageGate
        {PrototypeIndent}{PrototypeIndent}damageCooldown: 1
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
    public async Task DoesNotMutateDamageSpecifierReusedAcrossEntities()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid cappedEntity = default;
        EntityUid uncappedEntity = default;
        DamageableSystem damageable = null!;
        DamageTypePrototype blunt = null!;

        await server.WaitPost(() =>
        {
            var entityManager = server.ResolveDependency<IEntityManager>();
            var prototypeManager = server.ResolveDependency<IPrototypeManager>();
            cappedEntity = entityManager.SpawnEntity(CappedEntity, map.MapCoords);
            uncappedEntity = entityManager.SpawnEntity(UncappedEntity, map.MapCoords);
            damageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
            blunt = prototypeManager.Index<DamageTypePrototype>(BluntDamageType);
        });

        await server.WaitAssertion(() =>
        {
            var damage = new DamageSpecifier(blunt, 60);
            damageable.ChangeDamage(cappedEntity, damage, ignoreResistances: true, ignoreGlobalModifiers: true);
            var damageAfterGate = damage.DamageDict[blunt.ID];

            damageable.ChangeDamage(uncappedEntity, damage, ignoreResistances: true, ignoreGlobalModifiers: true);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageAfterGate, Is.EqualTo(FixedPoint2.New(60)));
                Assert.That(damageable.GetTotalDamage(cappedEntity), Is.EqualTo(FixedPoint2.New(25)));
                Assert.That(damageable.GetTotalDamage(uncappedEntity), Is.EqualTo(FixedPoint2.New(60)));
            }
        });
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
