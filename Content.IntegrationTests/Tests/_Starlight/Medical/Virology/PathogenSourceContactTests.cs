using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Virology;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

/// <summary>
/// Bacteria used to reach people through the same proximity sweep as fungus, so a rotting
/// body could infect someone who never touched it. Contact is what makes bacteria a
/// different threat from an airborne one, so the sweep carries fungus only and every
/// bacterial route from the environment goes through physical handling.
/// </summary>
[TestFixture]
public sealed class PathogenSourceContactTests : GameTest
{
    /// <summary>
    /// Forces every source roll to land, so a miss below means the route is closed rather
    /// than that the dice were unkind.
    /// </summary>
    private static void ForceCertainInfection(RobustIntegrationTest.ServerIntegrationInstance server)
    {
        server.CfgMan.SetCVar(StarlightCCVars.VirologyContaminationInfectionThreshold, 0f);
        server.CfgMan.SetCVar(StarlightCCVars.VirologyContaminationInfectionChanceScale, 100f);
    }

    [Test]
    public async Task ProximityCarriesFungusButNotBacteria()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var mobState = server.System<MobStateSystem>();
        var pathogens = server.System<PathogenSystem>();
        var registry = server.System<PathogenRegistrySystem>();
        var sources = server.System<PathogenContaminationSourceSystem>();

        var oldThreshold = server.CfgMan.GetCVar(StarlightCCVars.VirologyContaminationInfectionThreshold);
        var oldScale = server.CfgMan.GetCVar(StarlightCCVars.VirologyContaminationInfectionChanceScale);

        try
        {
            await server.WaitAssertion(() =>
            {
                ForceCertainInfection(server);
                maps.CreateMap(out var mapId);

                var corpse = SpawnRottingCorpse(entities, mobState, new MapCoordinates(Vector2.Zero, mapId));
                var victim = entities.SpawnEntity("MobHuman", new MapCoordinates(Vector2.UnitX, mapId));
                var crew = SpawnCrew(entities, mapId);

                // Environmental sources only amplify strains already circulating, so both
                // need a distant host before the corpse can pass anything on.
                var bacteria = registry.Generate("ThroatRot")!;
                var fungus = registry.Generate("SporeBloom")!;
                Assert.That(pathogens.TryInfect(crew[0], bacteria.Id), Is.True);
                Assert.That(pathogens.TryInfect(crew[1], fungus.Id), Is.True);

                sources.SampleSourcesForTest();

                Assert.That(
                    sources.ActiveSourceCount,
                    Is.GreaterThan(0),
                    "The rotting corpse should register as a contamination source.");

                Assert.That(
                    pathogens.IsInfected(victim, fungus.Id),
                    Is.True,
                    "Spores are airborne, so standing beside a rotting body should still infect.");
                Assert.That(
                    pathogens.IsInfected(victim, bacteria.Id),
                    Is.False,
                    "Bacteria must not travel from a source nobody touched.");
                Assert.That(entities.EntityExists(corpse), Is.True);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.CfgMan.SetCVar(StarlightCCVars.VirologyContaminationInfectionThreshold, oldThreshold);
                server.CfgMan.SetCVar(StarlightCCVars.VirologyContaminationInfectionChanceScale, oldScale);
            });
        }
    }

    [Test]
    public async Task HandlingFilthSpreadsBacteria()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var mobState = server.System<MobStateSystem>();
        var pathogens = server.System<PathogenSystem>();
        var registry = server.System<PathogenRegistrySystem>();
        var sources = server.System<PathogenContaminationSourceSystem>();

        var oldThreshold = server.CfgMan.GetCVar(StarlightCCVars.VirologyContaminationInfectionThreshold);
        var oldScale = server.CfgMan.GetCVar(StarlightCCVars.VirologyContaminationInfectionChanceScale);

        try
        {
            await server.WaitAssertion(() =>
            {
                ForceCertainInfection(server);
                maps.CreateMap(out var mapId);

                var corpse = SpawnRottingCorpse(entities, mobState, new MapCoordinates(Vector2.Zero, mapId));
                var handler = entities.SpawnEntity("MobHuman", new MapCoordinates(Vector2.UnitX, mapId));
                var crew = SpawnCrew(entities, mapId);

                // Only a bacterial strain circulates, so the proximity sweep has nothing
                // to give and the contact route is the only way this can land.
                var bacteria = registry.Generate("ThroatRot")!;
                Assert.That(pathogens.TryInfect(crew[0], bacteria.Id), Is.True);

                sources.SampleSourcesForTest();
                Assert.That(
                    pathogens.IsInfected(handler, bacteria.Id),
                    Is.False,
                    "Standing beside the body must not be enough.");

                entities.EventBus.RaiseEvent(
                    EventSource.Local,
                    new InteractHandEvent(handler, corpse));

                Assert.That(
                    pathogens.IsInfected(handler, bacteria.Id),
                    Is.True,
                    "Touching a rotting body with bare hands must expose the handler.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.CfgMan.SetCVar(StarlightCCVars.VirologyContaminationInfectionThreshold, oldThreshold);
                server.CfgMan.SetCVar(StarlightCCVars.VirologyContaminationInfectionChanceScale, oldScale);
            });
        }
    }

    private static EntityUid SpawnRottingCorpse(
        IEntityManager entities,
        MobStateSystem mobState,
        MapCoordinates coordinates)
    {
        var corpse = entities.SpawnEntity("MobHuman", coordinates);
        mobState.ChangeMobState(corpse, MobState.Dead);
        entities.EnsureComponent<RottingComponent>(corpse);
        return corpse;
    }

    /// <summary>
    /// Prevalence is capped as a fraction of living crew, so a nearly empty map would
    /// block the infection for the wrong reason. These stand well clear of the corpse so
    /// they are never picked as the proximity target.
    /// </summary>
    private static List<EntityUid> SpawnCrew(IEntityManager entities, MapId mapId)
    {
        var crew = new List<EntityUid>();
        for (var i = 0; i < 24; i++)
        {
            crew.Add(entities.SpawnEntity("MobHuman", new MapCoordinates(new Vector2(100 + i, 0), mapId)));
        }

        return crew;
    }
}
