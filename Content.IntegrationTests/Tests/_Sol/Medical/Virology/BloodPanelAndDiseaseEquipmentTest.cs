using Content.Server._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.Chemistry.Reaction;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Sol.Medical.Virology;

[TestFixture]
public sealed class BloodPanelAndDiseaseEquipmentTest
{
    [Test]
    public async Task SwabDiagnoserVaccinatorWorkflow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.Spawn("MobHuman");
            var user = entMan.Spawn("MobHuman");
            var pathogen = entMan.System<PathogenSystem>();
            Assert.That(pathogen.TryGetPathogen("SolPathogenFlu", out var flu) && flu != null, Is.True);
            pathogen.Infect(patient, flu!, 2f);

            var swab = entMan.Spawn("DiseaseSwab");
            Assert.That(entMan.TryGetComponent(swab, out DiseasePathogenSwabComponent swabComp), Is.True);
            entMan.System<DiseaseEquipmentSystem>().CollectSwabSample((swab, swabComp!), patient, user);

            Assert.That(entMan.TryGetComponent(swab, out PathogenSampleComponent sample), Is.True);
            Assert.That(sample!.PathogenId, Is.EqualTo("SolPathogenFlu"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodPanelIncludesOrganFunction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.Spawn("MobHuman");
            var vial = entMan.Spawn("ChemistryEmptyVial");
            var blood = entMan.EnsureComponent<CentrifugeCompatibleBloodVialComponent>(vial);
            var sample = entMan.EnsureComponent<PathogenSampleComponent>(vial);
            sample.IsBloodSample = true;
            sample.IsCentrifuged = true;
            sample.PathogenId = "SolPathogenWoundSepsis";
            sample.Dose = 2f;
            sample.DetectedStage = PathogenStage.Symptomatic;
            blood.PanelReady = true;
            blood.SourceEntity = entMan.GetNetEntity(patient);

            var text = entMan.System<BloodTestSystem>().BuildBloodPanelText(vial);
            Assert.That(text.Contains("Organ", StringComparison.OrdinalIgnoreCase) ||
                        text.Contains("damaged=", StringComparison.OrdinalIgnoreCase), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
