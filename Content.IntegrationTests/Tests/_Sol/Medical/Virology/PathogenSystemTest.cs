using Content.Server._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sol.Medical.Virology;

[TestFixture]
[TestOf(typeof(PathogenSystem))]
public sealed class PathogenSystemTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: SolVirologyTestMob
  parent: MobHuman
  components:
  - type: MindContainer

- type: entity
  id: SolVirologyTestStation
  categories: [ HideSpawnMenu ]
  components:
  - type: StationData
  - type: VirologyStation
";

    [Test]
    public async Task InfectsAndProgressesOnVirologyStation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var pathogen = entMan.System<PathogenSystem>();

        Assert.That(proto.HasIndex<PathogenPrototype>("SolPathogenFlu"), Is.True);

        await server.WaitAssertion(() =>
        {
            var station = entMan.Spawn("SolVirologyTestStation");
            var mob = entMan.Spawn("SolVirologyTestMob");

            // Attach mob to station membership for gating.
            var member = entMan.EnsureComponent<Content.Shared.Station.Components.StationMemberComponent>(mob);
            member.Station = station;

            pathogen.ForcedInfectionRoll = 0f; // always succeed when chance > 0
            Assert.That(pathogen.TryExpose(mob, "SolPathogenFlu", 2f, PathogenTransmission.Contact, force: true), Is.True);
            Assert.That(entMan.HasComponent<PathogenCarrierComponent>(mob), Is.True);

            var infection = pathogen.GetInfection(mob, "SolPathogenFlu");
            Assert.That(infection, Is.Not.Null);
            Assert.That(infection!.Stage, Is.EqualTo(PathogenStage.Incubation));

            pathogen.Cure(mob, "SolPathogenFlu");
            Assert.That(pathogen.GetInfection(mob, "SolPathogenFlu"), Is.Null);
            Assert.That(entMan.HasComponent<ImmunityComponent>(mob), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DiseaseSystemsNoOpWithoutVirologyStation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var pathogen = entMan.System<PathogenSystem>();

        await server.WaitAssertion(() =>
        {
            var mob = entMan.Spawn("SolVirologyTestMob");
            pathogen.ForcedInfectionRoll = 0f;
            Assert.That(pathogen.TryExpose(mob, "SolPathogenFlu", 5f, PathogenTransmission.Contact), Is.False);
            Assert.That(entMan.HasComponent<PathogenCarrierComponent>(mob), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VaccineGrantsImmunity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var pathogen = entMan.System<PathogenSystem>();

        await server.WaitAssertion(() =>
        {
            var station = entMan.Spawn("SolVirologyTestStation");
            var mob = entMan.Spawn("SolVirologyTestMob");
            var member = entMan.EnsureComponent<Content.Shared.Station.Components.StationMemberComponent>(mob);
            member.Station = station;

            var vaccine = entMan.Spawn("Vaccine");
            var vac = entMan.EnsureComponent<PathogenVaccineComponent>(vaccine);
            vac.PathogenId = "SolPathogenFlu";
            vac.VaccineIdentity = "SolPathogenFlu";
            vac.Strength = 0f;
            vac.Duration = TimeSpan.FromMinutes(30);

            Assert.That(pathogen.TryVaccinate(mob, vac), Is.True);
            pathogen.ForcedInfectionRoll = 0f;
            // Full immunity strength 0 should block non-forced exposure.
            Assert.That(pathogen.TryExpose(mob, "SolPathogenFlu", 5f, PathogenTransmission.Contact), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SyntheticBodiesCannotCarryPathogens()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var pathogen = entMan.System<PathogenSystem>();

        await server.WaitAssertion(() =>
        {
            var ipc = entMan.Spawn("MobIPC");
            var borg = entMan.Spawn("BorgChassisMedical");

            pathogen.ForcedInfectionRoll = 0f;
            foreach (var synthetic in new[] { ipc, borg })
            {
                Assert.That(
                    pathogen.TryExpose(
                        synthetic,
                        "SolPathogenFlu",
                        10f,
                        PathogenTransmission.Contact,
                        force: true),
                    Is.False);
                Assert.That(entMan.HasComponent<PathogenCarrierComponent>(synthetic), Is.False);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TreatmentSpecificPathogensAndThreshold()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var pathogen = entMan.System<PathogenSystem>();
        var solutions = entMan.System<SharedSolutionContainerSystem>();

        await server.WaitAssertion(() =>
        {
            var pneumonia = proto.Index<PathogenPrototype>("SolPathogenBacterialPneumonia");
            var enteric = proto.Index<PathogenPrototype>("SolPathogenEntericFever");
            var hemorrhagic = proto.Index<PathogenPrototype>("SolPathogenHemorrhagicVirus");
            var encephalitis = proto.Index<PathogenPrototype>("SolPathogenNeuroviralEncephalitis");

            Assert.Multiple(() =>
            {
                Assert.That(pneumonia.Treatments, Is.EquivalentTo(new[] { "SolCeftriaxone" }));
                Assert.That(enteric.Treatments, Is.EquivalentTo(new[] { "SolCeftriaxone" }));
                Assert.That(hemorrhagic.Treatments, Is.EquivalentTo(new[] { "SolRibavirin" }));
                Assert.That(encephalitis.Treatments, Is.EquivalentTo(new[] { "SolRibavirin" }));
            });

            var station = entMan.Spawn("SolVirologyTestStation");
            var mob = entMan.Spawn("SolVirologyTestMob");
            entMan.EnsureComponent<Content.Shared.Station.Components.StationMemberComponent>(mob).Station = station;

            pathogen.ForcedInfectionRoll = 0f;
            Assert.That(pathogen.TryExpose(mob, "SolPathogenBacterialPneumonia", 3f, PathogenTransmission.Contact, force: true), Is.True);
            var infection = pathogen.GetInfection(mob, "SolPathogenBacterialPneumonia");
            Assert.That(infection, Is.Not.Null);
            var startingDose = infection!.Dose;

            Assert.That(entMan.TryGetComponent(mob, out BloodstreamComponent blood), Is.True);
            Assert.That(solutions.TryGetSolution(mob, blood!.BloodSolutionName, out var bloodSol, out _), Is.True);

            Assert.That(pathogen.TryResolvePathogen("SolPathogenBacterialPneumonia", out var def) && def != null, Is.True);

            // Trace antiviral must not treat ceftriaxone-only pneumonia.
            solutions.TryAddReagent(bloodSol!.Value, "SolAntiviral", FixedPoint2.New(5));
            pathogen.TryApplyTreatments(mob, infection, def!);
            Assert.That(infection.Dose, Is.EqualTo(startingDose));

            // Trace ceftriaxone must not count.
            solutions.TryAddReagent(bloodSol.Value, "SolCeftriaxone", FixedPoint2.New(0.2));
            pathogen.TryApplyTreatments(mob, infection, def);
            Assert.That(infection.Dose, Is.EqualTo(startingDose));

            // Meaningful ceftriaxone reduces once per call.
            solutions.TryAddReagent(bloodSol.Value, "SolCeftriaxone", FixedPoint2.New(1));
            pathogen.TryApplyTreatments(mob, infection, def);
            Assert.That(infection.Dose, Is.EqualTo(startingDose - 0.5f).Within(0.001f));
        });

        await pair.CleanReturnAsync();
    }
}
