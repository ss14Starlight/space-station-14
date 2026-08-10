using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Starlight.Medical.Virology;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

[TestFixture]
public sealed class VirologyContentTests : GameTest
{
    [Test]
    public async Task EverySymptomIsReachableAndLocalised()
    {
        var loc = Pair.Server.ResolveDependency<ILocalizationManager>();
        var prototypes = Pair.Server.ResolveDependency<IPrototypeManager>();

        await Pair.Server.WaitAssertion(() =>
        {
            var referenced = new HashSet<string>(
                prototypes.EnumeratePrototypes<PathogenArchetypePrototype>()
                    .SelectMany(archetype => archetype.StageThreeSymptoms
                        .Concat(archetype.StageOneSymptoms)
                        .Concat(archetype.StageTwoSymptomPool))
                    .Select(symptom => symptom.Id));

            Assert.Multiple(() =>
            {
                foreach (var symptom in prototypes.EnumeratePrototypes<PathogenSymptomPrototype>())
                {
                    Assert.That(
                        loc.HasString(symptom.Name),
                        Is.True,
                        $"Symptom '{symptom.ID}' has no locale string for '{symptom.Name}'.");

                    Assert.That(
                        referenced.Contains(symptom.ID),
                        Is.True,
                        $"Symptom '{symptom.ID}' is in no archetype pool, so it can never fire.");
                }
            });
        });
    }

    [Test]
    public async Task EveryArchetypeIsLocalisedAndReferencesRealSymptoms()
    {
        var loc = Pair.Server.ResolveDependency<ILocalizationManager>();
        var prototypes = Pair.Server.ResolveDependency<IPrototypeManager>();

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var archetype in prototypes.EnumeratePrototypes<PathogenArchetypePrototype>())
                {
                    Assert.That(
                        loc.HasString(archetype.Name),
                        Is.True,
                        $"Archetype '{archetype.ID}' has no locale string for '{archetype.Name}'.");

                    if (archetype.Description is { } description)
                    {
                        Assert.That(
                            loc.HasString(description),
                            Is.True,
                            $"Archetype '{archetype.ID}' has no locale string for '{description}'.");
                    }

                    var pool = archetype.StageThreeSymptoms
                        .Concat(archetype.StageOneSymptoms)
                        .Concat(archetype.StageTwoSymptomPool)
                        .ToList();

                    Assert.That(
                        pool,
                        Is.Not.Empty,
                        $"Archetype '{archetype.ID}' has no symptoms at all.");

                    foreach (var symptom in pool)
                    {
                        Assert.That(
                            prototypes.HasIndex(symptom),
                            Is.True,
                            $"Archetype '{archetype.ID}' references missing symptom '{symptom}'.");
                    }
                }
            });
        });
    }

    /// <summary>
    /// Selection is by which list a symptom sits in, not by its stage - the generator would
    /// happily roll a stage three symptom out of the stage two pool. The tiering only holds
    /// because the archetypes are authored carefully, so it is checked here rather than left
    /// to good manners.
    /// </summary>
    [Test]
    public async Task ArchetypeSymptomListsHoldOnlyTheirOwnStage()
    {
        var prototypes = Pair.Server.ResolveDependency<IPrototypeManager>();

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var archetype in prototypes.EnumeratePrototypes<PathogenArchetypePrototype>())
                {
                    AssertStage(archetype, archetype.StageOneSymptoms, 1, nameof(archetype.StageOneSymptoms));
                    AssertStage(archetype, archetype.StageTwoSymptomPool, 2, nameof(archetype.StageTwoSymptomPool));
                    AssertStage(archetype, archetype.StageThreeSymptoms, 3, nameof(archetype.StageThreeSymptoms));
                }
            });

            void AssertStage(
                PathogenArchetypePrototype archetype,
                List<ProtoId<PathogenSymptomPrototype>> symptoms,
                int expected,
                string field)
            {
                foreach (var symptom in symptoms)
                {
                    if (!prototypes.TryIndex(symptom, out var proto))
                        continue;

                    Assert.That(
                        proto.MinStage,
                        Is.EqualTo(expected),
                        $"Archetype '{archetype.ID}' lists '{symptom}' under {field}, " +
                        $"but that symptom is stage {proto.MinStage}.");
                }
            }
        });
    }
}
