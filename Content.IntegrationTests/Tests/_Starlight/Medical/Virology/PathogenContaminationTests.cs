using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Fluids.EntitySystems;
using Content.Server._Starlight.Medical.Virology;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Chemistry.Components;
using Content.Shared.Disposal.Unit;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

[TestFixture]
public sealed class PathogenContaminationTests : GameTest
{
    private static readonly EntProtoId BananaPeel = "TrashBananaPeel";
    private static readonly EntProtoId FoodMeatRotten = "FoodMeatRotten";
    private static readonly EntProtoId RawMeat = "FoodMeat";
    private static readonly EntProtoId StorageCrate = "CrateGenericSteel";
    private static readonly ProtoId<TagPrototype> OrganicTrashTag = "OrganicTrash";
    private static readonly ProtoId<TagPrototype> RottenFoodTag = "PathogenRottenFood";
    private static readonly EntProtoId[] OrganicTrashPrototypes =
    [
        "FoodPacketBoritosTrash",
        "FoodTinPeachesTrash",
        "FoodPlateTrash",
        "FoodBowlBigTrash",
        BananaPeel,
        "FoodCornTrash",
        "FoodBungoPit",
        "TrashCherryPit",
    ];

    [TestCase(0f, 0.06f, 2.4f, 0f)]
    [TestCase(20f, 0.06f, 2.4f, 1.2f)]
    [TestCase(100f, 0.06f, 2.4f, 2.4f)]
    [TestCase(20f, 0.03f, 1.2f, 0.6f)]
    [TestCase(100f, 0.03f, 1.2f, 1.2f)]
    [TestCase(100f, 0.06f, 1.8f, 1.8f)]
    [TestCase(-10f, 0.06f, 2.4f, 0f)]
    public void PuddleContaminationIsBounded(
        float volume,
        float perUnit,
        float maximum,
        float expected)
    {
        Assert.That(
            PathogenContaminationMath.PuddleContamination(volume, perUnit, maximum),
            Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public async Task OrganicWastePrototypesCarryContaminationTag()
    {
        var server = Pair.Server;
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var prototypeId in OrganicTrashPrototypes)
            {
                var prototype = proto.Index<EntityPrototype>(prototypeId);
                Assert.That(
                    prototype.TryGetComponent<TagComponent>(
                        out var tags,
                        server.EntMan.ComponentFactory),
                    Is.True,
                    $"{prototypeId} should have a Tag component");
                Assert.That(
                    tags!.Tags,
                    Does.Contain(OrganicTrashTag),
                    $"{prototypeId} should be sampled as organic trash");
            }
        });
    }

    [Test]
    public async Task RottenFoodPrototypeCarriesContaminationTag()
    {
        var server = Pair.Server;
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var prototype = proto.Index<EntityPrototype>(FoodMeatRotten);
            Assert.That(
                prototype.TryGetComponent<TagComponent>(
                    out var tags,
                    server.EntMan.ComponentFactory),
                Is.True,
                $"{FoodMeatRotten} should have a Tag component");
            Assert.That(
                tags!.Tags,
                Does.Contain(RottenFoodTag),
                $"{FoodMeatRotten} should be sampled as rotten food");
        });
    }

    /// <summary>
    /// Rubbish is found by tag and rot is found by component, so disposal has to silence
    /// both. It marks instead of clearing tags for exactly that reason - a rotting steak
    /// carries no tag to clear, and used to start counting again the moment the pipes
    /// dumped it onto the disposal room floor.
    /// </summary>
    [Test]
    public async Task DisposalSilencesTaggedAndRottingSourcesAlike()
    {
        var testMap = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        var sources = server.System<PathogenContaminationSourceSystem>();

        EntityUid trash = default;
        EntityUid rotten = default;
        EntityUid rotting = default;

        await server.WaitPost(() =>
        {
            sources.ResetSourceStateForTest();
            sources.SampleSourcesForTest();

            trash = entities.SpawnEntity(BananaPeel, testMap.GridCoords);
            rotten = entities.SpawnEntity(FoodMeatRotten, testMap.GridCoords);
            rotting = entities.SpawnEntity(RawMeat, testMap.GridCoords);
            entities.EnsureComponent<RottingComponent>(rotting);
            sources.SampleSourcesForTest();
        });

        await server.WaitAssertion(() =>
            Assert.That(
                sources.ActiveSourceCount,
                Is.GreaterThanOrEqualTo(3),
                "All three have to count before disposal means anything."));

        // Disposal ejects things back onto the floor, so being uncontained afterwards must
        // not be enough to bring them back.
        await server.WaitPost(() =>
        {
            entities.EnsureComponent<BeingDisposedComponent>(trash);
            entities.EnsureComponent<BeingDisposedComponent>(rotten);
            entities.EnsureComponent<BeingDisposedComponent>(rotting);
            sources.SampleSourcesForTest();
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<PathogenDisposedComponent>(trash), Is.True);
                Assert.That(entities.HasComponent<PathogenDisposedComponent>(rotten), Is.True);
                Assert.That(entities.HasComponent<PathogenDisposedComponent>(rotting), Is.True);
                Assert.That(sources.ActiveSourceCount, Is.Zero);
            });
        });

        await server.WaitPost(() =>
        {
            entities.DeleteEntity(trash);
            entities.DeleteEntity(rotten);
            entities.DeleteEntity(rotting);
        });
    }

    /// <summary>
    /// Rot is the only collector that used to skip these guards, so a rotting steak kept
    /// contaminating the station from inside a bag, a crate or a disposal unit.
    /// </summary>
    [Test]
    public async Task RotSourcesIgnoreContainedAndUngriddedEntities()
    {
        var testMap = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        var containers = server.System<SharedContainerSystem>();
        var sources = server.System<PathogenContaminationSourceSystem>();

        EntityUid meat = default;
        EntityUid crate = default;

        await server.WaitPost(() =>
        {
            sources.ResetSourceStateForTest();
            sources.SampleSourcesForTest();

            meat = entities.SpawnEntity(RawMeat, testMap.GridCoords);
            crate = entities.SpawnEntity(StorageCrate, testMap.GridCoords);
            entities.EnsureComponent<RottingComponent>(meat);
            sources.SampleSourcesForTest();
        });

        await server.WaitAssertion(() =>
            Assert.That(
                sources.SourceReport.Any(report =>
                    report.Kind == PathogenContaminationSourceKind.RottingEdible),
                Is.True,
                "Rotting meat on the floor has to count in the first place."));

        await server.WaitPost(() =>
        {
            var container = containers.EnsureContainer<Container>(crate, "test_storage");
            Assert.That(containers.Insert(meat, container), Is.True);
            sources.SampleSourcesForTest();
        });

        await server.WaitAssertion(() =>
            Assert.That(
                sources.SourceReport.Any(report =>
                    report.Kind == PathogenContaminationSourceKind.RottingEdible),
                Is.False,
                "Stowing it in a crate is the crew dealing with it."));

        await server.WaitPost(() =>
        {
            entities.DeleteEntity(meat);
            entities.DeleteEntity(crate);
        });
    }

    [Test]
    public async Task FirstSampleBaselinesExistingPhysicalSources()
    {
        var testMap = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        var contamination = server.System<PathogenContaminationSystem>();
        var sources = server.System<PathogenContaminationSourceSystem>();
        EntityUid baselineTrash = default;
        EntityUid newTrash = default;

        await server.WaitPost(() =>
        {
            sources.ResetSourceStateForTest();
            baselineTrash = entities.SpawnEntity(BananaPeel, testMap.GridCoords);
            sources.SampleSourcesForTest();
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(sources.HasBaseline, Is.True);
            Assert.That(sources.ActiveSourceCount, Is.Zero);
            Assert.That(sources.BaselineSourceCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(sources.IgnoredBaselineSourceCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(contamination.GetContamination(PathogenType.Bacteria), Is.Zero);
            Assert.That(
                sources.IgnoredBaselineReport.Any(report =>
                    report.Kind == PathogenContaminationSourceKind.OrganicTrash),
                Is.True);
        });

        await server.WaitPost(() =>
        {
            newTrash = entities.SpawnEntity(BananaPeel, testMap.GridCoords);
            sources.SampleSourcesForTest();
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(sources.ActiveSourceCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(sources.IgnoredBaselineSourceCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(contamination.GetContamination(PathogenType.Bacteria), Is.GreaterThan(0f));
            Assert.That(
                sources.SourceReport.Any(report =>
                    report.Kind == PathogenContaminationSourceKind.OrganicTrash),
                Is.True);
        });

        await server.WaitPost(() =>
        {
            entities.DeleteEntity(baselineTrash);
            entities.DeleteEntity(newTrash);
        });
    }

    [Test]
    public async Task RottenFoodSourcesContributeAfterBaseline()
    {
        var testMap = await Pair.CreateTestMap();
        var server = Pair.Server;
        var entities = server.EntMan;
        var contamination = server.System<PathogenContaminationSystem>();
        var sources = server.System<PathogenContaminationSourceSystem>();
        EntityUid rottenFood = default;

        await server.WaitPost(() =>
        {
            sources.ResetSourceStateForTest();
            sources.SampleSourcesForTest();
        });

        await server.WaitPost(() =>
        {
            rottenFood = entities.SpawnEntity(FoodMeatRotten, testMap.GridCoords);
            sources.SampleSourcesForTest();
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(sources.ActiveSourceCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(contamination.GetContamination(PathogenType.Bacteria), Is.GreaterThan(0f));
            Assert.That(contamination.GetContamination(PathogenType.Fungus), Is.GreaterThan(0f));
            Assert.That(
                sources.SourceReport.Any(report =>
                    report.Kind == PathogenContaminationSourceKind.SpoiledFood),
                Is.True);
        });

        await server.WaitPost(() => entities.DeleteEntity(rottenFood));
    }

    [Test]
    public async Task MoldPuddlesContributeBacteriaAndFungus()
    {
        var testMap = await Pair.CreateTestMap();
        var server = Pair.Server;
        var contamination = server.System<PathogenContaminationSystem>();
        var puddles = server.System<PuddleSystem>();
        var sources = server.System<PathogenContaminationSourceSystem>();
        EntityUid puddle = default;

        await server.WaitPost(() =>
        {
            sources.ResetSourceStateForTest();
            sources.SampleSourcesForTest();

            var solution = new Solution("Mold", FixedPoint2.New(100));
            Assert.That(puddles.TrySpillAt(testMap.Tile, solution, out puddle), Is.True);
            sources.SampleSourcesForTest();
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    contamination.GetContamination(PathogenType.Bacteria),
                    Is.EqualTo(1.8f).Within(0.0001f));
                Assert.That(
                    contamination.GetContamination(PathogenType.Fungus),
                    Is.EqualTo(1.8f).Within(0.0001f));
            });
        });

        await server.WaitPost(() => server.EntMan.DeleteEntity(puddle));
    }

    [Test]
    public void ContaminationPoolTracksTypedContributions()
    {
        var pool = new PathogenContaminationPool();

        pool.Set(new Dictionary<PathogenType, float>
        {
            [PathogenType.Virus] = 10f,
            [PathogenType.Bacteria] = 30f,
            [PathogenType.Fungus] = 20f,
        });

        Assert.Multiple(() =>
        {
            Assert.That(pool.Total, Is.EqualTo(60f).Within(0.0001f));
            Assert.That(pool.Get(PathogenType.Virus), Is.EqualTo(10f).Within(0.0001f));
            Assert.That(pool.Get(PathogenType.Bacteria), Is.EqualTo(30f).Within(0.0001f));
            Assert.That(pool.Get(PathogenType.Fungus), Is.EqualTo(20f).Within(0.0001f));
            Assert.That(
                pool.GetDominantTypes(),
                Is.EqualTo(new[] { PathogenType.Bacteria }));
        });
    }

    [Test]
    public void NewSourceSnapshotReplacesPreviousContamination()
    {
        var pool = new PathogenContaminationPool();
        pool.Set(new Dictionary<PathogenType, float>
        {
            [PathogenType.Virus] = 20f,
            [PathogenType.Bacteria] = 30f,
        });

        pool.Set(new Dictionary<PathogenType, float>
        {
            [PathogenType.Fungus] = 9f,
        });

        Assert.Multiple(() =>
        {
            Assert.That(pool.Total, Is.EqualTo(9f).Within(0.0001f));
            Assert.That(pool.Get(PathogenType.Virus), Is.Zero);
            Assert.That(pool.Get(PathogenType.Bacteria), Is.Zero);
            Assert.That(pool.Get(PathogenType.Fungus), Is.EqualTo(9f).Within(0.0001f));
        });
    }

    [Test]
    public void BatchedContributionsPreserveCompositionAtCap()
    {
        var pool = new PathogenContaminationPool();
        pool.Set(new Dictionary<PathogenType, float>
        {
            [PathogenType.Virus] = 90f,
            [PathogenType.Bacteria] = 20f,
            [PathogenType.Fungus] = 20f,
        });

        Assert.Multiple(() =>
        {
            Assert.That(pool.Total, Is.EqualTo(100f).Within(0.0001f));
            Assert.That(pool.Get(PathogenType.Virus), Is.EqualTo(69.23077f).Within(0.0001f));
            Assert.That(pool.Get(PathogenType.Bacteria), Is.EqualTo(15.38462f).Within(0.0001f));
            Assert.That(pool.Get(PathogenType.Fungus), Is.EqualTo(15.38462f).Within(0.0001f));
        });
    }
}
