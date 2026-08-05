using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Botany;
using Content.Server.Station.Components;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Lathe;
using Content.Shared.Maps;
using Content.Shared.Research.Prototypes;
using Content.Shared.Roles;
using Content.Shared.VendingMachines;
using Robust.Shared.GameObjects;
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
    private static readonly EntProtoId[] Entities =
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
    private static readonly ProtoId<SeedPrototype> ViroculumSeed = "viroculum";

    private static readonly ProtoId<JobPrototype> Virologist = "Virologist";
    private static readonly EntProtoId VirologistLocker = "LockerVirologistFilled";
    private static readonly ProtoId<VendingMachineInventoryPrototype> ViroDrobe = "ViroDrobeInventory";
    private static readonly ProtoId<StartingGearPrototype> VirologistGear = "VirologistGear";

    private static readonly ProtoId<LatheRecipePrototype>[] Recipes =
    [
        "HandheldVirologyMonitor",
        "PathogenSwab",
        "PathogenAnalyzer",
        "PathogenInjector",
    ];

    /// <summary>
    /// Existence is not reachability. The job had gear, loadouts, a salary, a department
    /// entry, an ID card and lockers placed on five stations, and was still unplayable
    /// because no station offered a slot for it.
    /// </summary>
    [Test]
    public async Task VirologistJobIsPlayable()
    {
        var server = Pair.Server;
        var componentFactory = server.ResolveDependency<IComponentFactory>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var inDepartment = prototypes.EnumeratePrototypes<DepartmentPrototype>()
                .Any(department => department.Roles.Contains(Virologist));

            Assert.That(
                inDepartment,
                Is.True,
                $"'{Virologist}' is in no department, so it never appears in the lobby.");

            var jobsName = componentFactory.GetComponentName<StationJobsComponent>();
            var offeringMaps = new List<string>();

            foreach (var map in prototypes.EnumeratePrototypes<GameMapPrototype>())
            {
                foreach (var (_, station) in map.Stations)
                {
                    if (!station.StationComponentOverrides.TryGetValue(jobsName, out var entry) ||
                        entry.Component is not StationJobsComponent jobs)
                    {
                        continue;
                    }

                    // Enumerating is a read; ContainsKey counts as an execute access and
                    // the analyzer refuses it on this component.
                    foreach (var (job, _) in jobs.SetupAvailableJobs)
                    {
                        if (job != Virologist)
                            continue;

                        offeringMaps.Add(map.ID);
                        break;
                    }
                }
            }

            Assert.That(
                offeringMaps,
                Is.Not.Empty,
                $"No station offers '{Virologist}', so nobody can pick the role in a round.");
        });
    }

    /// <summary>
    /// A recipe prototype existing does not mean anything can print it. The recipe has to
    /// be in a pack that some lathe actually carries.
    /// </summary>
    [Test]
    public async Task EveryRecipeIsPrintableBySomeLathe()
    {
        var server = Pair.Server;
        var componentFactory = server.ResolveDependency<IComponentFactory>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var latheName = componentFactory.GetComponentName<LatheComponent>();
            var printable = new HashSet<string>();

            foreach (var proto in prototypes.EnumeratePrototypes<EntityPrototype>())
            {
                if (!proto.Components.TryGetValue(latheName, out var entry) ||
                    entry.Component is not LatheComponent lathe)
                {
                    continue;
                }

                foreach (var pack in lathe.StaticPacks.Concat(lathe.DynamicPacks))
                {
                    if (prototypes.TryIndex(pack, out var packProto))
                        printable.UnionWith(packProto.Recipes.Select(recipe => recipe.Id));
                }
            }

            Assert.Multiple(() =>
            {
                foreach (var recipe in Recipes)
                {
                    Assert.That(
                        printable,
                        Does.Contain(recipe.Id),
                        $"Recipe '{recipe}' is in no pack any lathe carries, so nothing can print it.");
                }
            });
        });
    }

    /// <summary>
    /// The equipment was deliberately distributed without map edits, which means the
    /// vendor and locker fills are the only way most of it reaches the crew.
    /// </summary>
    [Test]
    public async Task VirologyEquipmentIsDistributed()
    {
        var prototypes = Pair.Server.ResolveDependency<IPrototypeManager>();

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    prototypes.HasIndex(VirologistLocker),
                    Is.True,
                    "The virologist locker is missing, and it is placed on five station maps.");

                Assert.That(
                    prototypes.TryIndex(ViroDrobe, out var vendor),
                    Is.True,
                    "The ViroDrobe inventory is missing.");

                foreach (var (item, _) in vendor!.StartingInventory)
                {
                    Assert.That(
                        prototypes.HasIndex<EntityPrototype>(item),
                        Is.True,
                        $"ViroDrobe stocks missing entity '{item}'.");
                }

                Assert.That(
                    prototypes.TryIndex(VirologistGear, out var gear),
                    Is.True,
                    "The Virologist starting gear is missing.");

                foreach (var (slot, item) in gear!.Equipment)
                {
                    Assert.That(
                        prototypes.HasIndex(item),
                        Is.True,
                        $"Virologist gear slot '{slot}' references missing entity '{item}'.");
                }
            });
        });
    }

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
                        prototypes.HasIndex(id),
                        Is.True,
                        $"Missing entity prototype '{id}'.");
                }

                Assert.That(
                    prototypes.HasIndex(Virologist),
                    Is.True,
                    "The Virologist job prototype is missing.");

                foreach (var id in Recipes)
                {
                    Assert.That(
                        prototypes.HasIndex(id),
                        Is.True,
                        $"Missing lathe recipe '{id}' - the item exists but nobody can print it.");
                }

                Assert.That(
                    prototypes.TryIndex(ViroculumSeed, out var seed),
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
