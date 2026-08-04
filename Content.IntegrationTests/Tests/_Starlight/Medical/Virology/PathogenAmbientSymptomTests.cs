using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Virology;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.EntityEffects.Effects.Transform;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

[TestFixture]
public sealed class PathogenAmbientSymptomTests : GameTest
{
    private static readonly (ProtoId<PathogenArchetypePrototype> Archetype, string Signature)[] AmbientArchetypes =
    {
        ("SpaceCold", "AmbientViralShivering"),
        ("ThroatRot", "AmbientBacterialCoughingFit"),
        ("SporeBloom", "AmbientFungalEyeBlur"),
    };

    [Test]
    public async Task AmbientStrainsRollOneCoreTwoFlavoursAndOneSignature()
    {
        var server = Pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var registry = server.System<PathogenRegistrySystem>();

        await server.WaitAssertion(() =>
        {
            foreach (var (archetypeId, signature) in AmbientArchetypes)
            {
                var archetype = prototypes.Index(archetypeId);
                Assert.Multiple(() =>
                {
                    Assert.That(archetype.StageOneSymptomPool, Has.Count.EqualTo(3));
                    Assert.That(archetype.SymptomPool, Has.Count.EqualTo(6));
                    Assert.That(archetype.MinExtraSymptoms, Is.EqualTo(2));
                    Assert.That(archetype.MaxExtraSymptoms, Is.EqualTo(2));
                    Assert.That(archetype.MinStages, Is.EqualTo(3));
                    Assert.That(archetype.MaxStages, Is.EqualTo(3));
                    Assert.That(archetype.CoreSymptoms.Select(id => id.Id), Is.EqualTo(new[] { signature }));
                });

                foreach (var stageOneId in archetype.StageOneSymptomPool)
                {
                    var stageOne = prototypes.Index(stageOneId);
                    Assert.That(stageOne.MinStage, Is.EqualTo(1));
                    Assert.That(
                        stageOne.Effects.Any(effect =>
                            effect is Emote { ShowInChat: true } ||
                            effect is PopupMessage { Type: PopupRecipients.Pvs }),
                        Is.True,
                        $"{stageOneId} must be visible to nearby crew.");
                }

                for (var i = 0; i < 12; i++)
                {
                    var strain = registry.Generate(archetype);
                    var symptoms = strain.Symptoms
                        .Select(id => prototypes.Index(id))
                        .ToList();

                    Assert.Multiple(() =>
                    {
                        Assert.That(symptoms.Count(symptom => symptom.MinStage == 1), Is.EqualTo(1));
                        Assert.That(symptoms.Count(symptom => symptom.MinStage == 2), Is.EqualTo(2));
                        Assert.That(symptoms.Count(symptom => symptom.MinStage == 3), Is.EqualTo(1));
                        Assert.That(strain.Symptoms.Select(id => id.Id), Does.Contain(signature));
                    });
                }
            }
        });
    }

    [Test]
    public async Task FungalSignatureAddsAndRemovesMildBlur()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var statusEffects = server.System<StatusEffectsSystem>();

        EntityUid patient = default;
        await server.WaitPost(() =>
        {
            patient = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            Assert.That(
                statusEffects.TryAddStatusEffectDuration(
                    patient,
                    "PathogenMildBlurStatusEffect",
                    TimeSpan.FromSeconds(2)),
                Is.True);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var blur = entities.GetComponent<BlurryVisionComponent>(patient);
            Assert.That(blur.Magnitude, Is.EqualTo(1.5f).Within(0.001f));
        });

        await server.WaitPost(() =>
            Assert.That(
                statusEffects.TryRemoveStatusEffect(patient, "PathogenMildBlurStatusEffect"),
                Is.True));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
            Assert.That(entities.HasComponent<BlurryVisionComponent>(patient), Is.False));
    }
}
