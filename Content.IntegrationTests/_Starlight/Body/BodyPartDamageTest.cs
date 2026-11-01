// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.PartDamage;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._Starlight.Body;

[TestFixture]
[TestOf(typeof(BodyPartDamageSystem))]
public sealed class BodyPartDamageTest : GameTest
{
    private const string PiercingDamageId = "Piercing";

    [Test]
    public async Task DamageGoesToParts()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var damageable = entMan.System<DamageableSystem>();
        var partDamage = entMan.System<BodyPartDamageSystem>();
        var slBody = entMan.System<SLBodySystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            DamageSpecifier Of(string type, FixedPoint2 amount)
                => new(protoMan.Index<DamageTypePrototype>(type), amount);

            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var body = new Entity<SLBodyComponent>(human, entMan.GetComponent<SLBodyComponent>(human));
            var parts = partDamage.GetDamageableParts(body);
            Assert.That(parts, Has.Count.EqualTo(10), "torso, head, 2 arms, 2 hands, 2 legs, 2 feet");

            FixedPoint2 PartsTotal(string type)
            {
                var total = FixedPoint2.Zero;
                foreach (var part in partDamage.GetDamageableParts(body))
                    total += partDamage.GetPartDamage(part.Comp, type);
                return total;
            }

            FixedPoint2 BodyTotal(string type)
                => damageable.GetAllDamage(human).DamageDict.TryGetValue(type, out var value) ? value : FixedPoint2.Zero;

            var leftArm = parts.Single(p => entMan.GetComponent<SLBodyPartComponent>(p).Kind == BodyPartType.Arm
                && entMan.GetComponent<SLBodyPartComponent>(p).Symmetry == BodyPartSymmetry.Left);
            var leftHand = parts.Single(p => entMan.GetComponent<SLBodyPartComponent>(p).Kind == BodyPartType.Hand
                && entMan.GetComponent<SLBodyPartComponent>(p).Symmetry == BodyPartSymmetry.Left);

            using (partDamage.Target(human, BodyDamageTargeting.AtPart(leftArm)))
                damageable.ChangeDamage(human, Of("Blunt", 30), ignoreResistances: true);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(partDamage.GetPartDamage(leftArm.Comp, "Blunt"), Is.EqualTo(FixedPoint2.New(30)));
                Assert.That(BodyTotal("Blunt"), Is.EqualTo(FixedPoint2.New(30)));
                Assert.That(leftArm.Comp.Integrity, Is.EqualTo(leftArm.Comp.MaxIntegrity - 3), "10% of blunt goes into integrity");
            }

            damageable.ChangeDamage(human, Of("Heat", 20), ignoreResistances: true);
            Assert.That(PartsTotal("Heat"), Is.EqualTo(BodyTotal("Heat")));
            Assert.That(PartsTotal("Heat"), Is.EqualTo(FixedPoint2.New(20)));

            using (partDamage.Target(human, BodyDamageTargeting.Spread))
                damageable.ChangeDamage(human, Of("Piercing", 50), ignoreResistances: true);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(PartsTotal("Piercing"), Is.EqualTo(FixedPoint2.New(50)));
                Assert.That(partDamage.GetDamageableParts(body).Count(p => partDamage.GetPartDamage(p.Comp, "Piercing") > 0),
                    Is.GreaterThan(5));
            }

            using (partDamage.Target(human, BodyDamageTargeting.MostDamaged))
                damageable.ChangeDamage(human, Of("Blunt", -20), ignoreResistances: true);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(partDamage.GetPartDamage(leftArm.Comp, "Blunt"), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(PartsTotal("Blunt"), Is.EqualTo(BodyTotal("Blunt")));
            }

            var hand = leftHand.Comp;
            var integrityBefore = hand.Integrity;
            var room = hand.MaxHealth - partDamage.GetPartDamageTotal(hand);
            using (partDamage.Target(human, BodyDamageTargeting.AtPart(leftHand)))
                damageable.ChangeDamage(human, Of("Slash", room + 20), ignoreResistances: true);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(hand.Integrity, Is.EqualTo(integrityBefore - ((room * 0.2f) + 20)));
                Assert.That(partDamage.GetHealthFraction(hand), Is.Zero);
            }

            var armAndHand = partDamage.GetPartDamageTotal(leftArm.Comp) + partDamage.GetPartDamageTotal(hand);
            var totalBefore = damageable.GetTotalDamage(human);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(slBody.TryDetachPart((leftArm.Owner, entMan.GetComponent<SLBodyPartComponent>(leftArm))));
                Assert.That(damageable.GetTotalDamage(human), Is.EqualTo(totalBefore - armAndHand));
                Assert.That(PartsTotal("Slash"), Is.EqualTo(BodyTotal("Slash")));
            }

            entMan.DeleteEntity(human);
            entMan.DeleteEntity(leftArm);
        });
    }

    [Test]
    public async Task BrokenPartsAreDestroyed()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var damageable = entMan.System<DamageableSystem>();
        var partDamage = entMan.System<BodyPartDamageSystem>();
        var mobState = entMan.System<MobStateSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid SpawnHuman() => entMan.SpawnEntity("MobHuman", map.GridCoords);

        Entity<BodyPartDamageComponent> Part(EntityUid human, BodyPartType kind, BodyPartSymmetry side = BodyPartSymmetry.None)
            => partDamage.GetDamageableParts((human, entMan.GetComponent<SLBodyComponent>(human)))
                .First(p => entMan.GetComponent<SLBodyPartComponent>(p).Kind == kind
                    && (side == BodyPartSymmetry.None || entMan.GetComponent<SLBodyPartComponent>(p).Symmetry == side));

        void Hit(EntityUid human, EntityUid part, string type, FixedPoint2 amount)
        {
            using (partDamage.Target(human, BodyDamageTargeting.AtPart(part)))
                damageable.ChangeDamage(human, new DamageSpecifier(protoMan.Index<DamageTypePrototype>(type), amount), ignoreResistances: true);
        }

        EntityUid armHuman = default, arm = default, handHuman = default, hand = default;
        EntityUid headHuman = default, head = default, brain = default, torsoHuman = default;
        FixedPoint2 armHumanDamage = default;
        await server.WaitPost(() =>
        {
            // Just past what the arm can take: it comes off whole, its wounds stay on the body.
            armHuman = SpawnHuman();
            arm = Part(armHuman, BodyPartType.Arm, BodyPartSymmetry.Left);
            Hit(armHuman, arm, "Slash", 130);
            armHumanDamage = damageable.GetTotalDamage(armHuman);

            // Crushed far past it: the hand is blown apart.
            handHuman = SpawnHuman();
            hand = Part(handHuman, BodyPartType.Hand, BodyPartSymmetry.Right);
            Hit(handHuman, hand, "Blunt", 200);

            headHuman = SpawnHuman();
            head = Part(headHuman, BodyPartType.Head);
            brain = entMan.System<SharedBodySystem>().GetBodyOrganEntityComps<BrainComponent>((headHuman, null)).Single().Owner;
            Hit(headHuman, head, "Blunt", 180);

            torsoHuman = SpawnHuman();
            Hit(torsoHuman, Part(torsoHuman, BodyPartType.Torso), "Blunt", 400);
        });
        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(entMan.EntityExists(arm), "a severed arm drops whole");
                Assert.That(entMan.GetComponent<SLBodyPartComponent>(arm).Body, Is.Null);
                Assert.That(damageable.GetTotalDamage(armHuman), Is.EqualTo(armHumanDamage), "losing the arm does not heal the body");
                Assert.That(entMan.GetComponent<HandsComponent>(armHuman).Count, Is.EqualTo(1));

                Assert.That(entMan.Deleted(hand), "a hand hit far past its integrity is blown apart");

                Assert.That(entMan.Deleted(head), "the head is destroyed");
                Assert.That(mobState.IsDead(headHuman));
                Assert.That(entMan.EntityExists(brain), "the brain drops out of the destroyed head");
                Assert.That(entMan.GetComponent<SLBodyPartComponent>(brain).Body, Is.Null);

                Assert.That(entMan.Deleted(torsoHuman) || entMan.GetComponent<MetaDataComponent>(torsoHuman).EntityLifeStage >= EntityLifeStage.Terminating,
                    "a destroyed torso gibs the body");
            }
        });
    }

    [Test]
    public async Task OnlyHardHitsAndFireDestroyParts()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var damageable = entMan.System<DamageableSystem>();
        var partDamage = entMan.System<BodyPartDamageSystem>();
        var mobState = entMan.System<MobStateSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid SpawnHuman() => entMan.SpawnEntity("MobHuman", map.GridCoords);

        Entity<BodyPartDamageComponent> Part(EntityUid human, BodyPartType kind)
            => partDamage.GetDamageableParts((human, entMan.GetComponent<SLBodyComponent>(human)))
                .First(p => entMan.GetComponent<SLBodyPartComponent>(p).Kind == kind);

        void Hit(EntityUid human, EntityUid part, string type, FixedPoint2 amount)
        {
            using (partDamage.Target(human, BodyDamageTargeting.AtPart(part)))
                damageable.ChangeDamage(human, new DamageSpecifier(protoMan.Index<DamageTypePrototype>(type), amount), ignoreResistances: true);
        }

        int AshCount()
        {
            var count = 0;
            var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var meta))
            {
                if (meta.EntityPrototype?.ID == "Ash")
                    count++;
            }
            return count;
        }

        EntityUid beatenHuman = default, torso = default, frozenHuman = default, frozenArm = default;
        EntityUid burnedHuman = default, burnedArm = default, corpse = default;
        var ashBefore = 0;
        await server.WaitPost(() =>
        {
            ashBefore = AshCount();

            // A long beating wears the torso down to nothing, but never tears it apart
            // (kept under the species' own 400 blunt gib threshold).
            beatenHuman = SpawnHuman();
            torso = Part(beatenHuman, BodyPartType.Torso);
            for (var i = 0; i < 70; i++)
                Hit(beatenHuman, torso, "Blunt", 5);

            // Cold can't take a limb off, however much of it.
            frozenHuman = SpawnHuman();
            frozenArm = Part(frozenHuman, BodyPartType.Arm);
            Hit(frozenHuman, frozenArm, "Cold", 300);

            // A heavy burn chars the arm away.
            burnedHuman = SpawnHuman();
            burnedArm = Part(burnedHuman, BodyPartType.Arm);
            Hit(burnedHuman, burnedArm, "Heat", 150);

            // A corpse left burning goes down to ash.
            corpse = SpawnHuman();
            mobState.ChangeMobState(corpse, MobState.Dead);
            Hit(corpse, Part(corpse, BodyPartType.Torso), "Heat", 400);
        });
        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(entMan.EntityExists(beatenHuman), "wearing the torso down does not gib");
                Assert.That(entMan.GetComponent<BodyPartDamageComponent>(torso).Integrity, Is.EqualTo(FixedPoint2.New(1)),
                    "the torso hangs on at the last point of integrity");

                Assert.That(entMan.GetComponent<SLBodyPartComponent>(frozenArm).Body, Is.EqualTo(frozenHuman));

                Assert.That(entMan.Deleted(burnedArm), "a burnt arm turns to ash, it does not drop");
                Assert.That(entMan.EntityExists(burnedHuman));

                Assert.That(entMan.Deleted(corpse) || entMan.GetComponent<MetaDataComponent>(corpse).EntityLifeStage >= EntityLifeStage.Terminating,
                    "a burnt corpse is incinerated");
                Assert.That(AshCount(), Is.EqualTo(ashBefore + 2));
            }
        });
    }

    [Test]
    public async Task TissueRepairRestoresIntegrity()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var partDamage = entMan.System<BodyPartDamageSystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var arm = partDamage.GetDamageableParts((human, entMan.GetComponent<SLBodyComponent>(human)))
                .First(p => entMan.GetComponent<SLBodyPartComponent>(p).Kind == BodyPartType.Arm);
            var surgery = entMan.SpawnEntity("SurgeryRepairTissue", MapCoordinates.Nullspace);
            var step = entMan.SpawnEntity("SurgeryStepMendTissue", MapCoordinates.Nullspace);

            bool Offered()
            {
                var ev = new SurgeryValidEvent(human, arm);
                entMan.EventBus.RaiseLocalEvent(surgery, ref ev);
                return !ev.Cancelled;
            }

            Assert.That(Offered(), Is.False, "nothing to repair on a whole arm");

            using (partDamage.Target(human, BodyDamageTargeting.AtPart(arm)))
                entMan.System<DamageableSystem>().ChangeDamage(human,
                    new DamageSpecifier(protoMan.Index<DamageTypePrototype>(PiercingDamageId), 40), ignoreResistances: true);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(arm.Comp.Integrity, Is.LessThan(arm.Comp.MaxIntegrity));
                Assert.That(Offered(), "a damaged arm can be repaired");
            }

            var stepEv = new SurgeryStepEvent(human, human, arm, [])
            {
                StepProto = "SurgeryStepMendTissue",
                SurgeryProto = "SurgeryRepairTissue",
            };
            entMan.EventBus.RaiseLocalEvent(step, ref stepEv);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(arm.Comp.Integrity, Is.EqualTo(arm.Comp.MaxIntegrity));
                Assert.That(Offered(), Is.False);
            }

            entMan.DeleteEntity(human);
            entMan.DeleteEntity(surgery);
            entMan.DeleteEntity(step);
        });
    }
}
