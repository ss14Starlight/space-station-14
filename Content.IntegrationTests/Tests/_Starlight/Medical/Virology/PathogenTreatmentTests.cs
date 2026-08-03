using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server._Starlight.Medical.Virology;
using Content.Shared._Starlight.Medical.Virology;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

[TestFixture]
public sealed class PathogenTreatmentTests : GameTest
{
    [Test]
    public async Task TreatmentUsesDiscreteDosesAndRejectsMixing()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var registry = server.System<PathogenRegistrySystem>();
        var pathogens = server.System<PathogenSystem>();
        var treatment = server.System<PathogenTreatmentSystem>();

        Pathogen strain = default!;
        EntityUid injector = default;
        EntityUid infected = default;
        var recipients = new List<EntityUid>();

        await server.WaitPost(() =>
        {
            strain = registry.Generate("SpaceCold")!;
            injector = entities.SpawnEntity("PathogenInjector", MapCoordinates.Nullspace);
            infected = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            pathogens.TryInfect(infected, strain.Id);

            for (var i = 0; i < 4; i++)
                recipients.Add(entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace));
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(
                treatment.TryConfigureInjector(injector, PathogenInjectorMode.Treatment, strain.Id),
                Is.True);
            Assert.That(treatment.CanLoadInjector(injector), Is.False);
            Assert.That(
                treatment.TryConfigureInjector(injector, PathogenInjectorMode.Treatment, strain.Id),
                Is.False,
                "A configured injector must not accept a second payload.");

            var injectorComp = entities.GetComponent<PathogenInjectorComponent>(injector);
            var metadata = entities.GetComponent<MetaDataComponent>(injector);
            Assert.Multiple(() =>
            {
                Assert.That(injectorComp.Mode, Is.EqualTo(PathogenInjectorMode.Treatment));
                Assert.That(injectorComp.Doses, Is.EqualTo(5));
                Assert.That(injectorComp.MaxDoses, Is.EqualTo(5));
                Assert.That(metadata.EntityName, Does.Contain(strain.Designation));
                Assert.That(metadata.EntityDescription, Does.Contain("5/5"));
            });

            Assert.That(
                treatment.TryAdminister(injector, infected),
                Is.EqualTo(PathogenAdministrationResult.Cured));
            Assert.That(pathogens.IsInfected(infected, strain.Id), Is.False);
            Assert.That(pathogens.IsImmune(infected, strain.Id), Is.True);
            Assert.That(injectorComp.Doses, Is.EqualTo(4));
            Assert.That(metadata.EntityDescription, Does.Contain("4/5"));

            foreach (var recipient in recipients)
            {
                Assert.That(
                    treatment.TryAdminister(injector, recipient),
                    Is.EqualTo(PathogenAdministrationResult.Vaccinated));
            }

            Assert.Multiple(() =>
            {
                Assert.That(injectorComp.Empty, Is.True);
                Assert.That(injectorComp.Doses, Is.Zero);
                Assert.That(treatment.CanLoadInjector(injector), Is.True);
                Assert.That(metadata.EntityName, Does.Contain("empty"));
            });
        });
    }

    [Test]
    public async Task BeneficialAndLivePayloadsUseTheSameInjector()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var registry = server.System<PathogenRegistrySystem>();
        var pathogens = server.System<PathogenSystem>();
        var treatment = server.System<PathogenTreatmentSystem>();

        Pathogen beneficial = default!;
        Pathogen virulent = default!;
        EntityUid beneficialInjector = default;
        EntityUid liveInjector = default;
        EntityUid beneficialTarget = default;
        EntityUid liveTarget = default;

        await server.WaitPost(() =>
        {
            beneficial = registry.Generate("Vigor")!;
            virulent = registry.Generate("StationFever")!;
            virulent.Tier = PathogenTier.Virulent;
            beneficialInjector = entities.SpawnEntity("PathogenInjector", MapCoordinates.Nullspace);
            liveInjector = entities.SpawnEntity("PathogenInjector", MapCoordinates.Nullspace);
            beneficialTarget = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            liveTarget = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(
                treatment.TryConfigureInjector(
                    beneficialInjector,
                    PathogenInjectorMode.BeneficialStrain,
                    beneficial.Id),
                Is.True);
            Assert.That(
                treatment.TryAdminister(beneficialInjector, beneficialTarget),
                Is.EqualTo(PathogenAdministrationResult.BeneficialStrainApplied));
            Assert.That(pathogens.IsInfected(beneficialTarget, beneficial.Id), Is.True);
            Assert.That(
                entities.GetComponent<PathogenInjectorComponent>(beneficialInjector).Empty,
                Is.True);

            Assert.That(
                treatment.TryConfigureInjector(
                    liveInjector,
                    PathogenInjectorMode.LiveVaccine,
                    virulent.Id),
                Is.True);
            Assert.That(
                treatment.TryAdminister(liveInjector, liveTarget),
                Is.EqualTo(PathogenAdministrationResult.LiveVaccineApplied));
            Assert.Multiple(() =>
            {
                Assert.That(pathogens.IsImmune(liveTarget, virulent.Id), Is.True);
                Assert.That(entities.HasComponent<PathogenVaccineCarrierComponent>(liveTarget), Is.True);
                Assert.That(entities.GetComponent<PathogenInjectorComponent>(liveInjector).Empty, Is.True);
            });
        });
    }

    [Test]
    public async Task VaccinatorAndInjectorPrototypesUseDiscretePayloadSlots()
    {
        var server = Pair.Server;
        var entities = server.EntMan;

        EntityUid vaccinator = default;
        EntityUid injector = default;

        await server.WaitPost(() =>
        {
            vaccinator = entities.SpawnEntity("Vaccinator", MapCoordinates.Nullspace);
            injector = entities.SpawnEntity("PathogenInjector", MapCoordinates.Nullspace);
        });

        await server.WaitAssertion(() =>
        {
            var machine = entities.GetComponent<PathogenVaccinatorComponent>(vaccinator);
            var injectorComp = entities.GetComponent<PathogenInjectorComponent>(injector);
            var containers = entities.GetComponent<ContainerManagerComponent>(vaccinator);

            Assert.Multiple(() =>
            {
                Assert.That(injectorComp.Empty, Is.True);
                Assert.That(machine.ProduceTime, Is.EqualTo(TimeSpan.FromSeconds(10)));
                Assert.That(machine.LiveProduceTime, Is.EqualTo(TimeSpan.FromSeconds(10)));
                Assert.That(machine.InjectorContainer, Is.Not.Null);
                Assert.That(
                    containers.Containers.ContainsKey(PathogenVaccinatorComponent.InjectorContainerId),
                    Is.True);
                Assert.That(
                    containers.Containers.ContainsKey("pathogen-vessel"),
                    Is.False);
            });
        });
    }

    [Test]
    public async Task VaccinatorWaitsThenConfiguresAndEjectsInjector()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var containers = server.System<SharedContainerSystem>();
        var power = server.System<PowerReceiverSystem>();
        var registry = server.System<PathogenRegistrySystem>();
        var treatment = server.System<PathogenTreatmentSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        Pathogen strain = default!;
        EntityUid vaccinator = default;
        EntityUid culture = default;
        EntityUid injector = default;
        EntityUid user = default;
        PathogenVaccinatorComponent machine = default!;

        await server.WaitPost(() =>
        {
            strain = registry.Generate("SpaceCold")!;
            vaccinator = entities.SpawnEntity("Vaccinator", MapCoordinates.Nullspace);
            culture = entities.SpawnEntity("PathogenViableCulture", MapCoordinates.Nullspace);
            injector = entities.SpawnEntity("PathogenInjector", MapCoordinates.Nullspace);
            user = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            machine = entities.GetComponent<PathogenVaccinatorComponent>(vaccinator);
            entities.GetComponent<PathogenViableCultureComponent>(culture).Strain = strain.Id;
            power.SetNeedsPower(vaccinator, false);
            entities.GetComponent<ApcPowerReceiverComponent>(vaccinator).Powered = true;

            Assert.That(containers.Insert(culture, machine.CultureContainer!), Is.True);
            Assert.That(containers.Insert(injector, machine.InjectorContainer!), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            var startedAt = timing.CurTime;
            Assert.That(treatment.TryStartProduction((vaccinator, machine), user, live: false), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(machine.Producing, Is.True);
                Assert.That(machine.FinishTime, Is.EqualTo(startedAt + machine.ProduceTime));
                Assert.That(entities.GetComponent<PathogenInjectorComponent>(injector).Empty, Is.True);
                Assert.That(
                    treatment.TryStartProduction((vaccinator, machine), user, live: false),
                    Is.False,
                    "A running vaccinator must not begin a second configuration.");
            });
        });

        await server.WaitPost(() => machine.FinishTime = timing.CurTime);
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var injectorComp = entities.GetComponent<PathogenInjectorComponent>(injector);
            Assert.Multiple(() =>
            {
                Assert.That(machine.Producing, Is.False);
                Assert.That(machine.InjectorContainer!.ContainedEntity, Is.Null);
                Assert.That(injectorComp.Mode, Is.EqualTo(PathogenInjectorMode.Treatment));
                Assert.That(injectorComp.Doses, Is.EqualTo(5));
                Assert.That(entities.GetComponent<TransformComponent>(injector).ParentUid, Is.Not.EqualTo(vaccinator));
            });
        });
    }
}
