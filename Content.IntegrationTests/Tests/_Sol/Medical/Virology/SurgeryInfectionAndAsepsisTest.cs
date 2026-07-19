using System.Collections.Generic;
using Content.Server._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.Damage;
using Content.Shared.Station.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Sol.Medical.Virology;

[TestFixture]
[TestOf(typeof(SurgeryInfectionSystem))]
public sealed class SurgeryInfectionAndAsepsisTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: SolSurgeryTestMob
  parent: MobHuman

- type: entity
  id: SolSurgeryTestStation
  categories: [ HideSpawnMenu ]
  components:
  - type: StationData
  - type: VirologyStation

- type: entity
  id: SolSurgeryTestTool
  parent: Scalpel
";

    [Test]
    public async Task DirtyToolsIncreaseInfectionChance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var surgery = entMan.System<SurgeryInfectionSystem>();

        await server.WaitAssertion(() =>
        {
            var station = entMan.Spawn("SolSurgeryTestStation");
            var patient = entMan.Spawn("SolSurgeryTestMob");
            var user = entMan.Spawn("SolSurgeryTestMob");
            var tool = entMan.Spawn("SolSurgeryTestTool");

            entMan.EnsureComponent<StationMemberComponent>(patient).Station = station;
            entMan.EnsureComponent<StationMemberComponent>(user).Station = station;

            var clean = surgery.CalculateModifiers(user, patient, new List<EntityUid> { tool }, failed: false);
            Assert.That(clean.StationEnabled, Is.True);

            var sterility = entMan.EnsureComponent<SurgicalToolSterilityComponent>(tool);
            sterility.State = SurgicalSterilityState.Dirty;
            sterility.Contaminants.Add(new PathogenContaminationEntry
            {
                PathogenId = SurgeryInfectionSystem.DefaultSurgeryPathogen,
                Load = 3f,
            });

            var dirty = surgery.CalculateModifiers(user, patient, new List<EntityUid> { tool }, failed: false);
            Assert.That(dirty.ToolMultiplier, Is.GreaterThan(clean.ToolMultiplier));
            Assert.That(dirty.FinalChance, Is.GreaterThan(clean.FinalChance));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SurgeryInfectionNoOpsOffStation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var surgery = entMan.System<SurgeryInfectionSystem>();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.Spawn("SolSurgeryTestMob");
            var user = entMan.Spawn("SolSurgeryTestMob");
            var mods = surgery.CalculateModifiers(user, patient, new List<EntityUid>(), failed: false);
            Assert.That(mods.StationEnabled, Is.False);
            Assert.That(mods.FinalChance, Is.EqualTo(0f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SurgeryInfectionEnabledByStationComponentWithoutVirologyMode()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var surgery = entMan.System<SurgeryInfectionSystem>();

        await server.WaitAssertion(() =>
        {
            // Clear leftover pooled gamemode entities; only the station component should gate surgery risk.
            var leftover = new List<EntityUid>();
            var query = entMan.EntityQueryEnumerator<Content.Server._Sol.Medical.Virology.VirologyModeRuleComponent>();
            while (query.MoveNext(out var uid, out _))
                leftover.Add(uid);
            foreach (var uid in leftover)
                entMan.DeleteEntity(uid);

            Assert.That(entMan.Count<Content.Server._Sol.Medical.Virology.VirologyModeRuleComponent>(), Is.EqualTo(0));

            var station = entMan.Spawn("SolSurgeryTestStation");
            var patient = entMan.Spawn("SolSurgeryTestMob");
            var user = entMan.Spawn("SolSurgeryTestMob");
            entMan.EnsureComponent<StationMemberComponent>(patient).Station = station;

            var mods = surgery.CalculateModifiers(user, patient, new List<EntityUid>(), failed: false);
            Assert.That(mods.StationEnabled, Is.True);
            Assert.That(mods.FinalChance, Is.GreaterThan(0f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WashingClearsToolContamination()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var asepsis = entMan.System<SurgicalAsepsisSystem>();

        await server.WaitAssertion(() =>
        {
            var tool = entMan.Spawn("SolSurgeryTestTool");
            var user = entMan.Spawn("SolSurgeryTestMob");
            var sterility = entMan.EnsureComponent<SurgicalToolSterilityComponent>(tool);
            sterility.State = SurgicalSterilityState.Dirty;
            sterility.Contaminants.Add(new PathogenContaminationEntry { PathogenId = "SolPathogenFlu", Load = 2f });

            Assert.That(asepsis.TryWash((tool, sterility), user, sterilize: true), Is.True);
            Assert.That(sterility.State, Is.EqualTo(SurgicalSterilityState.Sterile));
            Assert.That(sterility.Contaminants, Is.Empty);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AttackingSomeoneDirtiesSterileTools()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var attacker = entMan.Spawn("SolSurgeryTestMob");
            var victim = entMan.Spawn("SolSurgeryTestMob");
            var tool = entMan.Spawn("SolSurgeryTestTool");
            var sterility = entMan.EnsureComponent<SurgicalToolSterilityComponent>(tool);
            sterility.State = SurgicalSterilityState.Sterile;

            var hit = new MeleeHitEvent([victim], attacker, tool, new DamageSpecifier(), null)
            {
                IsHit = true,
            };
            entMan.EventBus.RaiseLocalEvent(tool, hit);

            Assert.That(sterility.State, Is.EqualTo(SurgicalSterilityState.Dirty));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NitrileGlovesAreSterileContaminablePpe()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var asepsis = entMan.System<SurgicalAsepsisSystem>();

        await server.WaitAssertion(() =>
        {
            foreach (var proto in new[] { "ClothingHandsGlovesNitrile", "ClothingHandsGlovesBlackNitrile" })
            {
                var gloves = entMan.Spawn(proto);
                Assert.That(entMan.HasComponent<SurgicalToolSterilityComponent>(gloves), Is.True);
                Assert.That(entMan.HasComponent<SurfaceContaminationComponent>(gloves), Is.True);
                Assert.That(entMan.HasComponent<PathogenResistanceComponent>(gloves), Is.True);

                var sterility = entMan.GetComponent<SurgicalToolSterilityComponent>(gloves);
                Assert.That(sterility.State, Is.EqualTo(SurgicalSterilityState.Sterile));

                sterility.State = SurgicalSterilityState.Dirty;
                sterility.Contaminants.Add(new PathogenContaminationEntry { PathogenId = "SolPathogenFlu", Load = 1f });
                var surface = entMan.GetComponent<SurfaceContaminationComponent>(gloves);
                surface.IsDirty = true;

                var user = entMan.Spawn("SolSurgeryTestMob");
                Assert.That(asepsis.TryWash((gloves, sterility), user, sterilize: false), Is.True);
                Assert.That(asepsis.TryWash((gloves, sterility), user, sterilize: true), Is.True);
                Assert.That(sterility.State, Is.EqualTo(SurgicalSterilityState.Sterile));
                Assert.That(sterility.Contaminants, Is.Empty);
            }
        });

        await pair.CleanReturnAsync();
    }
}
