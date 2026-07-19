using System.Collections.Generic;
using Content.Server.Antag.Components;
using Content.Server._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Construction.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Guidebook;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sol.Medical.Virology;

[TestFixture]
[TestOf(typeof(ClandestineLabSystem))]
[TestOf(typeof(EnvironmentalSamplingSystem))]
[TestOf(typeof(PathogenStrainRegistrySystem))]
[TestOf(typeof(BioterrorSystem))]
public sealed class BioterrorCellLaboratoryTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: SolBioterrorTestMob
  parent: MobHuman
  components:
  - type: MindContainer

- type: entity
  id: SolBioterrorTestStation
  categories: [ HideSpawnMenu ]
  components:
  - type: StationData
  - type: VirologyStation

- type: entity
  id: SolBioterrorTestWall
  categories: [ HideSpawnMenu ]
  components:
  - type: Transform
  - type: EnvironmentalMicrobeSource
    chassisId: SolPathogenWoundSepsis
    baseQuality: 0.9
    remainingSamples: 3
    traitPool:
    - trait: SolTraitContact
      weight: 2
    - trait: SolTraitPersistent
      weight: 1
";

    [Test]
    public async Task HeadAndMemberRolesAndPrototypesResolve()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ResolveDependency<IPrototypeManager>();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            Assert.That(proto.HasIndex<EntityPrototype>("SpawnPointHeadBioterrorist"), Is.True);
            Assert.That(proto.HasIndex<EntityPrototype>("SpawnPointBioterrorist"), Is.True);
            Assert.That(proto.HasIndex<EntityPrototype>("SolClandestineSampleAnalyzerFlatpack"), Is.True);
            Assert.That(proto.HasIndex<EntityPrototype>("SolClandestineCultureIncubatorFlatpack"), Is.True);
            Assert.That(proto.HasIndex<EntityPrototype>("SolClandestinePathogenSynthesizerFlatpack"), Is.True);
            Assert.That(proto.HasIndex<EntityPrototype>("ClothingBackpackDuffelBioterrorLab"), Is.True);
            Assert.That(proto.HasIndex<StartingGearPrototype>("HeadBioterroristGear"), Is.True);
            Assert.That(proto.HasIndex<StartingGearPrototype>("BioterroristGear"), Is.True);
            Assert.That(proto.HasIndex<PathogenTraitPrototype>("SolTraitAirborne"), Is.True);
            Assert.That(proto.HasIndex<EntityPrototype>("VirologyModeRule"), Is.True);
            Assert.That(proto.HasIndex<GuideEntryPrototype>("Bioterrorists"), Is.True);

            Assert.That(proto.TryIndex<EntityPrototype>("SolClandestineSampleAnalyzerFlatpack", out var flatpack), Is.True);
            Assert.That(flatpack!.Components.ContainsKey("Flatpack"), Is.True);

            Assert.That(proto.TryIndex<AntagPrototype>("Bioterrorist", out var memberRole), Is.True);
            Assert.That(memberRole!.Guides, Does.Contain(new ProtoId<GuideEntryPrototype>("Bioterrorists")));
            Assert.That(proto.TryIndex<AntagPrototype>("HeadBioterrorist", out var headRole), Is.True);
            Assert.That(headRole!.Guides, Does.Contain(new ProtoId<GuideEntryPrototype>("Bioterrorists")));

            // The mandatory first slot is always the head. Its fallback checks
            // regular Bioterrorist eligibility, not HeadBioterrorist's two-hour
            // requirement, so a lone regular applicant is promoted.
            var rule = entMan.Spawn("VirologyModeRule");
            var selection = entMan.GetComponent<AntagSelectionComponent>(rule);
            Assert.That(selection.Definitions, Is.Not.Empty);
            var leader = selection.Definitions[0];
            Assert.Multiple(() =>
            {
                Assert.That(leader.Min, Is.EqualTo(1));
                Assert.That(leader.Max, Is.EqualTo(1));
                Assert.That(leader.PrefRoles, Does.Contain(new ProtoId<AntagPrototype>("HeadBioterrorist")));
                Assert.That(leader.FallbackRoles, Does.Contain(new ProtoId<AntagPrototype>("Bioterrorist")));
                Assert.That(leader.StartingGear, Is.EqualTo(new ProtoId<StartingGearPrototype>("HeadBioterroristGear")));
                Assert.That(leader.MindRoles, Does.Contain(new EntProtoId("MindRoleHeadBioterrorist")));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScrapeAnalyzeCultureSynthesizeDeployLoop()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var pathogen = entMan.System<PathogenSystem>();
            var registry = entMan.System<PathogenStrainRegistrySystem>();
            var bioterror = entMan.System<BioterrorSystem>();
            var solutions = entMan.System<SharedSolutionContainerSystem>();

            var station = entMan.Spawn("SolBioterrorTestStation");
            var mob = entMan.Spawn("SolBioterrorTestMob");
            var member = entMan.EnsureComponent<Content.Shared.Station.Components.StationMemberComponent>(mob);
            member.Station = station;

            var wall = entMan.Spawn("SolBioterrorTestWall");
            var scraper = entMan.Spawn("SolEnvironmentalScraper");
            Assert.That(entMan.HasComponent<EnvironmentalScraperComponent>(scraper), Is.True);
            Assert.That(entMan.HasComponent<EnvironmentalMicrobeSourceComponent>(wall), Is.True);

            var synthesizer = entMan.Spawn("SolClandestinePathogenSynthesizer");
            Assert.That(solutions.TryGetSolution(synthesizer, "tank", out var synthTank, out _), Is.True);
            solutions.TryAddReagent(synthTank!.Value, "SolCultureStabilizer", FixedPoint2.New(20));

            var pendingTraits = new List<ProtoId<PathogenTraitPrototype>> { "SolTraitAirborne", "SolTraitCoughShed" };
            Assert.That(registry.TryValidateTraits(pendingTraits, 6, out _), Is.True);

            var def = registry.RegisterStrain("SolPathogenFlu", pendingTraits);
            Assert.That(def.IsRuntimeStrain, Is.True);
            Assert.That(def.Id.StartsWith("SolStrain-"), Is.True);
            Assert.That((def.Transmission & PathogenTransmission.Airborne) != 0, Is.True);
            Assert.That(registry.TryResolve(def.Id, out var resolved) && resolved != null, Is.True);

            pathogen.ForcedInfectionRoll = 0f;
            Assert.That(pathogen.TryExpose(mob, def.Id, 3f, PathogenTransmission.Airborne, force: true), Is.True);
            Assert.That(pathogen.GetInfection(mob, def.Id), Is.Not.Null);

            var ampoule = entMan.Spawn("SolPathogenCultureAmpoule");
            var payload = entMan.EnsureComponent<PathogenPayloadComponent>(ampoule);
            payload.StrainId = def.Id;
            payload.Concentration = 5f;
            payload.Kind = PathogenPayloadKind.Food;

            var food = entMan.Spawn("FoodBreadPlain");
            var memberFood = entMan.EnsureComponent<Content.Shared.Station.Components.StationMemberComponent>(food);
            memberFood.Station = station;
            bioterror.DeployFoodOrSurface((ampoule, payload), food, mob, PathogenPayloadKind.Food);
            Assert.That(payload.Used, Is.True);
            Assert.That(pathogen.GetTotalContamination(food, def.Id), Is.GreaterThan(0f));

            // Trait incompatibility rejection
            Assert.That(registry.TryValidateTraits(
                new List<ProtoId<PathogenTraitPrototype>>
                {
                    "SolTraitAirborne",
                    "SolTraitSterilantResist",
                },
                maxBudget: 6,
                out _), Is.False);

            // No free bioterror culture charges remain on the component.
            var bioterrorComp = entMan.EnsureComponent<BioterroristComponent>(mob);
            Assert.That(bioterrorComp.GetType().GetProperty("CulturesRemaining"), Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FlatpackPointsToClandestineMachines()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var analyzerPack = entMan.Spawn("SolClandestineSampleAnalyzerFlatpack");
            var incubatorPack = entMan.Spawn("SolClandestineCultureIncubatorFlatpack");
            var synthPack = entMan.Spawn("SolClandestinePathogenSynthesizerFlatpack");

            Assert.That(entMan.GetComponent<FlatpackComponent>(analyzerPack).Entity, Is.EqualTo(new EntProtoId("SolClandestineSampleAnalyzer")));
            Assert.That(entMan.GetComponent<FlatpackComponent>(incubatorPack).Entity, Is.EqualTo(new EntProtoId("SolClandestineCultureIncubator")));
            Assert.That(entMan.GetComponent<FlatpackComponent>(synthPack).Entity, Is.EqualTo(new EntProtoId("SolClandestinePathogenSynthesizer")));
        });

        await pair.CleanReturnAsync();
    }
}
