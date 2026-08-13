using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Virology;
using Content.Shared._Starlight.Medical.Virology;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

[TestFixture]
public sealed class PathogenHostEligibilityTests : GameTest
{
    private const string TestArchetype = "SpaceCold";

    [Test]
    [TestCase("MobMonkey")]
    [TestCase("MobCow")]
    [TestCase("MobMouse")]
    [TestCase("MobBee")]
    public async Task AnimalsCannotHostPathogens(string prototype)
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var registry = server.System<PathogenRegistrySystem>();
        var pathogens = server.System<PathogenSystem>();

        await server.WaitAssertion(() =>
        {
            var animal = entities.SpawnEntity(prototype, MapCoordinates.Nullspace);
            var strain = registry.Generate(TestArchetype)!;

            Assert.Multiple(() =>
            {
                Assert.That(pathogens.CanHost(animal), Is.False, $"{prototype} must not be a valid host.");
                Assert.That(
                    pathogens.TryInfect(animal, strain.Id, bypassImmunity: true),
                    Is.False,
                    $"{prototype} must resist even a forced infection.");
                Assert.That(entities.HasComponent<PathogenInfectionComponent>(animal), Is.False);
            });
        });
    }

    [Test]
    [TestCase("MobIPC")]
    [TestCase("MobSkeletonPerson")]
    public async Task SyntheticAndUndeadSpeciesAreTotallyImmune(string prototype)
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var registry = server.System<PathogenRegistrySystem>();
        var pathogens = server.System<PathogenSystem>();
        var hosts = server.System<PathogenHostSelectionSystem>();

        await server.WaitAssertion(() =>
        {
            var mob = entities.SpawnEntity(prototype, MapCoordinates.Nullspace);
            var strain = registry.Generate(TestArchetype)!;

            Assert.Multiple(() =>
            {
                Assert.That(
                    entities.GetComponent<PathogenImmunityComponent>(mob).Total,
                    Is.True,
                    $"{prototype} must declare total pathogen immunity.");
                Assert.That(pathogens.CanHost(mob), Is.False);
                Assert.That(hosts.IsEligibleAutomaticHost(mob), Is.False);
                Assert.That(pathogens.TryInfect(mob, strain.Id, bypassImmunity: true), Is.False);
            });
        });
    }

    [Test]
    public async Task CrewCanStillHostPathogens()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var registry = server.System<PathogenRegistrySystem>();
        var pathogens = server.System<PathogenSystem>();

        await server.WaitAssertion(() =>
        {
            var crew = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var strain = registry.Generate(TestArchetype)!;

            Assert.That(pathogens.CanHost(crew), Is.True);
            Assert.That(pathogens.TryInfect(crew, strain.Id), Is.True);
        });
    }
}
