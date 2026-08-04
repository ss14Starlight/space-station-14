using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Virology;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Medical.Virology;
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
