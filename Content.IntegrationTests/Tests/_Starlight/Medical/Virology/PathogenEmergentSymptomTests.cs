using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Virology;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
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
            new[] { "Sneezing", "ViralSniffling", "ViralWateryEyes" },
            new[]
            {
                "EmergentViralFeverChills",
                "EmergentViralMuscleWeakness",
                "EmergentViralLightHeadedness",
            },
            new[] { "EmergentViralHighFever", "EmergentViralFaintingSpell" }),
        new(
            "GreyLung",
            new[] { "Coughing", "BacterialThroatClearing", "BacterialThroatDiscomfort" },
            new[]
            {
                "EmergentBacterialCoughingFit",
                "EmergentBacterialBreathlessness",
                "EmergentBacterialSevereHoarseness",
            },
            new[] { "EmergentBacterialHypoxicAttack", "EmergentBacterialRespiratoryCollapse" }),
        new(
            "Mycosis",
            new[] { "FungalScratching", "FungalEyeRubbing", "FungalSkinFlaking" },
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
                    Assert.That(archetype.StageOneSymptomPool.Select(id => id.Id), Is.EquivalentTo(data.StageOne));
                    Assert.That(archetype.SymptomPool.Select(id => id.Id), Is.EquivalentTo(data.StageTwo));
                    Assert.That(archetype.CoreSymptoms.Select(id => id.Id), Is.EquivalentTo(data.StageThree));
                    Assert.That(archetype.MinExtraSymptoms, Is.EqualTo(2));
                    Assert.That(archetype.MaxExtraSymptoms, Is.EqualTo(2));
                    Assert.That(archetype.MinStages, Is.EqualTo(3));
                    Assert.That(archetype.MaxStages, Is.EqualTo(3));
                });

                foreach (var symptomId in archetype.SymptomPool)
                {
                    var symptom = prototypes.Index(symptomId);
                    Assert.That(symptom.MinStage, Is.EqualTo(2));
                    Assert.That(symptom.Effects.Any(IsMechanicalStageTwoEffect), Is.True);
                }

                var stageThree = archetype.CoreSymptoms.Select(id => prototypes.Index(id)).ToList();
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
    public async Task NumbFingersDropsOnlyTheActiveHandItem()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var effects = server.System<SharedEntityEffectsSystem>();
        var hands = server.System<SharedHandsSystem>();
        var map = await Pair.CreateTestMap();

        EntityUid patient = default;
        EntityUid item = default;
        await server.WaitPost(() =>
        {
            var coordinates = new MapCoordinates(Vector2.Zero, map.MapId);
            patient = entities.SpawnEntity("MobHuman", coordinates);
            item = entities.SpawnEntity("Crowbar", coordinates);
            Assert.That(hands.TryPickupAnyHand(patient, item), Is.True);
            Assert.That(hands.GetActiveItem(patient), Is.EqualTo(item));

            effects.ApplyEffect(patient, new PathogenDropActiveItem());
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(hands.GetActiveItem(patient), Is.Null);
            Assert.That(entities.Deleted(item), Is.False);
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
