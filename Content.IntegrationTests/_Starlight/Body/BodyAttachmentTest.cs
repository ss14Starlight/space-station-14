// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._Starlight.Body;

[TestFixture]
[TestOf(typeof(SLBodySystem))]
public sealed class BodyAttachmentTest : GameTest
{
    private const string TestBody = "SLBodyTest";

    [Test]
    public async Task BuildDetachAttach()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySys = entMan.System<SLBodySystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(TestBody, map.MapCoords);
            var body = new Entity<SLBodyComponent>(uid, entMan.GetComponent<SLBodyComponent>(uid));
            var hands = entMan.GetComponent<HandsComponent>(uid);
            var movement = entMan.GetComponent<MovementSpeedModifierComponent>(uid);

            using (Assert.EnterMultipleScope())
            {
                // Torso, head, 2 arms, 2 hands, 2 legs, 2 feet + 8 organs (the eyes organ is an external part).
                Assert.That(bodySys.GetBodyParts(body), Has.Count.EqualTo(18));
                Assert.That(hands.Count, Is.EqualTo(2));
            }
            var fullSpeed = movement.BaseWalkSpeed;
            Assert.That(fullSpeed, Is.GreaterThan(0f));

            var root = bodySys.GetBodyParts(body)[0];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(root.Owner, Is.EqualTo(body.Comp.RootPartEntity));
                // The root part is hidden inside the body, not a loose entity next to it.
                Assert.That(entMan.System<SharedContainerSystem>().TryGetContainingContainer((root.Owner, null, null), out var rootContainer));
                Assert.That(rootContainer!.ID, Is.EqualTo(SLBodySystem.RootContainerId));
                Assert.That(rootContainer.Owner, Is.EqualTo(uid));
                Assert.That(bodySys.TryGetPartInSocket(root, "LeftArm", out var leftArm));
                Assert.That(bodySys.GetPartSide(bodySys.GetChildParts(leftArm).Single()), Is.EqualTo(BodySide.Left));
                Assert.That(bodySys.GetPartAddress(bodySys.GetChildParts(leftArm).Single()).Path, Is.EqualTo("/LeftArm/Hand"));

                // Detaching the arm takes its hand along.
                Assert.That(bodySys.TryDetachPart(leftArm));
                Assert.That(bodySys.GetBodyParts(body), Has.Count.EqualTo(16));
                Assert.That(hands.Count, Is.EqualTo(1));
                Assert.That(leftArm.Comp.Body, Is.Null);
                Assert.That(bodySys.GetSubtree(leftArm), Has.Count.EqualTo(2), "the hand stays attached to the severed arm");
                Assert.That(bodySys.TryDetachPart(root), Is.False, "the root part cannot be detached");

                // The socket is free again and accepts the arm back.
                Assert.That(bodySys.TryAttachPart(root, "LeftArm", leftArm));
                Assert.That(bodySys.GetBodyParts(body), Has.Count.EqualTo(18));
                Assert.That(hands.Count, Is.EqualTo(2));
                Assert.That(leftArm.Comp.Body, Is.EqualTo(uid));
                Assert.That(bodySys.TryAttachPart(root, "LeftArm", leftArm), Is.False, "a part cannot be attached twice");

                // Losing a leg slows the body down (half the legs, and the remaining leg is overloaded).
                Assert.That(bodySys.TryGetPartInSocket(root, "LeftLeg", out var leftLeg));
                Assert.That(bodySys.TryDetachPart(leftLeg));
                Assert.That(movement.BaseWalkSpeed, Is.LessThan((fullSpeed / 2) + 0.01f));

                entMan.DeleteEntity(uid);
                entMan.DeleteEntity(leftLeg);
            }
        });
    }

    [Test]
    public async Task LegacyOrganBridge()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySys = entMan.System<SLBodySystem>();
        var legacyBody = entMan.System<SharedBodySystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(TestBody, map.MapCoords);
            var body = new Entity<SLBodyComponent>(uid, entMan.GetComponent<SLBodyComponent>(uid));

            var organs = legacyBody.GetBodyOrgans(uid).ToList();
            Assert.That(organs, Has.Count.EqualTo(8));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(organs.All(o => o.Component.Body == uid), "every organ knows its body");
                Assert.That(legacyBody.GetBodyOrganEntityComps<LungComponent>((uid, null)), Has.Count.EqualTo(1));
                Assert.That(legacyBody.TryGetOrgansWithComponent<BrainComponent>((uid, null), out var brains));
                var brain = brains!.Single();

                var root = bodySys.GetBodyParts(body)[0];
                Assert.That(bodySys.TryGetPartInSocket(root, "Head", out var head));
                Assert.That(bodySys.IsInternal(bodySys.GetSubtree(head).First(p => p.Owner == brain.Owner)));

                Assert.That(bodySys.TryDetachPart(head));
                Assert.That(legacyBody.GetBodyOrgans(uid).Count(), Is.EqualTo(5), "head organs left with the head");
                Assert.That(brain.Comp2.Body, Is.Null);

                entMan.DeleteEntity(uid);
                entMan.DeleteEntity(head);
            }
        });
    }
}
