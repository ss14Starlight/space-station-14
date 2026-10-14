using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Starlight.Damage;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Damageable
{
    [TestFixture]
    [TestOf(typeof(DamageableComponent))]
    [TestOf(typeof(DamageableSystem))]
    public sealed class DamageableTest : GameTest
    {
        private const string TestDamageableEntityId = "TestDamageableEntityId";
        private const string TestGroupedDamageEntityId = "TestGroupedDamageEntityId"; // Starlight
        private const string TestMixMaxDamageEntityId = "TestMixMaxDamageEntityId"; // Starlight
        private const string TestNullGroupDamageEntityId = "TestNullGroupDamageEntityId"; // Starlight
        private const string TestGroup1 = "TestGroup1";
        private const string TestGroup2 = "TestGroup2";
        private const string TestGroup3 = "TestGroup3";
        private const string TestDamage1 = "TestDamage1";
        private const string TestDamage2a = "TestDamage2a";
        private const string TestDamage2b = "TestDamage2b";

        private const string TestDamage3a = "TestDamage3a";

        private const string TestDamage3b = "TestDamage3b";
        private const string TestDamage3c = "TestDamage3c";

        [TestPrototypes]
        private const string Prototypes = $"""
        # Define some damage groups
        -   type: damageType
            id: {TestDamage1}
            name: damage-type-blunt

        -   type: damageType
            id: {TestDamage2a}
            name: damage-type-blunt

        -   type: damageType
            id: {TestDamage2b}
            name: damage-type-blunt

        -   type: damageType
            id: {TestDamage3a}
            name: damage-type-blunt

        -   type: damageType
            id: {TestDamage3b}
            name: damage-type-blunt

        -   type: damageType
            id: {TestDamage3c}
            name: damage-type-blunt

        # Define damage Groups with 1,2,3 damage types
        -   type: damageGroup
            id: {TestGroup1}
            name: damage-group-brute
            damageTypes:
            - {TestDamage1}

        -   type: damageGroup
            id: {TestGroup2}
            name: damage-group-brute
            damageTypes:
            - {TestDamage2a}
            - {TestDamage2b}

        -   type: damageGroup
            id: {TestGroup3}
            name: damage-group-brute
            damageTypes:
            - {TestDamage3a}
            - {TestDamage3b}
            - {TestDamage3c}

        # This container should not support TestDamage1 or TestDamage2b
        -   type: damageContainer
            id: testDamageContainer
            supportedGroups:
            - {TestGroup3}
            supportedTypes:
            - {TestDamage2a}

        -   type: entity
            id: {TestDamageableEntityId}
            name: {TestDamageableEntityId}
            components:
            -   type: Damageable
                damageContainer: testDamageContainer

        -   type: entity
            id: {TestGroupedDamageEntityId}
            name: {TestGroupedDamageEntityId}
            components:
            -   type: Damageable
                damageContainer: testDamageContainer
                damage:
                    groups:
                        {TestGroup3}: 14

        -   type: entity
            id: {TestMixMaxDamageEntityId}
            name: {TestMixMaxDamageEntityId}
            components:
            -   type: Damageable
                damageContainer: testDamageContainer
            -   type: PassiveDamage
                damage:
                    mixmax:
                        value: -1
                        groups:
                        - {TestGroup3}
                        types:
                        - {TestDamage2a}

        -   type: entity
            id: {TestNullGroupDamageEntityId}
            name: {TestNullGroupDamageEntityId}
            components:
            -   type: Damageable
                damageContainer: testDamageContainer
                damage:
                    groups: null
                    types:
                        {TestDamage2a}: 1
        """;

        #region Starlight
        [Test]
        public async Task TestGroupCombinendHealing()
        {
            var pair = Pair;
            var server = pair.Server;

            var entityManager = server.ResolveDependency<IEntityManager>();
            var prototypeManager = server.ResolveDependency<IPrototypeManager>();
            var entitySystemManager = server.ResolveDependency<IEntitySystemManager>();
            var map = await pair.CreateTestMap();

            EntityUid groupedEntity = default;
            EntityUid mixMaxEntity = default;
            EntityUid nullGroupEntity = default;
            DamageableSystem damageable = null!;
            SharedEntityEffectsSystem entityEffects = null!;
            DamageSpecifier mixMax = null!;
            DamageTypePrototype type2a = default!;
            DamageTypePrototype type3a = default!;
            DamageTypePrototype type3b = default!;
            DamageTypePrototype type3c = default!;

            await server.WaitPost(() =>
            {
                groupedEntity = entityManager.SpawnEntity(TestGroupedDamageEntityId, map.MapCoords);
                mixMaxEntity = entityManager.SpawnEntity(TestMixMaxDamageEntityId, map.MapCoords);
                nullGroupEntity = entityManager.SpawnEntity(TestNullGroupDamageEntityId, map.MapCoords);
                damageable = entitySystemManager.GetEntitySystem<DamageableSystem>();
                entityEffects = entitySystemManager.GetEntitySystem<SharedEntityEffectsSystem>();
                mixMax = entityManager.GetComponent<PassiveDamageComponent>(mixMaxEntity).Damage;
                type2a = prototypeManager.Index<DamageTypePrototype>(TestDamage2a);
                type3a = prototypeManager.Index<DamageTypePrototype>(TestDamage3a);
                type3b = prototypeManager.Index<DamageTypePrototype>(TestDamage3b);
                type3c = prototypeManager.Index<DamageTypePrototype>(TestDamage3c);
            });

            await server.WaitRunTicks(1);

            await server.WaitAssertion(() =>
            {
                var nullGroupDamage = damageable.GetAllDamage(nullGroupEntity);
                Assert.Multiple(() =>
                {
                    Assert.That(nullGroupDamage.DamageGroupDict, Is.Empty);
                    Assert.That(nullGroupDamage.DamageDict[type2a.ID], Is.EqualTo(FixedPoint2.New(1)));
                });

                var groupedDamage = damageable.GetAllDamage(groupedEntity).DamageDict;
                Assert.Multiple(() =>
                {
                    Assert.That(groupedDamage[type3a.ID], Is.EqualTo(FixedPoint2.New(4.66f)));
                    Assert.That(groupedDamage[type3b.ID], Is.EqualTo(FixedPoint2.New(4.67f)));
                    Assert.That(groupedDamage[type3c.ID], Is.EqualTo(FixedPoint2.New(4.67f)));
                });

                damageable.ChangeDamage(groupedEntity, new DamageSpecifier
                {
                    DamageGroupDict = new Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2>
                    {
                        { TestGroup3, -14 },
                    },
                }, true);
                Assert.That(damageable.GetTotalDamage(groupedEntity), Is.EqualTo(FixedPoint2.Zero));

                damageable.SetDamage(mixMaxEntity, new DamageSpecifier
                {
                    DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                    {
                        { type3a.ID, 0.5f },
                        { type3b.ID, 0.2f },
                        { type3c.ID, 0.2f },
                        { type2a.ID, 0.1f },
                    },
                });
                damageable.ChangeDamage(mixMaxEntity, mixMax, true);
                Assert.That(damageable.GetTotalDamage(mixMaxEntity), Is.EqualTo(FixedPoint2.Zero));

                damageable.SetDamage(mixMaxEntity, new DamageSpecifier
                {
                    DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                    {
                        { type3a.ID, 0.6f },
                        { type3b.ID, 0.2f },
                        { type3c.ID, 0.2f },
                        { type2a.ID, 0.1f },
                    },
                });
                damageable.ChangeDamage(mixMaxEntity, mixMax, true);

                var remainingDamage = damageable.GetAllDamage(mixMaxEntity).DamageDict;
                Assert.Multiple(() =>
                {
                    Assert.That(remainingDamage[type3a.ID], Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(remainingDamage[type3b.ID], Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(remainingDamage[type3c.ID], Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(remainingDamage[type2a.ID], Is.EqualTo(FixedPoint2.New(0.1f)));
                });

                damageable.SetDamage(mixMaxEntity, new DamageSpecifier(type3a, 0.6f));
                entityEffects.ApplyEffect(mixMaxEntity, new HealthChange { Damage = mixMax }, 0.5f);
                Assert.That(
                    damageable.GetAllDamage(mixMaxEntity).DamageDict[type3a.ID],
                    Is.EqualTo(FixedPoint2.New(0.1f)));
            });
        }
        #endregion

        [Test]
        public async Task TestDamageableComponents()
        {
            var pair = Pair;
            var server = pair.Server;

            var sEntityManager = server.ResolveDependency<IEntityManager>();
            var sPrototypeManager = server.ResolveDependency<IPrototypeManager>();
            var sEntitySystemManager = server.ResolveDependency<IEntitySystemManager>();

            EntityUid sDamageableEntity = default;
            DamageableComponent sDamageableComponent = null;
            DamageableSystem sDamageableSystem = null;

            DamageGroupPrototype group1 = default!;
            DamageGroupPrototype group2 = default!;
            DamageGroupPrototype group3 = default!;

            DamageTypePrototype type1 = default!;
            DamageTypePrototype type2a = default!;
            DamageTypePrototype type2b = default!;
            DamageTypePrototype type3a = default!;
            DamageTypePrototype type3b = default!;
            DamageTypePrototype type3c = default!;

            FixedPoint2 typeDamage;

            var map = await pair.CreateTestMap();

            await server.WaitPost(() =>
            {
                var coordinates = map.MapCoords;

                sDamageableEntity = sEntityManager.SpawnEntity(TestDamageableEntityId, coordinates);
                sDamageableComponent = sEntityManager.GetComponent<DamageableComponent>(sDamageableEntity);
                sDamageableSystem = sEntitySystemManager.GetEntitySystem<DamageableSystem>();

                group1 = sPrototypeManager.Index<DamageGroupPrototype>(TestGroup1);
                group2 = sPrototypeManager.Index<DamageGroupPrototype>(TestGroup2);
                group3 = sPrototypeManager.Index<DamageGroupPrototype>(TestGroup3);

                type1 = sPrototypeManager.Index<DamageTypePrototype>(TestDamage1);
                type2a = sPrototypeManager.Index<DamageTypePrototype>(TestDamage2a);
                type2b = sPrototypeManager.Index<DamageTypePrototype>(TestDamage2b);
                type3a = sPrototypeManager.Index<DamageTypePrototype>(TestDamage3a);
                type3b = sPrototypeManager.Index<DamageTypePrototype>(TestDamage3b);
                type3c = sPrototypeManager.Index<DamageTypePrototype>(TestDamage3c);
            });

            await server.WaitRunTicks(5);

            await server.WaitAssertion(() =>
            {
                var uid = sDamageableEntity;
                var ent = new Entity<DamageableComponent>(uid, sDamageableComponent);

                // Check that damage is evenly distributed over a group if its a nice multiple
                var types = group3.DamageTypes;
                var damageToDeal = FixedPoint2.New(types.Count * 5);
                DamageSpecifier damage = new(group3, damageToDeal);

                sDamageableSystem.ChangeDamage(uid, damage, true);

                Assert.Multiple(() =>
                {
                    Assert.That(sDamageableSystem.GetTotalDamage(ent), Is.EqualTo(damageToDeal));
                    Assert.That(sDamageableSystem.GetDamagePerGroup(ent)[group3.ID], Is.EqualTo(damageToDeal));
                    foreach (var type in types)
                    {
                        Assert.That(sDamageableSystem.GetAllDamage(uid).DamageDict.TryGetValue(type, out typeDamage));
                        Assert.That(typeDamage, Is.EqualTo(damageToDeal / types.Count));
                    }
                });

                // Heal
                sDamageableSystem.ChangeDamage(uid, -damage);

                Assert.Multiple(() =>
                {
                    Assert.That(sDamageableSystem.GetTotalDamage(ent), Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(sDamageableSystem.GetDamagePerGroup(ent)[group3.ID], Is.EqualTo(FixedPoint2.Zero));
                    foreach (var type in types)
                    {
                        Assert.That(sDamageableSystem.GetAllDamage(uid).DamageDict.TryGetValue(type, out typeDamage));
                        Assert.That(typeDamage, Is.EqualTo(FixedPoint2.Zero));
                    }
                });

                // Check that damage works properly if it is NOT perfectly divisible among group members
                types = group3.DamageTypes;

                Assert.That(types, Has.Count.EqualTo(3));

                damage = new DamageSpecifier(group3, 14);
                sDamageableSystem.ChangeDamage(uid, damage, true);

                Assert.Multiple(() =>
                {
                    Assert.That(sDamageableSystem.GetTotalDamage(ent), Is.EqualTo(FixedPoint2.New(14)));
                    Assert.That(sDamageableSystem.GetDamagePerGroup(ent)[group3.ID], Is.EqualTo(FixedPoint2.New(14)));
                    Assert.That(sDamageableSystem.GetAllDamage(uid).DamageDict[type3a.ID], Is.EqualTo(FixedPoint2.New(4.66f)));
                    Assert.That(sDamageableSystem.GetAllDamage(uid).DamageDict[type3b.ID], Is.EqualTo(FixedPoint2.New(4.67f)));
                    Assert.That(sDamageableSystem.GetAllDamage(uid).DamageDict[type3c.ID], Is.EqualTo(FixedPoint2.New(4.67f)));
                });

                // Heal
                sDamageableSystem.ChangeDamage(uid, -damage);

                Assert.Multiple(() =>
                {
                    Assert.That(sDamageableSystem.GetTotalDamage(ent), Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(sDamageableSystem.GetDamagePerGroup(ent)[group3.ID], Is.EqualTo(FixedPoint2.Zero));
                    foreach (var type in types)
                    {
                        Assert.That(sDamageableSystem.GetAllDamage(uid).DamageDict.TryGetValue(type, out typeDamage));
                        Assert.That(typeDamage, Is.EqualTo(FixedPoint2.Zero));
                    }

                    // Test that unsupported groups return false when setting/getting damage (and don't change damage)
                    Assert.That(sDamageableSystem.GetTotalDamage(ent), Is.EqualTo(FixedPoint2.Zero));
                });
                damage = new DamageSpecifier(group1, FixedPoint2.New(10)) + new DamageSpecifier(type2b, FixedPoint2.New(10));
                sDamageableSystem.ChangeDamage(uid, damage, true);

                Assert.Multiple(() =>
                {
                    Assert.That(sDamageableSystem.GetDamagePerGroup(ent).TryGetValue(group1.ID, out _), Is.False);
                    Assert.That(sDamageableSystem.GetAllDamage(uid).DamageDict.TryGetValue(type1.ID, out typeDamage), Is.False);
                    Assert.That(sDamageableSystem.GetTotalDamage(ent), Is.EqualTo(FixedPoint2.Zero));
                });

                // Test SetAll and ClearAll function
                sDamageableSystem.SetAllDamage((sDamageableEntity, sDamageableComponent), 10);
                Assert.That(sDamageableSystem.GetTotalDamage(ent), Is.EqualTo(FixedPoint2.New(10 * sDamageableSystem.GetAllDamage(uid).DamageDict.Count)));
                sDamageableSystem.SetAllDamage((sDamageableEntity, sDamageableComponent), 0);
                Assert.That(sDamageableSystem.GetTotalDamage(ent), Is.EqualTo(FixedPoint2.Zero));
                sDamageableSystem.SetAllDamage((sDamageableEntity, sDamageableComponent), 10);
                Assert.That(sDamageableSystem.GetTotalDamage(ent), Is.EqualTo(FixedPoint2.New(10 * sDamageableSystem.GetAllDamage(uid).DamageDict.Count)));
                sDamageableSystem.ClearAllDamage((sDamageableEntity, sDamageableComponent));
                Assert.That(sDamageableSystem.GetTotalDamage(ent), Is.EqualTo(FixedPoint2.Zero));

                // Test 'wasted' healing
                sDamageableSystem.ChangeDamage(uid, new DamageSpecifier(type3a, 5));
                sDamageableSystem.ChangeDamage(uid, new DamageSpecifier(type3b, 7));
                sDamageableSystem.ChangeDamage(uid, new DamageSpecifier(group3, -11));

                Assert.Multiple(() =>
                {
                    Assert.That(sDamageableSystem.GetAllDamage(uid).DamageDict[type3a.ID], Is.EqualTo(FixedPoint2.New(1.34)));
                    Assert.That(sDamageableSystem.GetAllDamage(uid).DamageDict[type3b.ID], Is.EqualTo(FixedPoint2.New(3.33)));
                    Assert.That(sDamageableSystem.GetAllDamage(uid).DamageDict[type3c.ID], Is.EqualTo(FixedPoint2.New(0)));
                });

                // Test Over-Healing
                sDamageableSystem.ChangeDamage(uid, new DamageSpecifier(group3, FixedPoint2.New(-100)));
                Assert.That(sDamageableSystem.GetTotalDamage(ent), Is.EqualTo(FixedPoint2.Zero));

                // Test that if no health change occurred, returns false
                sDamageableSystem.ChangeDamage(uid, new DamageSpecifier(group3, -100));
                Assert.That(sDamageableSystem.GetTotalDamage(ent), Is.EqualTo(FixedPoint2.Zero));

                #region Starlight
                // Mix-max uses the normal negative healing convention and heals the largest value first.
                damage = new DamageSpecifier(type3a, FixedPoint2.New(0.6f))
                        + new DamageSpecifier(type3b, FixedPoint2.New(0.2f))
                        + new DamageSpecifier(type3c, FixedPoint2.New(0.1f))
                        + new DamageSpecifier(type2a, FixedPoint2.New(0.2f));
                sDamageableSystem.ChangeDamage(uid, damage, true);
                sDamageableSystem.ChangeDamage(uid, new DamageSpecifier
                {
                    MixMax = new DamageSpecifierMixMax
                    {
                        Value = FixedPoint2.New(-1),
                        Groups = new() { group3.ID },
                        Types = new() { type2a.ID },
                    },
                }, true);

                Assert.Multiple(() =>
                {
                    Assert.That(sDamageableSystem.GetAllDamage(uid).DamageDict[type3a.ID], Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(sDamageableSystem.GetAllDamage(uid).DamageDict[type3b.ID], Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(sDamageableSystem.GetAllDamage(uid).DamageDict[type2a.ID], Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(sDamageableSystem.GetAllDamage(uid).DamageDict[type3c.ID], Is.EqualTo(FixedPoint2.New(0.1f)));
                });
            #endregion
            });
        }
    }
}
