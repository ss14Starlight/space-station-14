using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Virology;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Disposal.Unit;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

[TestFixture]
public sealed class PathogenContaminationTests : GameTest
{
    private static readonly ProtoId<PathogenArchetypePrototype> StationFlu = "StationFlu";
    private static readonly EntProtoId SporePatch = "PathogenSporePatch";
    private static readonly EntProtoId BananaPeel = "TrashBananaPeel";
    private static readonly ProtoId<TagPrototype> OrganicTrashTag = "OrganicTrash";
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

    [Test]
    public void MilestonesAreOrderedAndOneShot()
    {
        var milestones = new PathogenContaminationMilestones();

        Assert.That(milestones.GetPending(24.9f), Is.Empty);
        Assert.That(
            milestones.GetPending(50f),
            Is.EqualTo(new[]
            {
                PathogenContaminationMilestone.AmbientLow,
                PathogenContaminationMilestone.Emergent,
            }));

        milestones.MarkHandled(PathogenContaminationMilestone.AmbientLow);
        milestones.MarkHandled(PathogenContaminationMilestone.Emergent);

        Assert.That(milestones.GetPending(49f), Is.Empty);
        Assert.That(
            milestones.GetPending(75f),
            Is.EqualTo(new[] { PathogenContaminationMilestone.AmbientHigh }));

        milestones.MarkHandled(PathogenContaminationMilestone.AmbientHigh);
        Assert.That(milestones.GetPending(100f), Is.Empty);

        milestones.Reset();
        Assert.That(
            milestones.GetPending(100f),
            Is.EqualTo(new[]
            {
                PathogenContaminationMilestone.AmbientLow,
                PathogenContaminationMilestone.Emergent,
                PathogenContaminationMilestone.AmbientHigh,
            }));
    }

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
    public async Task DisposalTransitionRemovesOrganicTrashTag()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var tags = server.System<TagSystem>();
        EntityUid trash = default;

        await server.WaitAssertion(() =>
        {
            trash = entities.SpawnEntity(BananaPeel, MapCoordinates.Nullspace);
            Assert.That(tags.HasTag(trash, OrganicTrashTag), Is.True);

            entities.EnsureComponent<BeingDisposedComponent>(trash);

            Assert.That(tags.HasTag(trash, OrganicTrashTag), Is.False);
        });

        await server.WaitPost(() => entities.DeleteEntity(trash));
    }

    [TestCase(2.3f, 2.4f, 0.016666667f, 0f, 0f)]
    [TestCase(2.3f, 2.4f, 0.016666667f, 0.01f, 0.01f)]
    [TestCase(3f, 2.4f, 0.016666667f, 0f, 0.05f)]
    [TestCase(100f, 2.4f, 1f, 0f, 1f)]
    public void SourceInfectionChanceUsesThresholdAndMinimum(
        float contamination,
        float threshold,
        float scale,
        float minimum,
        float expected)
    {
        Assert.That(
            PathogenContaminationMath.SourceInfectionChance(contamination, threshold, scale, minimum),
            Is.EqualTo(expected).Within(0.0001f));
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

    [Test]
    public async Task ReducedEmergentProfileKeepsThreeStages()
    {
        var server = Pair.Server;
        var proto = server.ResolveDependency<IPrototypeManager>();
        var registry = server.System<PathogenRegistrySystem>();

        await server.WaitAssertion(() =>
        {
            var archetype = proto.Index(StationFlu);
            var strain = registry.Generate(archetype, new PathogenGenerationOptions
            {
                MaxPrevalenceCap = 0.08f,
                TransmissibilityMultiplier = 0.7f,
                ProtectionBypassMultiplier = 0.5f,
                StageDelayMultiplier = 1.25f,
                MinExtraSymptoms = 1,
                MaxExtraSymptoms = 2,
            });

            Assert.Multiple(() =>
            {
                Assert.That(strain.Tier, Is.EqualTo(PathogenTier.Emergent));
                Assert.That(strain.RespawnOnExtinction, Is.False);
                Assert.That(strain.MaxStage, Is.EqualTo(3));
                Assert.That(strain.MaxPrevalence, Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(strain.Transmissibility,
                    Is.InRange(archetype.MinTransmissibility * 0.7f, archetype.MaxTransmissibility * 0.7f));
                Assert.That(strain.ProtectionBypass, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(strain.StageDelay.TotalSeconds,
                    Is.InRange(
                        archetype.MinStageDelay.TotalSeconds * 1.25,
                        archetype.MaxStageDelay.TotalSeconds * 1.25));
                // Generation always adds one stage-one symptom before the core set, so a
                // strain carries core + 1 + however many extras the roll asked for. The
                // old range omitted the stage-one pick and so failed whenever the extras
                // roll came up 2 - a coin flip on every run.
                Assert.That(strain.Symptoms.Count,
                    Is.InRange(archetype.CoreSymptoms.Count + 2, archetype.CoreSymptoms.Count + 3));
            });
        });
    }

    [Test]
    public async Task SporePatchPrototypeCarriesPinnedStrainComponent()
    {
        var server = Pair.Server;
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var patch = proto.Index<EntityPrototype>(SporePatch);

            Assert.That(
                patch.TryGetComponent<PathogenSporePatchComponent>(
                    out _,
                    server.EntMan.ComponentFactory),
                Is.True);
        });
    }

    [Test]
    public void MonitorUiTypesAreNetworkSerializable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                Attribute.IsDefined(
                    typeof(PathogenDetectorUiKey),
                    typeof(NetSerializableAttribute)),
                Is.True);
            Assert.That(
                Attribute.IsDefined(
                    typeof(PathogenDetectorUiState),
                    typeof(NetSerializableAttribute)),
                Is.True);
            Assert.That(
                Attribute.IsDefined(
                    typeof(PathogenContaminationBeaconGroup),
                    typeof(NetSerializableAttribute)),
                Is.True);
        });
    }
}
