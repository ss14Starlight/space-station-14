// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.HealthAnalyzer;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.HealthAnalyzer;
using Content.Shared._Starlight.Medical.PartDamage;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._Starlight.Body;

[TestFixture]
[TestOf(typeof(HealthAnalyzerDollSystem))]
public sealed class HealthAnalyzerDollTest : GameTest
{
    private const string BluntDamage = "Blunt";

    [Test]
    public async Task DollShowsParts()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var partDamage = entMan.System<BodyPartDamageSystem>();
            var slBody = entMan.System<SLBodySystem>();

            Dictionary<string, byte> Doll()
            {
                var ev = new CollectHealthAnalyzerExtensionsEvent();
                entMan.EventBus.RaiseLocalEvent(human, ref ev);
                return ev.Doll.ToDictionary(p => p.Part, p => p.Level);
            }

            var doll = Doll();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(doll.Keys, Is.SupersetOf(["head", "chest", "groin", "l_arm", "r_arm", "l_hand", "r_hand", "l_leg", "r_leg", "l_foot", "r_foot"]));
                Assert.That(doll.Values.All(v => v == 0), "a healthy body is all green");
            }

            var parts = partDamage.GetDamageableParts((human, entMan.GetComponent<SLBodyComponent>(human)));
            var head = parts.Single(p => entMan.GetComponent<SLBodyPartComponent>(p).Kind == BodyPartType.Head);
            using (partDamage.Target(human, BodyDamageTargeting.AtPart(head)))
            {
                entMan.System<DamageableSystem>().ChangeDamage(human,
                    new DamageSpecifier(protoMan.Index<DamageTypePrototype>(BluntDamage), 60), ignoreResistances: true);
            }

            var leftArm = parts.Single(p => entMan.GetComponent<SLBodyPartComponent>(p).Kind == BodyPartType.Arm
                && entMan.GetComponent<SLBodyPartComponent>(p).Symmetry == BodyPartSymmetry.Left);
            slBody.TryDetachPart((leftArm.Owner, entMan.GetComponent<SLBodyPartComponent>(leftArm)));

            doll = Doll();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(doll["head"], Is.GreaterThan(0).And.Not.EqualTo(HealthAnalyzerDollPart.Missing), "a hurt head is not green");
                Assert.That(doll["l_arm"], Is.EqualTo(HealthAnalyzerDollPart.Missing));
                Assert.That(doll["l_hand"], Is.EqualTo(HealthAnalyzerDollPart.Missing), "the hand went with the arm");
                Assert.That(doll["r_arm"], Is.Zero);
            }

            entMan.DeleteEntity(human);
            entMan.DeleteEntity(leftArm);
        });
    }
}
