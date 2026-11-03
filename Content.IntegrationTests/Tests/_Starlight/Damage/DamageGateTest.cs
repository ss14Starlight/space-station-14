#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Damage.Components;
using Content.Server.Destructible;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Damage;

[TestFixture]
[TestOf(typeof(DamageGateComponent))]
public sealed class DamageGateTest : GameTest
{
    private const string CappedEntity = "TestDamageGateCapped";
    private const string UncappedEntity = "TestDamageGateUncapped";
    private const string ThresholdEntity = "TestDamageGateThresholds";
    private const string MinimumThresholdEntity = "TestDamageGateMinimumThresholds";
    private const string CappedMinimumThresholdEntity = "TestDamageGateCappedMinimumThreshold";
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
        {PrototypeIndent}{PrototypeIndent}limitToNextStage: true
        {PrototypeIndent}{PrototypeIndent}stageDamageCooldown: 1

        - type: entity
        {PrototypeIndent}id: {MinimumThresholdEntity}
        {PrototypeIndent}components:
        {PrototypeIndent}- type: Damageable
        {PrototypeIndent}- type: Destructible
        {PrototypeIndent}{PrototypeIndent}thresholds:
        {PrototypeIndent}{PrototypeIndent}- trigger:
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}{PrototypeIndent}!type:DamageTrigger
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}{PrototypeIndent}damage: 20
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}behaviors:
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}- !type:DoActsBehavior
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}{PrototypeIndent}acts: [Breakage]
        {PrototypeIndent}{PrototypeIndent}- trigger:
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}{PrototypeIndent}!type:DamageTrigger
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}{PrototypeIndent}damage: 40
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}behaviors:
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}- !type:DoActsBehavior
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}{PrototypeIndent}acts: [Destruction]
        {PrototypeIndent}- type: DamageGate
        {PrototypeIndent}{PrototypeIndent}minDamageToStage: 50
        {PrototypeIndent}{PrototypeIndent}minDamageToDestruction: 100

        - type: entity
        {PrototypeIndent}id: {CappedMinimumThresholdEntity}
        {PrototypeIndent}components:
        {PrototypeIndent}- type: Damageable
        {PrototypeIndent}- type: Destructible
        {PrototypeIndent}{PrototypeIndent}thresholds:
        {PrototypeIndent}{PrototypeIndent}- trigger:
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}{PrototypeIndent}!type:DamageTrigger
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}{PrototypeIndent}damage: 100
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}behaviors:
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}- !type:DoActsBehavior
        {PrototypeIndent}{PrototypeIndent}{PrototypeIndent}{PrototypeIndent}acts: [Destruction]
        {PrototypeIndent}- type: DamageGate
        {PrototypeIndent}{PrototypeIndent}maxDamagePerHit: 400
        {PrototypeIndent}{PrototypeIndent}minDamageToDestruction: 4900
        {PrototypeIndent}{PrototypeIndent}limitToNextStage: true

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
    public async Task RequiresMinimumSingleHitDamageForBreakageAndDestruction()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid entity = default;
        IEntityManager entityManager = null!;
        DamageableSystem damageable = null!;
        DamageTypePrototype blunt = null!;

        await server.WaitPost(() =>
        {
            entityManager = server.ResolveDependency<IEntityManager>();
            var prototypeManager = server.ResolveDependency<IPrototypeManager>();
            entity = entityManager.SpawnEntity(MinimumThresholdEntity, map.MapCoords);
            damageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
            blunt = prototypeManager.Index<DamageTypePrototype>(BluntDamageType);
        });

        await server.WaitAssertion(() =>
        {
            var destructible = entityManager.GetComponent<DestructibleComponent>(entity);
            var breakageTrigger = (DamageTrigger)destructible.Thresholds[0].Trigger!;
            var destructionTrigger = (DamageTrigger)destructible.Thresholds[1].Trigger!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(breakageTrigger.Damage, Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(destructionTrigger.Damage, Is.EqualTo(FixedPoint2.New(40)));
            }

            damageable.ChangeDamage(entity, new DamageSpecifier(blunt, 10), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            damageable.ChangeDamage(entity, new DamageSpecifier(blunt, 10), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageable.GetTotalDamage(entity), Is.EqualTo(FixedPoint2.FromCents(1999)));
                Assert.That(destructible.IsBroken, Is.False);
                Assert.That(entityManager.IsQueuedForDeletion(entity), Is.False);
            }

            damageable.ChangeDamage(entity, new DamageSpecifier(blunt, 49), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageable.GetTotalDamage(entity), Is.EqualTo(FixedPoint2.FromCents(1999)));
                Assert.That(entityManager.IsQueuedForDeletion(entity), Is.False);
            }

            damageable.ChangeDamage(entity, new DamageSpecifier(blunt, 50), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageable.GetTotalDamage(entity), Is.EqualTo(FixedPoint2.FromCents(3999)));
                Assert.That(destructible.IsBroken, Is.True);
                Assert.That(entityManager.IsQueuedForDeletion(entity), Is.False);
            }

            damageable.ChangeDamage(entity, new DamageSpecifier(blunt, 99), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageable.GetTotalDamage(entity), Is.EqualTo(FixedPoint2.FromCents(3999)));
                Assert.That(entityManager.IsQueuedForDeletion(entity), Is.False);
            }

            damageable.ChangeDamage(entity, new DamageSpecifier(blunt, 100), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            Assert.That(entityManager.IsQueuedForDeletion(entity), Is.True);
        });
    }

    [Test]
    public async Task X4DestroysStationAiCoreInOneHit()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid core = default;
        EntityUid x4 = default;
        var coordinates = map.GridCoords;
        IEntityManager entityManager = null!;
        DamageableSystem damageable = null!;
        TriggerSystem trigger = null!;

        await server.WaitPost(() =>
        {
            entityManager = server.ResolveDependency<IEntityManager>();
            core = entityManager.SpawnEntity("PlayerStationAiEmpty", coordinates);
            damageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
            trigger = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<TriggerSystem>();
        });

        await server.WaitRunTicks(5);

        await server.WaitPost(() =>
            x4 = entityManager.SpawnEntity("X4", coordinates));

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var mapSystem = entityManager.System<SharedMapSystem>();
            var transformSystem = entityManager.System<SharedTransformSystem>();
            var coreMapCoordinates = transformSystem.GetMapCoordinates(core);
            var x4MapCoordinates = transformSystem.GetMapCoordinates(x4);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(coreMapCoordinates.MapId, Is.EqualTo(map.MapId));
                Assert.That(x4MapCoordinates.MapId, Is.EqualTo(map.MapId));
                Assert.That(mapSystem.TryFindGridAt(coreMapCoordinates, out var coreGrid, out _), Is.True);
                Assert.That(mapSystem.TryFindGridAt(x4MapCoordinates, out var x4Grid, out _), Is.True);
                Assert.That(coreGrid, Is.EqualTo(map.Grid.Owner));
                Assert.That(x4Grid, Is.EqualTo(map.Grid.Owner));
                Assert.That(map.Tile.Tile.IsEmpty, Is.False);
            }
        });

        await server.WaitPost(() =>
        {
            Assert.That(trigger.Trigger(x4, key: TriggerSystem.DefaultTriggerKey, predicted: false), Is.True);
            Assert.That(trigger.GetRemainingTime(x4), Is.Not.Null);
        });

        await server.WaitRunTicks(601);
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.EntityExists(x4), Is.False, "X4 timer did not trigger the explosion.");
            var visuals = entityManager.EntityQueryEnumerator<ExplosionVisualsComponent>();
            Assert.That(visuals.MoveNext(out var visual), Is.True);
            Assert.That(visual.Tiles.ContainsKey(map.Grid.Owner), Is.True);
        });

        await server.WaitRunTicks(119);

        await server.WaitAssertion(() =>
            Assert.That(entityManager.EntityExists(core), Is.False,
                $"X4 left the AI core present with {damageable.GetTotalDamage(core)} damage."));
    }

    [Test]
    public async Task RepeatedC4HitsCannotDestroyStationAiCore()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid core = default;
        var coordinates = map.GridCoords;
        IEntityManager entityManager = null!;
        DamageableSystem damageable = null!;
        ExplosionSystem explosion = null!;

        await server.WaitPost(() =>
        {
            entityManager = server.ResolveDependency<IEntityManager>();
            core = entityManager.SpawnEntity("PlayerStationAiEmpty", coordinates);
            damageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
            explosion = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<ExplosionSystem>();
        });

        for (var hit = 0; hit < 10; hit++)
        {
            EntityUid c4 = default;
            await server.WaitPost(() => c4 = entityManager.SpawnEntity("C4", coordinates));

            await server.WaitRunTicks(1);

            await server.WaitPost(() => explosion.TriggerExplosive(c4));

            await server.WaitRunTicks(10);

            await server.WaitAssertion(() =>
                Assert.That(entityManager.EntityExists(core), Is.True,
                    $"C4 hit {hit + 1} destroyed the station AI core."));
        }

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(entityManager.EntityExists(core), Is.True);
                Assert.That(damageable.GetTotalDamage(core), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(damageable.GetTotalDamage(core), Is.LessThan(FixedPoint2.New(800)));
            }
        });
    }

    [Test]
    public async Task SyndieMiniBombCannotDestroyStationAiCore()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid core = default;
        EntityUid minibomb = default;
        var coordinates = map.GridCoords;
        IEntityManager entityManager = null!;
        DamageableSystem damageable = null!;
        ExplosionSystem explosion = null!;

        await server.WaitPost(() =>
        {
            entityManager = server.ResolveDependency<IEntityManager>();
            core = entityManager.SpawnEntity("PlayerStationAiEmpty", coordinates);
            minibomb = entityManager.SpawnEntity("SyndieMiniBomb", coordinates);
            damageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
            explosion = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<ExplosionSystem>();
        });

        await server.WaitRunTicks(1);
        await server.WaitPost(() => explosion.TriggerExplosive(minibomb));
        await server.WaitRunTicks(120);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.EntityExists(core), Is.True,
                "The Syndie minibomb destroyed the station AI core.");

            var totalDamage = damageable.GetTotalDamage(core);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(totalDamage, Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(totalDamage, Is.LessThan(FixedPoint2.New(800)));
            }
        });
    }

    [Test]
    public async Task DamageCapsCannotBypassMinimumSingleHitDestructionDamage()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid entity = default;
        IEntityManager entityManager = null!;
        DamageableSystem damageable = null!;
        DamageTypePrototype blunt = null!;

        await server.WaitPost(() =>
        {
            entityManager = server.ResolveDependency<IEntityManager>();
            var prototypeManager = server.ResolveDependency<IPrototypeManager>();
            entity = entityManager.SpawnEntity(CappedMinimumThresholdEntity, map.MapCoords);
            damageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
            blunt = prototypeManager.Index<DamageTypePrototype>(BluntDamageType);
        });

        await server.WaitAssertion(() =>
        {
            var damageGate = entityManager.GetComponent<DamageGateComponent>(entity);
            var destructible = entityManager.GetComponent<DestructibleComponent>(entity);
            var destructionTrigger = (DamageTrigger)destructible.Thresholds[0].Trigger!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageGate.MaxDamagePerHit, Is.EqualTo(FixedPoint2.New(400)));
                Assert.That(damageGate.LimitToNextStage, Is.True);
                Assert.That(destructionTrigger.Damage, Is.EqualTo(FixedPoint2.New(100)));
                Assert.That(damageable.CanBeDamagedBy(entity, blunt.ID), Is.True);
            }

            damageable.ChangeDamage(entity, new DamageSpecifier(blunt, 5000), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageable.GetTotalDamage(entity), Is.EqualTo(FixedPoint2.FromCents(9999)));
                Assert.That(entityManager.IsQueuedForDeletion(entity), Is.False);
            }

            for (var hit = 0; hit < 10; hit++)
                damageable.ChangeDamage(entity, new DamageSpecifier(blunt, 5000), ignoreResistances: true,
                    ignoreGlobalModifiers: true);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageable.GetTotalDamage(entity), Is.EqualTo(FixedPoint2.FromCents(9999)));
                Assert.That(entityManager.IsQueuedForDeletion(entity), Is.False);
            }
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
