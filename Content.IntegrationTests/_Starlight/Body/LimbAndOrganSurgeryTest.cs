// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Limbs;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._Starlight.Body;

[TestFixture]
public sealed class LimbAndOrganSurgeryTest : GameTest
{
    [Test]
    public async Task AmputateAndAttach()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var medBody = entMan.System<SharedBodySystem>();
        var slBody = entMan.System<SLBodySystem>();
        var limbs = entMan.System<LimbSystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var hands = entMan.GetComponent<HandsComponent>(human);
            Assert.That(hands.Count, Is.EqualTo(2));

            var torso = medBody.GetBodyChildrenOfType(human, BodyPartType.Torso).Single();
            var rightArm = medBody.GetBodyChildrenOfType(human, BodyPartType.Arm)
                .Single(p => p.Component.Symmetry == BodyPartSymmetry.Right);
            Assert.That(rightArm.Component.Body, Is.EqualTo(human));

            Entity<TransformComponent, HumanoidAppearanceComponent, SLBodyComponent> body = (human,
                entMan.GetComponent<TransformComponent>(human),
                entMan.GetComponent<HumanoidAppearanceComponent>(human),
                entMan.GetComponent<SLBodyComponent>(human));
            limbs.Amputatate(body, (rightArm.Id, entMan.GetComponent<TransformComponent>(rightArm.Id),
                entMan.GetComponent<MetaDataComponent>(rightArm.Id), rightArm.Component));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(hands.Count, Is.EqualTo(1), "the right hand left with the arm");
                Assert.That(rightArm.Component.Body, Is.Null);
                Assert.That(medBody.GetBodyChildrenOfType(human, BodyPartType.Arm).Count(), Is.EqualTo(1));

                // Sew it back on.
                Assert.That(limbs.AttachLimb((human, body.Comp2), "right arm", (torso.Id, torso.Component), (rightArm.Id, rightArm.Component)));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(hands.Count, Is.EqualTo(2));
                Assert.That(rightArm.Component.Body, Is.EqualTo(human));
            }

            // Replace the left arm with a cyber arm, which brings its own hand.
            var leftArm = medBody.GetBodyChildrenOfType(human, BodyPartType.Arm)
                .Single(p => p.Component.Symmetry == BodyPartSymmetry.Left);
            limbs.Amputatate(body, (leftArm.Id, entMan.GetComponent<TransformComponent>(leftArm.Id),
                entMan.GetComponent<MetaDataComponent>(leftArm.Id), leftArm.Component));
            Assert.That(hands.Count, Is.EqualTo(1));

            var cyberArm = entMan.SpawnEntity("LeftArmCyber", map.GridCoords);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(slBody.GetChildParts((cyberArm, entMan.GetComponent<SLBodyPartComponent>(cyberArm))).Count(), Is.EqualTo(1),
                            "the cyber arm spawns with its hand attached");
                Assert.That(limbs.AttachLimb((human, body.Comp2), "left arm", (torso.Id, torso.Component),
                    (cyberArm, entMan.GetComponent<SLBodyPartComponent>(cyberArm))));
                Assert.That(hands.Count, Is.EqualTo(2));
            }

            entMan.DeleteEntity(human);
            entMan.DeleteEntity(leftArm.Id);
        });
    }

    [Test]
    public async Task RemoveAndInsertOrgan()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var medBody = entMan.System<SharedBodySystem>();
        var slBody = entMan.System<SLBodySystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var lungs = medBody.GetBodyOrganEntityComps<LungComponent>((human, null)).Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(lungs.Comp2.Body, Is.EqualTo(human));

                // Organ removal through the medical body API.
                Assert.That(medBody.RemoveOrgan(lungs.Owner, lungs.Comp2));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(lungs.Comp2.Body, Is.Null);
                Assert.That(medBody.GetBodyOrganEntityComps<LungComponent>((human, null)), Is.Empty);
            }

            var torso = medBody.GetBodyChildrenOfType(human, BodyPartType.Torso).Single();
            Entity<SLBodyPartComponent> torsoPart = (torso.Id, entMan.GetComponent<SLBodyPartComponent>(torso.Id));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(slBody.TryAttachPart(torsoPart, "Lungs", (lungs.Owner, entMan.GetComponent<SLBodyPartComponent>(lungs.Owner))));
                Assert.That(lungs.Comp2.Body, Is.EqualTo(human));
            }

            entMan.DeleteEntity(human);
        });
    }
}
