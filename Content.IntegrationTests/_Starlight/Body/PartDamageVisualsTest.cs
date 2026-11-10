// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.PartDamage;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._Starlight.Body;

[TestFixture]
public sealed class PartDamageVisualsTest : GameTest
{
    private const string BluntDamagePrototype = "Blunt";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task OverlaysFollowParts()
    {
        var server = Pair.Server;
        var client = Pair.Client;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();

        EntityUid human = default;
        await server.WaitPost(() => human = sEntMan.SpawnEntity("MobHuman", map.GridCoords));
        await Pair.RunTicksSync(10);

        await server.WaitPost(() =>
        {
            var partDamage = sEntMan.System<BodyPartDamageSystem>();
            var arm = partDamage.GetDamageableParts((human, sEntMan.GetComponent<SLBodyComponent>(human)))
                .First(p => sEntMan.GetComponent<SLBodyPartComponent>(p).Kind == BodyPartType.Arm
                    && sEntMan.GetComponent<SLBodyPartComponent>(p).Symmetry == BodyPartSymmetry.Left);
            using (partDamage.Target(human, BodyDamageTargeting.AtPart(arm)))
                sEntMan.System<DamageableSystem>().ChangeDamage(human,
                    new DamageSpecifier(protoMan.Index<DamageTypePrototype>(BluntDamagePrototype), 40), ignoreResistances: true);
        });
        await Pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var uid = Pair.ToClientUid(human);
            var sprite = cEntMan.System<SpriteSystem>();

            string? State(string key)
                => sprite.LayerMapTryGet((uid, null), key, out var index, false)
                    && sprite.TryGetLayer((uid, null), index, out var layer, false)
                    && layer.Visible
                    ? layer.State.Name
                    : null;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(State("LArmBrute"), Is.Not.Null.And.Not.EndsWith("_0"), "the hit arm is bruised");
                Assert.That(State("RArmBrute"), Is.Null.Or.EndsWith("_0"), "the other arm is not");
                Assert.That(State("ChestBrute"), Is.Null.Or.EndsWith("_0"), "neither is the chest");
            }
        });
    }
}
