using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Virology;
using Content.Server.Medical.SuitSensors;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Inventory;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

[TestFixture]
public sealed class PathogenDiagnosisTests : GameTest
{
    [Test]
    public async Task HigherTierDisplacesAndLowerTierBounces()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var registry = server.System<PathogenRegistrySystem>();
        var pathogens = server.System<PathogenSystem>();

        Pathogen ambient = default!;
        Pathogen emergent = default!;
        Pathogen otherAmbient = default!;
        EntityUid firstHost = default;
        EntityUid secondHost = default;

        await server.WaitPost(() =>
        {
            ambient = registry.Generate("SpaceCold")!;
            emergent = registry.Generate("StationFever")!;
            otherAmbient = registry.Generate("ThroatRot")!;
            firstHost = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            secondHost = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(pathogens.TryInfect(firstHost, ambient.Id), Is.True);
            Assert.That(pathogens.TryInfect(firstHost, emergent.Id), Is.True);

            var infection = entities.GetComponent<PathogenInfectionComponent>(firstHost)
                .Infections
                .Single();
            Assert.Multiple(() =>
            {
                Assert.That(infection.Pathogen, Is.EqualTo(emergent.Id));
                Assert.That(pathogens.IsImmune(firstHost, ambient.Id), Is.True);
                Assert.That(pathogens.TryInfect(firstHost, ambient.Id), Is.False);
            });

            Assert.That(pathogens.TryInfect(secondHost, ambient.Id), Is.True);
            Assert.That(pathogens.TryInfect(secondHost, otherAmbient.Id), Is.False);
            Assert.That(
                entities.GetComponent<PathogenInfectionComponent>(secondHost)
                    .Infections
                    .Single()
                    .Pathogen,
                Is.EqualTo(ambient.Id));
        });
    }

    [Test]
    public async Task DistinctHostsAdvanceReportsAndSourceCompletesImmediately()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var registry = server.System<PathogenRegistrySystem>();
        var sampling = server.System<PathogenSamplingSystem>();

        Pathogen patientStrain = default!;
        Pathogen sourceStrain = default!;
        EntityUid firstHost = default;
        EntityUid secondHost = default;

        await server.WaitPost(() =>
        {
            patientStrain = registry.Generate("SpaceCold")!;
            sourceStrain = registry.Generate("SporeBloom")!;
            firstHost = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            secondHost = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(
                registry.AnalyzeSample(patientStrain.Id, firstHost, sourceSample: false),
                Is.EqualTo(PathogenAnalysisResult.Partial));
            Assert.That(
                registry.CanAnalyzeSample(patientStrain.Id, firstHost, sourceSample: false),
                Is.EqualTo(PathogenAnalysisResult.DuplicateHost));
            Assert.That(
                registry.AnalyzeSample(patientStrain.Id, firstHost, sourceSample: false),
                Is.EqualTo(PathogenAnalysisResult.DuplicateHost));
            Assert.That(patientStrain.SampledHosts, Has.Count.EqualTo(1));

            var partialReport = sampling.BuildReport(patientStrain);
            Assert.Multiple(() =>
            {
                Assert.That(patientStrain.Identification, Is.EqualTo(PathogenIdentificationStage.Partial));
                Assert.That(partialReport, Does.Contain(patientStrain.Designation));
                Assert.That(partialReport, Does.Contain("VIRAL"));
                Assert.That(partialReport, Does.Contain("INSUFFICIENT DATA"));
                Assert.That(partialReport, Does.Contain("second host"));
            });

            Assert.That(
                registry.AnalyzeSample(patientStrain.Id, secondHost, sourceSample: false),
                Is.EqualTo(PathogenAnalysisResult.Completed));

            var completeReport = sampling.BuildReport(patientStrain);
            Assert.Multiple(() =>
            {
                Assert.That(patientStrain.Identification, Is.EqualTo(PathogenIdentificationStage.Complete));
                Assert.That(patientStrain.SampledHosts, Has.Count.EqualTo(2));
                Assert.That(completeReport, Does.Not.Contain("INSUFFICIENT DATA"));
                Assert.That(completeReport, Does.Contain("NATURAL"));
                Assert.That(completeReport, Does.Contain("Transmissibility"));
            });

            Assert.That(
                registry.AnalyzeSample(sourceStrain.Id, null, sourceSample: true),
                Is.EqualTo(PathogenAnalysisResult.Completed));
            Assert.That(sourceStrain.Identification, Is.EqualTo(PathogenIdentificationStage.Complete));
            Assert.That(sourceStrain.SampledHosts, Is.Empty);
        });
    }

    [Test]
    public async Task DetectorHonorsSensorModeAndNeverCarriesCoordinates()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var registry = server.System<PathogenRegistrySystem>();
        var pathogens = server.System<PathogenSystem>();
        var detector = server.System<PathogenDetectorSystem>();
        var inventory = server.System<InventorySystem>();
        var sensors = server.System<SuitSensorSystem>();

        Pathogen strain = default!;
        EntityUid observer = default;
        EntityUid host = default;
        EntityUid uniform = default;

        await server.WaitPost(() =>
        {
            strain = registry.Generate("SpaceCold")!;
            observer = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            host = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            uniform = entities.SpawnEntity("ClothingUniformJumpsuitColorGrey", MapCoordinates.Nullspace);
            pathogens.TryInfect(host, strain.Id);

            Assert.That(inventory.TryEquip(host, uniform, "jumpsuit"), Is.True);
            var sensor = entities.GetComponent<SuitSensorComponent>(uniform);
            sensors.SetSensor((uniform, sensor), SuitSensorMode.SensorOff);
        });

        await server.WaitAssertion(() =>
            Assert.That(detector.BuildState(observer).Infections, Is.Empty));

        await server.WaitPost(() =>
        {
            var sensor = entities.GetComponent<SuitSensorComponent>(uniform);
            sensors.SetSensor((uniform, sensor), SuitSensorMode.SensorBinary);
        });
        await server.WaitAssertion(() =>
            Assert.That(detector.BuildState(observer).Infections, Is.Empty));

        await server.WaitPost(() =>
        {
            var sensor = entities.GetComponent<SuitSensorComponent>(uniform);
            sensors.SetSensor((uniform, sensor), SuitSensorMode.SensorVitals);
        });
        await server.WaitAssertion(() =>
        {
            var unknown = detector.BuildState(observer).Infections.Single();
            Assert.That(unknown.Detection, Does.Contain("Unidentified pathogen"));
        });

        await server.WaitPost(() =>
            registry.AnalyzeSample(strain.Id, host, sourceSample: false));
        await server.WaitAssertion(() =>
        {
            var identified = detector.BuildState(observer).Infections.Single();
            Assert.That(identified.Detection, Does.Contain(strain.Designation));

            var stateFields = typeof(PathogenDetectorUiState)
                .GetFields()
                .Select(field => field.FieldType)
                .ToArray();
            Assert.That(
                stateFields.Any(type =>
                    type.Name.Contains("Coordinate", StringComparison.OrdinalIgnoreCase)),
                Is.False);
        });
    }

    [Test]
    public async Task DiagnosisPrototypesUseExistingMachinesAndBatchCentrifuge()
    {
        var server = Pair.Server;
        var entities = server.EntMan;

        EntityUid detector = default;
        EntityUid swab = default;
        EntityUid diagnoser = default;
        EntityUid centrifuge = default;
        EntityUid culture = default;

        await server.WaitPost(() =>
        {
            detector = entities.SpawnEntity("PathogenDetector", MapCoordinates.Nullspace);
            swab = entities.SpawnEntity("PathogenSwab", MapCoordinates.Nullspace);
            diagnoser = entities.SpawnEntity("DiseaseDiagnoser", MapCoordinates.Nullspace);
            centrifuge = entities.SpawnEntity("MachineCentrifuge", MapCoordinates.Nullspace);
            culture = entities.SpawnEntity("PathogenViableCulture", MapCoordinates.Nullspace);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<PathogenDetectorComponent>(detector), Is.True);
                Assert.That(entities.HasComponent<PathogenSwabComponent>(swab), Is.True);
                Assert.That(entities.HasComponent<PathogenDiagnoserComponent>(diagnoser), Is.True);
                Assert.That(entities.HasComponent<PathogenViableCultureComponent>(culture), Is.True);
            });

            var batch = entities.GetComponent<PathogenCentrifugeComponent>(centrifuge);
            Assert.Multiple(() =>
            {
                Assert.That(batch.Capacity, Is.EqualTo(6));
                Assert.That(batch.ProcessTime, Is.EqualTo(TimeSpan.FromSeconds(10)));
                Assert.That(batch.Container, Is.Not.Null);
            });
        });
    }
}
