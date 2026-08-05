using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Virology;
using Content.Server.Storage.EntitySystems;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Standing;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

[TestFixture]
public sealed class PathogenIsolationTests : GameTest
{
    /// <summary>
    /// A container sealed well enough to stop a body decomposing has to hold its pathogens
    /// in too. Without this the correct handling - bag the body, put it in the morgue -
    /// does nothing for disease, while cremating the evidence does.
    /// </summary>
    [Test]
    public async Task BodyBagSealsPathogensIn()
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var map = await Pair.CreateTestMap();
        var isolation = server.System<PathogenIsolationSystem>();
        var pathogens = server.System<PathogenSystem>();
        var registry = server.System<PathogenRegistrySystem>();
        var sources = server.System<PathogenContaminationSourceSystem>();
        var standing = server.System<StandingStateSystem>();
        var storage = server.System<EntityStorageSystem>();
        var oldSporeChance = server.CfgMan.GetCVar(StarlightCCVars.VirologySporePatchChance);

        try
        {
            await server.WaitPost(() =>
            {
                server.CfgMan.SetCVar(StarlightCCVars.VirologySporePatchChance, 1f);

                var viralBag = entities.SpawnEntity("BodyBag", map.GridCoords);
                var viralHost = entities.SpawnEntity("MobHuman", map.GridCoords);
                var fungalBag = entities.SpawnEntity(
                    "BodyBag",
                    map.GridCoords.Offset(new Vector2(10, 0)));
                var fungalHost = entities.SpawnEntity(
                    "MobHuman",
                    map.GridCoords.Offset(new Vector2(10, 0)));

                var virus = registry.Generate("SpaceCold")!;
                var fungus = registry.Generate("SporeBloom")!;

                var baseline = sources.GetViralCarrierContamination();
                Assert.That(pathogens.TryInfect(viralHost, virus.Id), Is.True);
                Assert.That(pathogens.TryInfect(fungalHost, fungus.Id), Is.True);
                Assert.That(
                    sources.GetViralCarrierContamination(),
                    Is.GreaterThan(baseline),
                    "An unbagged viral carrier should still contaminate.");

                Assert.That(standing.Down(viralHost), Is.True);
                Assert.That(standing.Down(fungalHost), Is.True);

                // Body bags spawn closed, and CloseStorage returns early on an already
                // closed container without picking anything up.
                storage.OpenStorage(viralBag);
                storage.OpenStorage(fungalBag);
                storage.CloseStorage(viralBag);
                storage.CloseStorage(fungalBag);

                Assert.That(
                    entities.GetComponent<EntityStorageComponent>(viralBag).Contents.Contains(viralHost),
                    Is.True,
                    "The body bag should have closed around the host.");
                Assert.That(isolation.IsIsolated(viralHost), Is.True);
                Assert.That(isolation.IsIsolated(fungalHost), Is.True);

                Assert.That(
                    sources.GetViralCarrierContamination(),
                    Is.EqualTo(baseline).Within(0.0001f),
                    "A bagged viral carrier must stop contributing contamination.");

                sources.TryCreateSporePatches();
                Assert.That(
                    entities.EntityQuery<PathogenSporePatchComponent>()
                        .Any(patch => patch.Strain == fungus.Id),
                    Is.False,
                    "A bagged fungal carrier must not seed spore patches.");
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
