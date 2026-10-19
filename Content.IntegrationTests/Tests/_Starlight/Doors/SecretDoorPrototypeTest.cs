using Content.IntegrationTests.Fixtures;
using Content.Server.Destructible;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Damage.Components;
using Content.Shared.Destructible.Thresholds.Triggers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Doors;

public sealed class SecretDoorPrototypeTest : GameTest
{
    private static readonly ProtoId<ConstructionGraphPrototype> SecretDoorGraph = "SecretDoor";
    private static readonly EntProtoId NormalDoor = "SolidSecretDoor";
    private static readonly EntProtoId ReinforcedDoor = "ReinforcedSecretDoor";

    [Test]
    public async Task ReinforcedFacadeKeepsNormalDoorDurabilityAndConstructionLinks()
        => await Pair.Server.WaitPost(() =>
        {
            var prototypes = Pair.Server.ResolveDependency<IPrototypeManager>();
            var components = Pair.Server.ResolveDependency<IComponentFactory>();
            var graph = prototypes.Index(SecretDoorGraph);

            Assert.That(graph.Edge("solidSecretDoor", "reinforcedSecretDoor"), Is.Not.Null);
            Assert.That(graph.Edge("reinforcedSecretDoor", "solidSecretDoor"), Is.Not.Null);

            var normal = prototypes.Index(NormalDoor);
            var reinforced = prototypes.Index(ReinforcedDoor);
            Assert.That(normal.TryComp<DamageableComponent>(out var normalDamage, components), Is.True);
            Assert.That(reinforced.TryComp<DamageableComponent>(out var reinforcedDamage, components), Is.True);
            Assert.That(reinforcedDamage!.DamageModifierSetId, Is.EqualTo(normalDamage!.DamageModifierSetId));

            Assert.That(normal.TryComp<DestructibleComponent>(out var normalDestructible, components), Is.True);
            Assert.That(reinforced.TryComp<DestructibleComponent>(out var reinforcedDestructible, components), Is.True);
            Assert.That(reinforcedDestructible!.Thresholds.Count, Is.EqualTo(normalDestructible!.Thresholds.Count));
            Assert.That(reinforcedDestructible.Thresholds[0].Trigger, Is.TypeOf<DamageTrigger>());
            Assert.That(((DamageTrigger) reinforcedDestructible.Thresholds[0].Trigger!).Damage,
                Is.EqualTo(((DamageTrigger) normalDestructible.Thresholds[0].Trigger!).Damage));
        });
}
