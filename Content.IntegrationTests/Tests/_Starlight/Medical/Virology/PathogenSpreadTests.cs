using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Virology;
using Content.Shared._Starlight.Medical.Virology;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

[TestFixture]
public sealed class PathogenSpreadTests : GameTest
{
    [Test]
    public async Task SweepCanInfectHostWithoutInvalidatingSourceQuery()
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
            Assert.That(pathogens.TrySetStage(source, strain.Id, 1, out _), Is.True);

            spread.SweepNow();
        });

        await server.WaitAssertion(() =>
            Assert.That(pathogens.IsInfected(target, strain.Id), Is.True));
    }
}
