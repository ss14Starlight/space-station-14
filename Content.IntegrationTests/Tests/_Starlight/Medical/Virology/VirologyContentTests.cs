using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Botany;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Research.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

/// <summary>
/// The existence layer: is every piece of virology content present and reachable? These
/// run before anything about behaviour is worth checking, and they cover the failure mode
/// no other test does - content that loads perfectly but can never be reached or read.
/// </summary>
[TestFixture]
public sealed class VirologyContentTests : GameTest
{
    private static readonly string[] Entities =
    [
        "HandheldVirologyMonitor",
        "PathogenAnalyzer",
        "PathogenSwab",
        "PathogenViableCulture",
        "ComputerVirologyDetector",
        "PathogenDecontaminator",
        "PathogenSporePatch",
        "PathogenInjector",
        "BiosealRollerBed",
        "BiosealRollerBedSpawnFolded",
        "ViroculumSeeds",
        "FoodViroculumCap",
    ];

    /// <summary>
    /// Viroculum is a seed prototype rather than an entity, and its produce is the
    /// treatment ingredient - so the seed resolving is not enough on its own.
    /// </summary>
    private const string ViroculumSeed = "viroculum";

    private static readonly string[] Recipes =
    [
        "HandheldVirologyMonitor",
        "PathogenSwab",
        "PathogenAnalyzer",
        "PathogenInjector",
    ];

    [Test]
    public async Task EveryVirologyPrototypeResolves()
    {
        var prototypes = Pair.Server.ResolveDependency<IPrototypeManager>();

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var id in Entities)
                {
                    Assert.That(
                        prototypes.HasIndex<EntityPrototype>(id),
                        Is.True,
                        $"Missing entity prototype '{id}'.");
                }

                Assert.That(
                    prototypes.HasIndex<JobPrototype>("Virologist"),
                    Is.True,
                    "The Virologist job prototype is missing.");

                foreach (var id in Recipes)
                {
                    Assert.That(
                        prototypes.HasIndex<LatheRecipePrototype>(id),
                        Is.True,
                        $"Missing lathe recipe '{id}' - the item exists but nobody can print it.");
                }

                Assert.That(
                    prototypes.TryIndex<SeedPrototype>(ViroculumSeed, out var seed),
                    Is.True,
                    $"Missing seed prototype '{ViroculumSeed}'.");

                Assert.That(
                    seed!.ProductPrototypes,
                    Is.Not.Empty,
                    "Viroculum grows nothing, so the treatment ingredient is unobtainable.");

                foreach (var product in seed.ProductPrototypes)
                {
                    Assert.That(
                        prototypes.HasIndex(product),
                        Is.True,
                        $"Viroculum produces missing entity '{product}'.");
                }
            });
        });
    }

    /// <summary>
    /// A symptom outside every archetype pool is finished work that can never fire. Eleven
    /// of these accumulated before this test existed, complete with written popup text.
    /// </summary>
    [Test]
    public async Task EverySymptomIsReachableAndLocalised()
    {
        var loc = Pair.Server.ResolveDependency<ILocalizationManager>();
        var prototypes = Pair.Server.ResolveDependency<IPrototypeManager>();

        await Pair.Server.WaitAssertion(() =>
        {
            var referenced = new HashSet<string>(
                prototypes.EnumeratePrototypes<PathogenArchetypePrototype>()
                    .SelectMany(archetype => archetype.CoreSymptoms
                        .Concat(archetype.StageOneSymptomPool)
                        .Concat(archetype.SymptomPool))
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

                    var pool = archetype.CoreSymptoms
                        .Concat(archetype.StageOneSymptomPool)
                        .Concat(archetype.SymptomPool)
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
}
