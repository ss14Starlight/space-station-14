using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Virology;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

[TestFixture]
public sealed class PathogenSpreadTests : GameTest
{
    [Test]
    public async Task IncubatingVirusCanInfectOnProximitySweep()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var pathogens = server.System<PathogenSystem>();
        var registry = server.System<PathogenRegistrySystem>();
        var spread = server.System<PathogenSpreadSystem>();

        EntityUid target = default;
        Pathogen strain = default!;

        await server.WaitPost(() =>
        {
            maps.CreateMap(out var mapId);
            var source = entities.SpawnEntity(
                "MobHuman",
                new MapCoordinates(Vector2.Zero, mapId));
            target = entities.SpawnEntity(
                "MobHuman",
                new MapCoordinates(Vector2.UnitX, mapId));

            // Ambient strains cap at 15% of living crew. Eight hosts allow the source
            // to infect one nearby target while the remaining crew stays out of range.
            for (var i = 0; i < 6; i++)
            {
                entities.SpawnEntity(
                    "MobHuman",
                    new MapCoordinates(new Vector2(100 + i, 0), mapId));
            }

            strain = registry.Generate("SpaceCold")!;
            strain.Transmissibility = 1f;
            strain.MaxPrevalence = 1f;
            strain.SpreadRange = 3f;

            Assert.That(pathogens.TryInfect(source, strain.Id), Is.True);
            Assert.That(
                entities.GetComponent<PathogenInfectionComponent>(source).Infections.Single().Stage,
                Is.Zero);

            spread.SweepNow();
        });

        await server.WaitAssertion(() =>
            Assert.That(pathogens.IsInfected(target, strain.Id), Is.True));
    }

    /// <summary>
    /// The dead deliberately keep their infections so bodies stay worth swabbing, which
    /// means nothing else stops a corpse being picked as a shedding source. Containment
    /// does not cover for it either: a body bag's fixture sits on ItemMask, so it never
    /// blocks the InRangeUnobstructed raycast that the proximity sweep relies on.
    /// </summary>
    [Test]
    public async Task DeadHostsDoNotShedVirus()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var mobState = server.System<MobStateSystem>();
        var pathogens = server.System<PathogenSystem>();
        var registry = server.System<PathogenRegistrySystem>();
        var spread = server.System<PathogenSpreadSystem>();

        EntityUid livingTarget = default;
        EntityUid deadTarget = default;
        Pathogen livingStrain = default!;
        Pathogen deadStrain = default!;

        await server.WaitPost(() =>
        {
            maps.CreateMap(out var mapId);
            var livingSource = entities.SpawnEntity(
                "MobHuman",
                new MapCoordinates(Vector2.Zero, mapId));
            livingTarget = entities.SpawnEntity(
                "MobHuman",
                new MapCoordinates(Vector2.UnitX, mapId));
            var deadSource = entities.SpawnEntity(
                "MobHuman",
                new MapCoordinates(new Vector2(30, 0), mapId));
            deadTarget = entities.SpawnEntity(
                "MobHuman",
                new MapCoordinates(new Vector2(31, 0), mapId));

            // Two infected hosts already sit against the 15% ambient tier budget, and the
            // corpse still counts toward it while no longer counting as living crew.
            // Twenty spare crew keeps the cap clear so the control below can transmit.
            for (var i = 0; i < 20; i++)
            {
                entities.SpawnEntity(
                    "MobHuman",
                    new MapCoordinates(new Vector2(100 + i, 0), mapId));
            }

            livingStrain = registry.Generate("SpaceCold")!;
            deadStrain = registry.Generate("SpaceCold")!;
            foreach (var strain in new[] { livingStrain, deadStrain })
            {
                strain.Transmissibility = 1f;
                strain.MaxPrevalence = 1f;
                strain.SpreadRange = 3f;
            }

            Assert.That(pathogens.TryInfect(livingSource, livingStrain.Id), Is.True);
            Assert.That(pathogens.TryInfect(deadSource, deadStrain.Id), Is.True);

            mobState.ChangeMobState(deadSource, MobState.Dead);
            Assert.That(
                entities.GetComponent<PathogenInfectionComponent>(deadSource).Infections,
                Is.Not.Empty,
                "A corpse should keep its infections so the body is still worth swabbing.");

            spread.SweepNow();
        });

        await server.WaitAssertion(() =>
        {
            // Control: the identical living setup does transmit, so the assertion below
            // fails for the right reason rather than because the harness went inert.
            Assert.That(
                pathogens.IsInfected(livingTarget, livingStrain.Id),
                Is.True,
                "A living carrier should still spread by proximity.");
            Assert.That(
                pathogens.IsInfected(deadTarget, deadStrain.Id),
                Is.False,
                "A corpse is not breathing or coughing and must not shed.");
        });
    }

    /// <summary>
    /// Corpses keep their infections, and living crew is the cap denominator. If corpses
    /// also filled the numerator, every death would move the cap twice in the same
    /// direction with nothing to undo it, and a tier could lock out permanently while
    /// healthy crew stood around with no living carrier left anywhere.
    /// </summary>
    [Test]
    public async Task DeadHostsDoNotHoldPrevalenceBudget()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var mobState = server.System<MobStateSystem>();
        var pathogens = server.System<PathogenSystem>();
        var registry = server.System<PathogenRegistrySystem>();
        var transmission = server.System<PathogenTransmissionSystem>();

        var carriers = new List<EntityUid>();
        Pathogen strain = default!;

        await server.WaitPost(() =>
        {
            maps.CreateMap(out var mapId);

            var crew = new List<EntityUid>();
            for (var i = 0; i < 10; i++)
            {
                crew.Add(entities.SpawnEntity(
                    "MobHuman",
                    new MapCoordinates(new Vector2(i * 10, 0), mapId)));
            }

            strain = registry.Generate("SpaceCold")!;

            for (var i = 0; i < 3; i++)
            {
                Assert.That(pathogens.TryInfect(crew[i], strain.Id), Is.True);
                carriers.Add(crew[i]);
            }

            Assert.That(
                transmission.AtCap(strain),
                Is.True,
                "Three ambient carriers among ten crew should exceed the 15% tier budget.");
        });

        await server.WaitPost(() =>
        {
            foreach (var carrier in carriers)
            {
                mobState.ChangeMobState(carrier, MobState.Dead);
            }
        });

        // Counts are cached per tick and killing a mob does not invalidate them, so the
        // assertion below would otherwise read stale numbers and pass for the wrong reason.
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            foreach (var carrier in carriers)
            {
                Assert.That(
                    entities.GetComponent<PathogenInfectionComponent>(carrier).Infections,
                    Is.Not.Empty,
                    "Corpses must keep their infections so bodies stay worth swabbing.");
            }

            Assert.That(
                transmission.AtCap(strain),
                Is.False,
                "Corpses must not hold prevalence budget once they can no longer spread.");
        });
    }

    [Test]
    public async Task IncubatingBacteriaCanInfectOnContact()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var pathogens = server.System<PathogenSystem>();
        var registry = server.System<PathogenRegistrySystem>();

        EntityUid target = default;
        Pathogen strain = default!;

        await server.WaitPost(() =>
        {
            var source = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            target = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);

            for (var i = 0; i < 6; i++)
                entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);

            strain = registry.Generate("ThroatRot")!;
            strain.Transmissibility = 1f;
            strain.MaxPrevalence = 1f;

            Assert.That(pathogens.TryInfect(source, strain.Id), Is.True);
            Assert.That(
                entities.GetComponent<PathogenInfectionComponent>(source).Infections.Single().Stage,
                Is.Zero);

            entities.EventBus.RaiseEvent(
                EventSource.Local,
                new PathogenContactEvent(source, target));
        });

        await server.WaitAssertion(() =>
            Assert.That(pathogens.IsInfected(target, strain.Id), Is.True));
    }

    [Test]
    public async Task IncubatingFungusCanCreatePinnedSporePatch()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var mapManager = server.ResolveDependency<IMapManager>();
        var maps = server.System<SharedMapSystem>();
        var pathogens = server.System<PathogenSystem>();
        var registry = server.System<PathogenRegistrySystem>();
        var sources = server.System<PathogenContaminationSourceSystem>();
        var oldChance = server.CfgMan.GetCVar(StarlightCCVars.VirologySporePatchChancePerSample);

        Pathogen strain = default!;

        try
        {
            await server.WaitPost(() =>
            {
                server.CfgMan.SetCVar(StarlightCCVars.VirologySporePatchChancePerSample, 1f);
                maps.CreateMap(out var mapId);
                var grid = mapManager.CreateGridEntity(mapId);
                maps.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(1));
                var source = entities.SpawnEntity(
                    "MobHuman",
                    new EntityCoordinates(grid.Owner, Vector2.Zero));

                strain = registry.Generate("SporeBloom")!;
                Assert.That(pathogens.TryInfect(source, strain.Id), Is.True);
                Assert.That(
                    entities.GetComponent<PathogenInfectionComponent>(source).Infections.Single().Stage,
                    Is.Zero);
                Assert.That(entities.GetComponent<TransformComponent>(source).GridUid, Is.EqualTo(grid.Owner));
                Assert.That(
                    server.CfgMan.GetCVar(StarlightCCVars.VirologySporePatchChancePerSample),
                    Is.EqualTo(1f));

                sources.TryCreateSporePatches();
                Assert.That(
                    entities.GetComponent<PathogenInfectionComponent>(source)
                        .Infections.Single().SporePatchCreated,
                    Is.True);
            });

            await server.WaitAssertion(() =>
                Assert.That(
                    entities.EntityQuery<PathogenSporePatchComponent>()
                        .Any(patch => patch.Strain == strain.Id),
                    Is.True));
        }
        finally
        {
            await server.WaitPost(() =>
                server.CfgMan.SetCVar(
                    StarlightCCVars.VirologySporePatchChancePerSample,
                    oldChance));
        }
    }

    [Test]
    public async Task IncubatingVirusContributesCarrierContamination()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var mapManager = server.ResolveDependency<IMapManager>();
        var maps = server.System<SharedMapSystem>();
        var pathogens = server.System<PathogenSystem>();
        var registry = server.System<PathogenRegistrySystem>();
        var sources = server.System<PathogenContaminationSourceSystem>();
        var perCarrier = server.CfgMan.GetCVar(StarlightCCVars.VirologyContaminationViralCarrier);

        await server.WaitAssertion(() =>
        {
            maps.CreateMap(out var mapId);
            var grid = mapManager.CreateGridEntity(mapId);
            maps.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(1));
            var carrier = entities.SpawnEntity(
                "MobHuman",
                new EntityCoordinates(grid.Owner, Vector2.Zero));
            var strain = registry.Generate("SpaceCold")!;
            var before = sources.GetViralCarrierContamination();

            Assert.That(pathogens.TryInfect(carrier, strain.Id), Is.True);
            Assert.That(
                entities.GetComponent<PathogenInfectionComponent>(carrier).Infections.Single().Stage,
                Is.Zero);
            Assert.That(
                sources.GetViralCarrierContamination(),
                Is.EqualTo(before + perCarrier).Within(0.0001f));
        });
    }
}
