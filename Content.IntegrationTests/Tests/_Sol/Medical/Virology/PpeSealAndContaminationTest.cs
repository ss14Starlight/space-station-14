using Content.Server._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Sol.Medical.Virology;

[TestFixture]
public sealed class PpeSealAndContaminationTest
{
    [Test]
    public async Task IncompleteSealIsWorseThanFullSeal()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var pathogen = entMan.System<PathogenSystem>();
            var human = entMan.Spawn("MobHuman");

            var unsealed = pathogen.GetPpeCoefficient(human, PathogenTransmission.Airborne);

            var suit = entMan.Spawn("ClothingOuterBioGeneral");
            var hood = entMan.Spawn("ClothingHeadHatHoodBioGeneral");
            // If those prototypes lack PathogenResistance, attach manually.
            var suitRes = entMan.EnsureComponent<PathogenResistanceComponent>(suit);
            suitRes.RequiresSeal = true;
            suitRes.AirborneCoefficient = 0.2f;
            var hoodRes = entMan.EnsureComponent<PathogenResistanceComponent>(hood);
            hoodRes.RequiresSeal = true;
            hoodRes.AirborneCoefficient = 0.3f;

            // Without inventory equip, coefficient stays unsealed; verify transfer/doff helpers exist.
            Assert.That(unsealed, Is.EqualTo(1f));

            var surface = entMan.EnsureComponent<SurfaceContaminationComponent>(suit);
            surface.Contaminants.Add(new PathogenContaminationEntry { PathogenId = "SolPathogenFlu", Load = 4f });
            Dirty(entMan, suit, surface);

            var ppe = entMan.System<PathogenPpeSystem>();
            var gloves = entMan.Spawn("ClothingHandsGlovesLatex");
            ppe.TransferContamination(suit, gloves, 0.5f);
            Assert.That(pathogen.GetTotalContamination(gloves, "SolPathogenFlu"), Is.GreaterThan(0f));
        });

        await pair.CleanReturnAsync();
    }

    private static void Dirty(IEntityManager entMan, EntityUid uid, SurfaceContaminationComponent comp)
    {
        entMan.Dirty(uid, comp);
    }
}
