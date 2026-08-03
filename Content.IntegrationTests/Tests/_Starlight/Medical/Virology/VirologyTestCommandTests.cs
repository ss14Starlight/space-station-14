using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Virology;
using Content.Shared._Starlight.Medical.Virology;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

[TestFixture]
public sealed class VirologyTestCommandTests : GameTest
{
    [Test]
    public async Task AdminHarnessControlsLiveVirologyState()
    {
        var server = Pair.Server;
        var console = server.ResolveDependency<IConsoleHost>();
        var entities = server.EntMan;
        var registry = server.System<PathogenRegistrySystem>();
        var contamination = server.System<PathogenContaminationSystem>();

        await server.WaitPost(() => console.ExecuteCommand("virotest setup"));

        Pathogen virus = default!;
        Pathogen fungus = default!;
        await server.WaitAssertion(() =>
        {
            Assert.That(
                registry.Strains.Values.Select(strain => strain.Archetype.Id),
                Is.SupersetOf(new[]
                {
                    "SpaceCold",
                    "ThroatRot",
                    "SporeBloom",
                    "StationFever",
                    "GreyLung",
                    "RedFlux",
                }));

            virus = registry.Strains.Values
                .Where(strain => strain.Archetype == "SpaceCold")
                .MaxBy(strain => strain.Id)!;
            fungus = registry.Strains.Values
                .Where(strain => strain.Archetype == "SporeBloom")
                .MaxBy(strain => strain.Id)!;
        });

        EntityUid human = default;
        await server.WaitPost(() =>
            human = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace));
        var netHuman = entities.GetNetEntity(human);

        await server.WaitPost(() =>
            console.ExecuteCommand($"virotest kit {netHuman}"));
        await server.WaitAssertion(() =>
        {
            var spawnedPrototypes = entities.EntityQuery<MetaDataComponent>()
                .Select(meta => meta.EntityPrototype?.ID)
                .Where(id => id is not null)
                .ToHashSet();

            Assert.That(
                spawnedPrototypes,
                Is.SupersetOf(new[]
                {
                    "HandheldVirologyMonitor",
                    "PathogenAnalyzer",
                    "PathogenSwab",
                    "ChemistryEmptyVialSmall",
                    "PathogenDecontaminator",
                    "DiseaseDiagnoser",
                    "MachineCentrifuge",
                    "ClothingMaskGas",
                    "ClothingMaskSterile",
                    "ClothingMaskBreathMedical",
                    "ClothingHandsGlovesLatex",
                    "ClothingHandsGlovesNitrile",
                    "ClothingOuterBioGeneral",
                    "ClothingHeadHatHoodBioGeneral",
                    "ClothingOuterHardsuitEngineering",
                }));
        });

        await server.WaitPost(() =>
            console.ExecuteCommand($"virotest infect {netHuman} {virus.Id}"));
        await server.WaitAssertion(() =>
        {
            var infections = entities.GetComponent<PathogenInfectionComponent>(human);
            var infection = infections.Infections.Single();
            Assert.Multiple(() =>
            {
                Assert.That(infection.Pathogen, Is.EqualTo(virus.Id));
                Assert.That(infection.SymptomIntervalOverride, Is.EqualTo(TimeSpan.FromSeconds(3)));
            });
        });

        await server.WaitPost(() =>
            console.ExecuteCommand($"virotest fast {netHuman} {virus.Id} 1"));
        await server.WaitAssertion(() =>
        {
            var infection = entities.GetComponent<PathogenInfectionComponent>(human).Infections.Single();
            Assert.That(infection.SymptomIntervalOverride, Is.EqualTo(TimeSpan.FromSeconds(1)));
        });

        EntityUid fungalHuman = default;
        await server.WaitPost(() =>
            fungalHuman = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace));
        var netFungalHuman = entities.GetNetEntity(fungalHuman);

        await server.WaitPost(() =>
            console.ExecuteCommand($"virotest infect {netFungalHuman} {fungus.Id}"));
        await server.WaitPost(() =>
            console.ExecuteCommand($"virotest stage {netFungalHuman} {fungus.Id} {fungus.MaxStage}"));
        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var infection = entities.GetComponent<PathogenInfectionComponent>(fungalHuman)
                .Infections
                .Single(active => active.Pathogen == fungus.Id);

            Assert.Multiple(() =>
            {
                Assert.That(infection.SymptomTimers, Has.Count.EqualTo(fungus.Symptoms.Count));
                Assert.That(
                    infection.SymptomTimers.Values.Distinct().Count(),
                    Is.EqualTo(fungus.Symptoms.Count));
            });
        });

        await server.WaitPost(() =>
            console.ExecuteCommand($"virotest stage {netHuman} {virus.Id} 1"));
        await server.WaitPost(() =>
            console.ExecuteCommand($"virotest identify {netHuman} {virus.Id}"));
        await server.WaitAssertion(() =>
        {
            var infection = entities.GetComponent<PathogenInfectionComponent>(human)
                .Infections
                .Single(active => active.Pathogen == virus.Id);
            Assert.Multiple(() =>
            {
                Assert.That(infection.Stage, Is.EqualTo(1));
                Assert.That(virus.Identification, Is.EqualTo(PathogenIdentificationStage.Complete));
            });
        });

        await server.WaitPost(() =>
            console.ExecuteCommand("virotest contamination 10 20 30"));
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(contamination.Contamination, Is.EqualTo(60f).Within(0.0001f));
                Assert.That(
                    contamination.GetContamination(PathogenType.Virus),
                    Is.EqualTo(10f).Within(0.0001f));
                Assert.That(
                    contamination.GetContamination(PathogenType.Bacteria),
                    Is.EqualTo(20f).Within(0.0001f));
                Assert.That(
                    contamination.GetContamination(PathogenType.Fungus),
                    Is.EqualTo(30f).Within(0.0001f));
            });
        });

        await server.WaitPost(() =>
            console.ExecuteCommand($"virotest spore {netFungalHuman} {fungus.Id}"));
        await server.WaitAssertion(() =>
        {
            Assert.That(
                entities.EntityQuery<PathogenSporePatchComponent>()
                    .Any(patch => patch.Strain == fungus.Id),
                Is.True);
        });

        await server.WaitPost(() =>
            console.ExecuteCommand($"virotest cure {netHuman} all"));
        await server.WaitPost(() =>
            console.ExecuteCommand($"virotest cure {netFungalHuman} all"));
        await server.WaitAssertion(() =>
        {
            if (entities.TryGetComponent<PathogenInfectionComponent>(human, out var infections))
                Assert.That(infections.Infections, Is.Empty);
            if (entities.TryGetComponent<PathogenInfectionComponent>(fungalHuman, out var fungalInfections))
                Assert.That(fungalInfections.Infections, Is.Empty);
        });
    }
}
