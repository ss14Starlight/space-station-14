using System.Collections.Generic;
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
    public async Task MonitorHonorsSensorModeAndCarriesCrewNamesOnly()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var registry = server.System<PathogenRegistrySystem>();
        var pathogens = server.System<PathogenSystem>();
        var detector = server.System<PathogenDetectorSystem>();
        var contamination = server.System<PathogenContaminationSystem>();
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
            contamination.SetContamination(new Dictionary<PathogenType, float>
            {
                [PathogenType.Virus] = 4f,
                [PathogenType.Bacteria] = 3f,
                [PathogenType.Fungus] = 2f,
            });

            Assert.That(inventory.TryEquip(host, uniform, "jumpsuit"), Is.True);
            var sensor = entities.GetComponent<SuitSensorComponent>(uniform);
            sensors.SetSensor((uniform, sensor), SuitSensorMode.SensorOff);
        });

        await server.WaitAssertion(() =>
            Assert.That(detector.BuildState(observer).SickCrew, Is.Empty));

        await server.WaitPost(() =>
        {
            var sensor = entities.GetComponent<SuitSensorComponent>(uniform);
            sensors.SetSensor((uniform, sensor), SuitSensorMode.SensorBinary);
        });
        await server.WaitAssertion(() =>
            Assert.That(detector.BuildState(observer).SickCrew, Is.Empty));

        await server.WaitPost(() =>
        {
            var sensor = entities.GetComponent<SuitSensorComponent>(uniform);
            sensors.SetSensor((uniform, sensor), SuitSensorMode.SensorVitals);
        });
        await server.WaitAssertion(() =>
        {
            var state = detector.BuildState(observer);
            Assert.That(state.SickCrew, Has.Count.EqualTo(1));
            Assert.That(state.SickCrew.Single(), Is.Not.Empty);
        });

        await server.WaitPost(() =>
            registry.AnalyzeSample(strain.Id, host, sourceSample: false));
        await server.WaitAssertion(() =>
        {
            var state = detector.BuildState(observer);
            var sickCrewField = typeof(PathogenDetectorUiState).GetField(nameof(state.SickCrew));
            var fieldNames = typeof(PathogenDetectorUiState)
                .GetFields()
                .Select(field => field.Name)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(state.SickCrew, Has.Count.EqualTo(1));
                Assert.That(state.Contamination, Is.EqualTo(9f));
                Assert.That(state.Virus, Is.EqualTo(4f));
                Assert.That(state.Bacteria, Is.EqualTo(3f));
                Assert.That(state.Fungus, Is.EqualTo(2f));
                Assert.That(sickCrewField?.FieldType, Is.EqualTo(typeof(List<string>)));
                Assert.That(fieldNames.Any(name => name.Contains("Detection")), Is.False);
                Assert.That(fieldNames.Any(name => name.Contains("Pathogen")), Is.False);
            });
        });
    }

    [Test]
    public async Task AnalyzerReadsPatientsSourcesCulturesAndEveryInjectorPayload()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var analyzerSystem = server.System<PathogenAnalyzerSystem>();
        var registry = server.System<PathogenRegistrySystem>();
        var pathogens = server.System<PathogenSystem>();
        var treatment = server.System<PathogenTreatmentSystem>();

        Pathogen treatmentStrain = default!;
        Pathogen sourceStrain = default!;
        Pathogen liveStrain = default!;
        Pathogen beneficialStrain = default!;
        EntityUid healthy = default;
        EntityUid patient = default;
        EntityUid source = default;
        EntityUid culture = default;
        EntityUid treatmentInjector = default;
        EntityUid liveInjector = default;
        EntityUid beneficialInjector = default;

        await server.WaitPost(() =>
        {
            treatmentStrain = registry.Generate("SpaceCold")!;
            sourceStrain = registry.Generate("SporeBloom")!;
            liveStrain = registry.Generate("StationFever")!;
            liveStrain.Tier = PathogenTier.Virulent;
            beneficialStrain = registry.Generate("Vigor")!;

            healthy = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            patient = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            source = entities.SpawnEntity("PathogenSporePatch", MapCoordinates.Nullspace);
            culture = entities.SpawnEntity("PathogenViableCulture", MapCoordinates.Nullspace);
            treatmentInjector = entities.SpawnEntity("PathogenInjector", MapCoordinates.Nullspace);
            liveInjector = entities.SpawnEntity("PathogenInjector", MapCoordinates.Nullspace);
            beneficialInjector = entities.SpawnEntity("PathogenInjector", MapCoordinates.Nullspace);

            Assert.That(pathogens.TryInfect(patient, treatmentStrain.Id), Is.True);
            entities.GetComponent<PathogenSporePatchComponent>(source).Strain = sourceStrain.Id;
            entities.GetComponent<PathogenViableCultureComponent>(culture).Strain = treatmentStrain.Id;
            Assert.That(
                treatment.TryConfigureInjector(
                    treatmentInjector,
                    PathogenInjectorMode.Treatment,
                    treatmentStrain.Id),
                Is.True);
            Assert.That(
                treatment.TryConfigureInjector(
                    liveInjector,
                    PathogenInjectorMode.LiveVaccine,
                    liveStrain.Id),
                Is.True);
            Assert.That(
                treatment.TryConfigureInjector(
                    beneficialInjector,
                    PathogenInjectorMode.BeneficialStrain,
                    beneficialStrain.Id),
                Is.True);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(analyzerSystem.CanScan(healthy), Is.True);
            Assert.That(analyzerSystem.BuildState(healthy).Pathogens, Is.Empty);

            var unidentified = analyzerSystem.BuildState(patient).Pathogens.Single();
            Assert.Multiple(() =>
            {
                Assert.That(unidentified.FullyIdentified, Is.False);
                Assert.That(unidentified.Heading, Does.Contain("Unidentified"));
                Assert.That(unidentified.Heading, Does.Not.Contain(treatmentStrain.Designation));
            });
        });

        await server.WaitPost(() =>
        {
            registry.IdentifyFully(treatmentStrain.Id);
            registry.IdentifyFully(sourceStrain.Id);
            registry.IdentifyFully(liveStrain.Id);
            registry.IdentifyFully(beneficialStrain.Id);
        });

        await server.WaitAssertion(() =>
        {
            var patientState = analyzerSystem.BuildState(patient);
            var patientReading = patientState.Pathogens.Single();
            var sourceState = analyzerSystem.BuildState(source);
            var cultureState = analyzerSystem.BuildState(culture);
            var treatmentState = analyzerSystem.BuildState(treatmentInjector);
            var liveState = analyzerSystem.BuildState(liveInjector);
            var beneficialState = analyzerSystem.BuildState(beneficialInjector);

            Assert.Multiple(() =>
            {
                Assert.That(patientState.TargetKind, Is.EqualTo(PathogenAnalyzerTargetKind.Patient));
                Assert.That(patientReading.FullyIdentified, Is.True);
                Assert.That(patientReading.Heading, Is.EqualTo(treatmentStrain.Designation));
                Assert.That(patientReading.Classification, Is.EqualTo("VIRAL"));
                Assert.That(patientReading.Symptoms, Is.Not.Empty);
                Assert.That(patientReading.Context, Does.Contain("stage"));

                Assert.That(sourceState.TargetKind, Is.EqualTo(PathogenAnalyzerTargetKind.ContaminationSource));
                Assert.That(sourceState.Pathogens.Single().Heading, Is.EqualTo(sourceStrain.Designation));
                Assert.That(cultureState.TargetKind, Is.EqualTo(PathogenAnalyzerTargetKind.Culture));
                Assert.That(cultureState.Pathogens.Single().Context, Does.Contain("viable culture"));

                Assert.That(treatmentState.Pathogens.Single().Context, Does.Contain("Treatment payload"));
                Assert.That(treatmentState.Pathogens.Single().Context, Does.Contain("5/5"));
                Assert.That(liveState.Pathogens.Single().Context, Does.Contain("Live-vaccine payload"));
                Assert.That(beneficialState.Pathogens.Single().Context, Does.Contain("Beneficial culture payload"));
            });
        });
    }

    [Test]
    public async Task DiagnosisPrototypesUseExistingMachinesAndBatchCentrifuge()
    {
        var server = Pair.Server;
        var entities = server.EntMan;

        EntityUid detector = default;
        EntityUid analyzer = default;
        EntityUid swab = default;
        EntityUid diagnoser = default;
        EntityUid centrifuge = default;
        EntityUid culture = default;

        await server.WaitPost(() =>
        {
            detector = entities.SpawnEntity("HandheldVirologyMonitor", MapCoordinates.Nullspace);
            analyzer = entities.SpawnEntity("PathogenAnalyzer", MapCoordinates.Nullspace);
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
                Assert.That(entities.HasComponent<PathogenAnalyzerComponent>(analyzer), Is.True);
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
