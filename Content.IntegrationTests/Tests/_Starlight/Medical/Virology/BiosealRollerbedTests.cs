using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Virology;
using Content.Server.Storage.EntitySystems;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Interaction;
using Content.Shared.Movement.Events;
using Content.Shared.Standing;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

[TestFixture]
public sealed class BiosealRollerbedTests : GameTest
{
    [Test]
    public async Task ClosedBiosealBlocksPathogensAndScanning()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var map = await Pair.CreateTestMap();
        var analyzer = server.System<PathogenAnalyzerSystem>();
        var interaction = server.System<SharedInteractionSystem>();
        var isolation = server.System<PathogenIsolationSystem>();
        var pathogens = server.System<PathogenSystem>();
        var registry = server.System<PathogenRegistrySystem>();
        var sources = server.System<PathogenContaminationSourceSystem>();
        var spread = server.System<PathogenSpreadSystem>();
        var standing = server.System<StandingStateSystem>();
        var storage = server.System<EntityStorageSystem>();
        var transmission = server.System<PathogenTransmissionSystem>();
        var oldSporeChance = server.CfgMan.GetCVar(StarlightCCVars.VirologySporePatchChance);

        EntityUid virusSource = default;
        EntityUid virusTarget = default;
        EntityUid virusBed = default;
        EntityUid bacteriaSource = default;
        EntityUid bacteriaTarget = default;
        EntityUid fungusSource = default;
        Pathogen virus = default!;
        Pathogen bacteria = default!;
        Pathogen fungus = default!;
        Pathogen outsideStrain = default!;

        try
        {
            await server.WaitPost(() =>
            {
                server.CfgMan.SetCVar(StarlightCCVars.VirologySporePatchChance, 1f);
                virusBed = entities.SpawnEntity("BiosealRollerBed", map.GridCoords);
                virusSource = entities.SpawnEntity("MobHuman", map.GridCoords);
                virusTarget = entities.SpawnEntity(
                    "MobHuman",
                    map.GridCoords.Offset(Vector2.UnitX));
                var bacteriaBed = entities.SpawnEntity(
                    "BiosealRollerBed",
                    map.GridCoords.Offset(new Vector2(10, 0)));
                bacteriaSource = entities.SpawnEntity(
                    "MobHuman",
                    map.GridCoords.Offset(new Vector2(10, 0)));
                bacteriaTarget = entities.SpawnEntity(
                    "MobHuman",
                    map.GridCoords.Offset(new Vector2(11, 0)));
                var fungusBed = entities.SpawnEntity(
                    "BiosealRollerBed",
                    map.GridCoords.Offset(new Vector2(20, 0)));
                fungusSource = entities.SpawnEntity(
                    "MobHuman",
                    map.GridCoords.Offset(new Vector2(20, 0)));

                // Keep ambient prevalence caps above one host for each test strain.
                for (var i = 0; i < 6; i++)
                {
                    entities.SpawnEntity(
                        "MobHuman",
                        map.GridCoords.Offset(new Vector2(100 + i, 0)));
                }

                virus = registry.Generate("SpaceCold")!;
                bacteria = registry.Generate("ThroatRot")!;
                fungus = registry.Generate("SporeBloom")!;
                outsideStrain = registry.Generate("SpaceCold")!;
                foreach (var strain in new[] { virus, bacteria, fungus, outsideStrain })
                {
                    strain.Transmissibility = 1f;
                    strain.MaxPrevalence = 1f;
                    strain.SpreadRange = 3f;
                }

                var baselineContamination = sources.GetViralCarrierContamination();
                Assert.That(pathogens.TryInfect(virusSource, virus.Id), Is.True);
                Assert.That(pathogens.TryInfect(bacteriaSource, bacteria.Id), Is.True);
                Assert.That(pathogens.TryInfect(fungusSource, fungus.Id), Is.True);
                Assert.That(
                    sources.GetViralCarrierContamination(),
                    Is.GreaterThan(baselineContamination));

                Assert.That(standing.Down(virusSource), Is.True);
                Assert.That(standing.Down(bacteriaSource), Is.True);
                Assert.That(standing.Down(fungusSource), Is.True);
                storage.CloseStorage(virusBed);
                storage.CloseStorage(bacteriaBed);
                storage.CloseStorage(fungusBed);
                Assert.That(
                    entities.GetComponent<EntityStorageComponent>(virusBed).Contents.Contains(virusSource),
                    Is.True);
                Assert.That(isolation.IsIsolated(virusSource), Is.True);
                Assert.That(isolation.IsIsolated(bacteriaSource), Is.True);
                Assert.That(isolation.IsIsolated(fungusSource), Is.True);
                Assert.That(interaction.CanAccess(virusTarget, virusSource), Is.False);
                Assert.That(analyzer.CanScan(virusSource), Is.False);
                Assert.That(
                    sources.GetViralCarrierContamination(),
                    Is.EqualTo(baselineContamination).Within(0.0001f));

                spread.SweepNow();
                entities.EventBus.RaiseEvent(
                    EventSource.Local,
                    new PathogenContactEvent(bacteriaSource, bacteriaTarget));
                sources.TryCreateSporePatches();

                Assert.That(pathogens.IsInfected(virusTarget, virus.Id), Is.False);
                Assert.That(pathogens.IsInfected(bacteriaTarget, bacteria.Id), Is.False);
                Assert.That(transmission.TryExpose(virusSource, outsideStrain, 1f), Is.False);
                Assert.That(
                    entities.EntityQuery<PathogenSporePatchComponent>()
                        .Any(patch => patch.Strain == fungus.Id),
                    Is.False,
                    "A sealed fungal carrier must not seed spore patches.");
            });

            await server.WaitPost(() =>
            {
                var move = new ContainerRelayMovementEntityEvent(virusSource);
                entities.EventBus.RaiseLocalEvent(virusBed, ref move);
            });
            await server.WaitRunTicks(90);
            await server.WaitAssertion(() =>
            {
                Assert.That(entities.GetComponent<EntityStorageComponent>(virusBed).Open, Is.True);
                Assert.That(isolation.IsIsolated(virusSource), Is.False);
            });
        }
        finally
        {
            await server.WaitPost(() =>
                server.CfgMan.SetCVar(
                    StarlightCCVars.VirologySporePatchChance,
                    oldSporeChance));
        }
    }
}
