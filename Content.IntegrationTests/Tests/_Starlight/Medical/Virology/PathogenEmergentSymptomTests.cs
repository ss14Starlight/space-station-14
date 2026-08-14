using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Virology;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

[TestFixture]
public sealed class PathogenEmergentSymptomTests : GameTest
{
    private static readonly EmergentArchetypeData[] EmergentArchetypes =
    {
        new(
            "StationFlu",
            new[] { "Sneezing", "CoreViralSniffling", "CoreViralWateryEyes" },
            new[]
            {
                "EmergentViralFeverChills",
                "EmergentViralMuscleWeakness",
                "EmergentViralLightHeadedness",
            },
            new[] { "EmergentViralHighFever", "EmergentViralFaintingSpell" }),
        new(
            "GreyLung",
            new[] { "Coughing", "CoreBacterialThroatClearing", "CoreBacterialThroatDiscomfort" },
            new[]
            {
                "EmergentBacterialCoughingFit",
                "EmergentBacterialBreathlessness",
                "EmergentBacterialSevereHoarseness",
            },
            new[] { "EmergentBacterialHypoxicAttack", "EmergentBacterialRespiratoryCollapse" }),
        new(
            "Mycosis",
            new[] { "CoreFungalSkinIrritation", "CoreFungalEyeIrritation", "CoreFungalFlakingSkin" },
            new[]
            {
                "EmergentFungalEyeInflammation",
                "EmergentFungalCramps",
                "EmergentFungalNumbFingers",
            },
            new[] { "EmergentFungalToxicMycosis", "EmergentFungalOcularSporeFlare" }),
    };

    [Test]
    public async Task EmergentStrainsRollOneCoreTwoMechanicalSymptomsAndBothSignatures()
    {
        var server = Pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var registry = server.System<PathogenRegistrySystem>();

        await server.WaitAssertion(() =>
        {
            foreach (var data in EmergentArchetypes)
            {
                var archetype = prototypes.Index<PathogenArchetypePrototype>(data.Archetype);
                Assert.Multiple(() =>
                {
                    Assert.That(archetype.StageOneSymptoms.Select(id => id.Id), Is.EquivalentTo(data.StageOne));
                    Assert.That(archetype.StageTwoSymptomPool.Select(id => id.Id), Is.EquivalentTo(data.StageTwo));
                    Assert.That(archetype.StageThreeSymptoms.Select(id => id.Id), Is.EquivalentTo(data.StageThree));
                    Assert.That(archetype.MinStages, Is.EqualTo(3));
                    Assert.That(archetype.MaxStages, Is.EqualTo(3));
                });

                foreach (var symptomId in archetype.StageTwoSymptomPool)
                {
                    var symptom = prototypes.Index(symptomId);
                    Assert.That(symptom.MinStage, Is.EqualTo(2));
                    Assert.That(symptom.Effects.Any(IsMechanicalStageTwoEffect), Is.True);
                }

                var stageThree = archetype.StageThreeSymptoms.Select(id => prototypes.Index(id)).ToList();
                Assert.Multiple(() =>
                {
                    Assert.That(stageThree.All(symptom => symptom.MinStage == 3), Is.True);
                    Assert.That(
                        stageThree.SelectMany(symptom => symptom.Effects).OfType<PathogenCappedDamage>().Count(),
                        Is.EqualTo(1));
                });

                for (var i = 0; i < 12; i++)
                {
                    var strain = registry.Generate(archetype);
                    var symptoms = strain.Symptoms.Select(id => prototypes.Index(id)).ToList();

                    Assert.Multiple(() =>
                    {
                        Assert.That(symptoms.Count(symptom => symptom.MinStage == 1), Is.EqualTo(1));
                        Assert.That(symptoms.Count(symptom => symptom.MinStage == 2), Is.EqualTo(2));
                        Assert.That(symptoms.Count(symptom => symptom.MinStage == 3), Is.EqualTo(2));
                        Assert.That(strain.Symptoms.Select(id => id.Id), Does.Contain(data.StageThree[0]));
                        Assert.That(strain.Symptoms.Select(id => id.Id), Does.Contain(data.StageThree[1]));
                    });
                }
            }
        });
    }

    [Test]
    public async Task EmergentDamageStopsExactlyAtItsAuthoredCap()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var effects = server.System<SharedEntityEffectsSystem>();

        EntityUid patient = default;
        await server.WaitPost(() =>
        {
            patient = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var effect = new PathogenCappedDamage
            {
                DamageType = new ProtoId<DamageTypePrototype>("Heat"),
                Amount = 3,
                Maximum = 15,
            };

            for (var i = 0; i < 10; i++)
                effects.ApplyEffect(patient, effect);
        });

        await server.WaitAssertion(() =>
        {
            var damage = entities.GetComponent<DamageableComponent>(patient);
            Assert.That(damage.Damage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(15)));
        });
    }

    [Test]
    public async Task EmergentDamageCapIgnoresPreExistingDamage()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var damageable = server.System<DamageableSystem>();
        var effects = server.System<SharedEntityEffectsSystem>();

        EntityUid patient = default;
        await server.WaitPost(() =>
        {
            patient = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);

            var preexisting = new DamageSpecifier();
            preexisting.DamageDict["Heat"] = FixedPoint2.New(10);
            Assert.That(damageable.TryChangeDamage(patient, preexisting, true), Is.True);

            var infections = entities.EnsureComponent<PathogenInfectionComponent>(patient);
            infections.Infections.Add(new PathogenInfection { Pathogen = 1 });

            var effect = new PathogenCappedDamage
            {
                DamageType = new ProtoId<DamageTypePrototype>("Heat"),
                Amount = 3,
                Maximum = 6,
            };

            for (var i = 0; i < 3; i++)
                effects.ApplyEffect(patient, effect);
        });

        await server.WaitAssertion(() =>
        {
            var damage = entities.GetComponent<DamageableComponent>(patient);
            Assert.That(damage.Damage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(16)));
        });
    }

    [Test]
    public async Task NumbFingersDropsOnlyTheActiveHandItem()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var effects = server.System<SharedEntityEffectsSystem>();
        var hands = server.System<SharedHandsSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid patient = default;
        EntityUid active = default;
        EntityUid offHand = default;
        var pickedUpActive = false;
        var pickedUpOffHand = false;
        EntityUid? activeBefore = null;
        await server.WaitPost(() =>
        {
            var coordinates = new MapCoordinates(Vector2.Zero, map.MapId);
            patient = entities.SpawnEntity("MobHuman", coordinates);
            active = entities.SpawnEntity("Crowbar", coordinates);
            offHand = entities.SpawnEntity("Crowbar", coordinates);

            pickedUpActive = hands.TryPickupAnyHand(patient, active);
            pickedUpOffHand = hands.TryPickupAnyHand(patient, offHand);
            activeBefore = hands.GetActiveItem(patient);
        });

        // The second pickup has to land in a free hand, leaving the first one active.
        Assert.Multiple(() =>
        {
            Assert.That(pickedUpActive, Is.True, "first item was not picked up");
            Assert.That(pickedUpOffHand, Is.True, "second item was not picked up");
            Assert.That(activeBefore, Is.EqualTo(active), "the first item should still be the active one");
        });

        await server.WaitPost(() => effects.ApplyEffect(patient, new PathogenDropActiveItem()));

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(hands.GetActiveItem(patient), Is.Null);
                Assert.That(hands.IsHolding(patient, active), Is.False);
                Assert.That(hands.IsHolding(patient, offHand), Is.True);
                Assert.That(entities.Deleted(active), Is.False);
                Assert.That(entities.Deleted(offHand), Is.False);
            });
        });
    }

    private static bool IsMechanicalStageTwoEffect(EntityEffect effect)
        => effect is MovementSpeedModifier or ModifyStatusEffect or Jitter or PathogenDropActiveItem;

    private sealed record EmergentArchetypeData(
        ProtoId<PathogenArchetypePrototype> Archetype,
        string[] StageOne,
        string[] StageTwo,
        string[] StageThree);
}
