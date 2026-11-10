// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Limbs;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._Starlight.Body;

[TestFixture]
public sealed class LimbMaskTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task MaskFollowsAmputation()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        EntityUid human = default;
        Entity<SLBodyPartComponent> torso = default;
        Entity<SLBodyPartComponent> rightArm = default;
        await server.WaitPost(() =>
        {
            human = sEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var body = sEntMan.System<SharedBodySystem>();
            var torsoPart = body.GetBodyChildrenOfType(human, BodyPartType.Torso).Single();
            torso = (torsoPart.Id, torsoPart.Component);
            var arm = body.GetBodyChildrenOfType(human, BodyPartType.Arm).Single(p => p.Component.Symmetry == BodyPartSymmetry.Right);
            rightArm = (arm.Id, arm.Component);
        });
        await Pair.RunTicksSync(10);

        int MaskedLayers()
        {
            var sprite = cEntMan.GetComponent<SpriteComponent>(Pair.ToClientUid(human));
            return sprite.AllLayers.OfType<SpriteComponent.Layer>().Count(l => l.ShaderPrototype?.Id.StartsWith("LimbMask") == true);
        }

        int ParamLayers()
        {
            var sprite = cEntMan.GetComponent<SpriteComponent>(Pair.ToClientUid(human));
            return sprite.AllLayers.OfType<SpriteComponent.Layer>().Count(l => l.CopyToShaderParameters != null);
        }

        await client.WaitAssertion(() => Assert.That(MaskedLayers(), Is.Zero, "nothing is masked on a whole body"));
        var paramsBefore = 0;
        await client.WaitPost(() => paramsBefore = ParamLayers());

        await server.WaitPost(() =>
        {
            var limbs = sEntMan.System<LimbSystem>();
            limbs.Amputatate((human, sEntMan.GetComponent<TransformComponent>(human),
                    sEntMan.GetComponent<HumanoidAppearanceComponent>(human), sEntMan.GetComponent<SLBodyComponent>(human)),
                (rightArm.Owner, sEntMan.GetComponent<TransformComponent>(rightArm), sEntMan.GetComponent<MetaDataComponent>(rightArm),
                    rightArm.Comp!));
        });
        await Pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var humanoid = cEntMan.GetComponent<HumanoidAppearanceComponent>(Pair.ToClientUid(human));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(humanoid.PermanentlyHidden, Does.Contain(HumanoidVisualLayers.RArm));
                Assert.That(MaskedLayers(), Is.GreaterThan(0), "layers over the body get the mask");
                Assert.That(ParamLayers(), Is.GreaterThan(paramsBefore), "the missing arm's silhouette feeds the mask");
            }
        });

        await server.WaitPost(() =>
        {
            var limbs = sEntMan.System<LimbSystem>();
            Assert.That(limbs.AttachLimb((human, sEntMan.GetComponent<HumanoidAppearanceComponent>(human)), "right arm",
                (torso.Owner, torso.Comp!), (rightArm.Owner, rightArm.Comp!)));
        });
        await Pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(MaskedLayers(), Is.Zero, "the mask is gone with the arm back");
                Assert.That(ParamLayers(), Is.EqualTo(paramsBefore));
            }
        });
    }
}
