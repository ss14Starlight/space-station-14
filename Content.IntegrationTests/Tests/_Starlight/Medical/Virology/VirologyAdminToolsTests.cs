using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Virology;
using Content.Server.Administration.Managers;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

[TestFixture]
public sealed class VirologyAdminToolsTests : GameTest
{
    private const string TestArchetype = "SpaceCold";

    private async Task<EntityUid> SpawnPlayerHost()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var minds = server.System<SharedMindSystem>();

        EntityUid host = default;
        await server.WaitPost(() =>
        {
            server.System<SharedMapSystem>().CreateMap(out var mapId);
            host = entities.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, mapId));

            var mindId = minds.CreateMind(ServerSession!.UserId);
            minds.TransferTo(mindId, host);
            server.PlayerMan.SetAttachedEntity(ServerSession, host);
        });

        await server.WaitRunTicks(1);
        return host;
    }

    [Test]
    public async Task InfectedRosterSeparatesLivingHostsFromCorpses()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var registry = server.System<PathogenRegistrySystem>();
        var pathogens = server.System<PathogenSystem>();
        var hosts = server.System<PathogenHostSelectionSystem>();
        var mobState = server.System<MobStateSystem>();

        var player = await SpawnPlayerHost();

        Pathogen strain = default!;
        EntityUid corpse = default;
        await server.WaitPost(() =>
        {
            strain = registry.Generate(TestArchetype)!;
            corpse = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);

            Assert.That(pathogens.TryInfect(player, strain.Id), Is.True);
            Assert.That(pathogens.TryInfect(corpse, strain.Id), Is.True);
            mobState.ChangeMobState(corpse, MobState.Dead);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var crew = hosts.CountLivingCrew();
            Assert.That(crew, Is.GreaterThan(0), "The denominator must count the living player.");

            var infections = entities.GetComponent<PathogenInfectionComponent>(corpse).Infections;
            Assert.Multiple(() =>
            {
                Assert.That(
                    infections.Any(infection => infection.Pathogen == strain.Id),
                    Is.True,
                    "A corpse keeps its infection, so the roster has to account for it.");
                Assert.That(
                    entities.GetComponent<MobStateComponent>(corpse).CurrentState,
                    Is.EqualTo(MobState.Dead),
                    "The dead carrier must not be counted as a living host.");
            });
        });
    }

    [Test]
    public async Task CustomCommandInfectsAPlayerHost()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var console = server.ResolveDependency<IConsoleHost>();
        var registry = server.System<PathogenRegistrySystem>();

        var host = await SpawnPlayerHost();

        await server.WaitPost(() =>
            console.ExecuteCommand($"virology custom {TestArchetype} hosts=1"));

        await server.WaitAssertion(() =>
        {
            var strain = registry.Strains.Values
                .Where(candidate => candidate.Archetype == TestArchetype)
                .MaxBy(candidate => candidate.Id);

            Assert.That(strain, Is.Not.Null, "The outbreak must generate a runtime strain.");
            Assert.That(
                entities.GetComponent<PathogenInfectionComponent>(host).Infections
                    .Select(infection => infection.Pathogen),
                Does.Contain(strain!.Id),
                "An admin outbreak must land on an eligible player host.");
        });
    }

    [Test]
    public async Task CustomCommandAppliesTuningOptions()
    {
        var server = Pair.Server;
        var console = server.ResolveDependency<IConsoleHost>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var registry = server.System<PathogenRegistrySystem>();

        await SpawnPlayerHost();

        await server.WaitPost(() =>
            console.ExecuteCommand($"virology custom {TestArchetype} spread=0 cap=0.25 symptoms=0 hosts=1"));

        await server.WaitAssertion(() =>
        {
            var strain = registry.Strains.Values
                .Where(candidate => candidate.Archetype == TestArchetype)
                .MaxBy(candidate => candidate.Id);

            Assert.That(strain, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(
                    strain!.Transmissibility,
                    Is.EqualTo(0f).Within(0.0001f),
                    "A zero spread multiplier must produce a strain that cannot transmit.");
                Assert.That(
                    strain.MaxPrevalence,
                    Is.LessThanOrEqualTo(0.25f),
                    "The prevalence cap must not exceed what the admin asked for.");
                Assert.That(
                    strain.Symptoms
                        .Select(id => prototypes.Index(id))
                        .Count(symptom => symptom.MinStage == 2),
                    Is.EqualTo(0),
                    "symptoms=0 should suppress the random stage-two symptom draw.");
            });
        });
    }

    [Test]
    public async Task InfectAndCureCommandsTargetOneHost()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var console = server.ResolveDependency<IConsoleHost>();
        var registry = server.System<PathogenRegistrySystem>();
        var pathogens = server.System<PathogenSystem>();

        var host = await SpawnPlayerHost();
        var netHost = entities.GetNetEntity(host);

        await server.WaitPost(() =>
            console.ExecuteCommand($"virology custom {TestArchetype} hosts=0"));

        Pathogen strain = default!;
        await server.WaitAssertion(() =>
        {
            strain = registry.Strains.Values
                .Where(candidate => candidate.Archetype == TestArchetype)
                .MaxBy(candidate => candidate.Id)!;
            Assert.That(strain, Is.Not.Null);
            Assert.That(entities.HasComponent<PathogenInfectionComponent>(host), Is.False);
        });

        await server.WaitPost(() =>
            console.ExecuteCommand($"virology infect {netHost} {strain.Id}"));

        await server.WaitAssertion(() =>
            Assert.That(
                entities.GetComponent<PathogenInfectionComponent>(host).Infections
                    .Select(infection => infection.Pathogen),
                Does.Contain(strain.Id)));

        await server.WaitPost(() =>
            console.ExecuteCommand($"virology cure {netHost} {strain.Id}"));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(
                entities.HasComponent<PathogenInfectionComponent>(host),
                Is.False);
            Assert.That(
                pathogens.IsImmune(host, strain.Id),
                Is.True,
                "Admin cure should make the target immune to the cured strain.");
        });

        await server.WaitPost(() =>
            console.ExecuteCommand($"virology infect {netHost} {strain.Id}"));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
            Assert.That(
                entities.HasComponent<PathogenInfectionComponent>(host),
                Is.False,
                "The normal admin infect command must respect strain immunity."));
    }

    [Test]
    public async Task CureAllClearsEveryInfection()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var console = server.ResolveDependency<IConsoleHost>();
        var pathogens = server.System<PathogenSystem>();

        var host = await SpawnPlayerHost();
        var strainId = 0;

        await server.WaitPost(() =>
            console.ExecuteCommand($"virology custom {TestArchetype} hosts=1"));
        await server.WaitAssertion(() =>
        {
            Assert.That(
                entities.HasComponent<PathogenInfectionComponent>(host),
                Is.True,
                "The outbreak has to land before cureall means anything.");
            strainId = entities.GetComponent<PathogenInfectionComponent>(host)
                .Infections
                .Single()
                .Pathogen;
        });

        await server.WaitPost(() => console.ExecuteCommand("virology cureall"));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(
                entities.HasComponent<PathogenInfectionComponent>(host),
                Is.False,
                "The panic button must leave nobody infected.");
            Assert.That(
                pathogens.IsImmune(host, strainId),
                Is.True,
                "The panic button should make carriers immune to the strains it cured.");
        });
    }

    [Test]
    public async Task AdminVerbsCureAndImmunise()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var admins = server.ResolveDependency<IAdminManager>();
        var verbs = server.System<SharedVerbSystem>();
        var registry = server.System<PathogenRegistrySystem>();
        var pathogens = server.System<PathogenSystem>();

        var admin = await SpawnPlayerHost();

        EntityUid patient = default;
        Pathogen strain = default!;
        await server.WaitPost(() =>
        {
            patient = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            admins.PromoteHost(ServerSession!);

            strain = registry.Generate(TestArchetype)!;
            Assert.That(pathogens.TryInfect(patient, strain.Id), Is.True);
        });
        await server.WaitRunTicks(1);

        var cureText = string.Empty;
        var immuneText = string.Empty;

        await server.WaitAssertion(() =>
        {
            cureText = Loc.GetString("verb-virology-cure-text");
            immuneText = Loc.GetString("verb-virology-immune-text");

            var offered = verbs.GetLocalVerbs(patient, admin, typeof(Verb))
                .Where(verb => verb.Category?.Text == Loc.GetString("verb-categories-virology"))
                .Select(verb => verb.Text)
                .ToList();

            Assert.That(
                offered,
                Is.EquivalentTo(new[] { cureText, immuneText }),
                "The Virology category must hold exactly the cure and immunity verbs.");
        });

        await server.WaitPost(() =>
        {
            var cure = verbs.GetLocalVerbs(patient, admin, typeof(Verb))
                .First(verb => verb.Text == cureText);
            cure.Act!();
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(
                entities.HasComponent<PathogenInfectionComponent>(patient),
                Is.False,
                "The cure verb must clear every strain the target carries.");
            Assert.That(
                pathogens.IsImmune(patient, strain.Id),
                Is.True,
                "The cure verb must grant immunity to each strain it clears.");
        });

        await server.WaitPost(() =>
        {
            var immune = verbs.GetLocalVerbs(patient, admin, typeof(Verb))
                .First(verb => verb.Text == immuneText);
            immune.Act!();
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<PathogenImmunityComponent>(patient).Total, Is.True);
            Assert.That(
                pathogens.CanHost(patient),
                Is.False,
                "Total immunity must take the target out of the host pool entirely.");
        });
    }
}
