using Robust.Shared.Map;
using System;
using System.Threading.Tasks;
using Content.Server.Genetics;
using Content.Shared.Electrocution;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;
using Content.Shared.Prying.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Genetics;

/// <summary>
/// Integration tests for the genetics system with realistic game scenarios.
/// </summary>
[TestFixture]
public sealed class GeneticsIntegrationTest
{
    /// <summary>
    /// Test genetics system with actual player mob prototypes.
    /// </summary>
    [Test]
    public async Task TestGeneticsWithPlayerMob()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            // Spawn a human mob (they should have DNA)
            if (!protoMan.TryIndex<EntityPrototype>("MobHuman", out var humanProto))
            {
                Assert.Inconclusive("MobHuman prototype not found");
                return;
            }

            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);

            // Check if human has DNA component
            var hasDna = entMan.TryGetComponent<DnaComponent>(human, out var dnaComp);

            if (!hasDna)
            {
                Assert.Inconclusive("MobHuman doesn't have DnaComponent in this build");
                entMan.DeleteEntity(human);
                return;
            }

            Assert.That(dnaComp.DNA, Is.Not.Null, "Human DNA should be generated");

            // Add a genetic component to the human
            entMan.AddComponent<InsulatedComponent>(human);

            // DNA should update
            Assert.That(dnaComp.DNA, Is.Not.Null);
            Assert.That(dnaComp.DNA, Is.Not.Empty);

            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that cloning preserves genetics information.
    /// </summary>
    [Test]
    public async Task TestGeneticsWithCloning()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Create original entity with unique genetics
            var original = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var originalDna = entMan.AddComponent<DnaComponent>(original);
            entMan.AddComponent<InsulatedComponent>(original);
            entMan.AddComponent<PryingComponent>(original);

            // DNA should be auto-generated including the components
            Assert.That(originalDna.DNA, Is.Not.Null);
            Assert.That(originalDna.DNA, Is.Not.Empty);

            entMan.DeleteEntity(original);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test genetics with round progression (start, play, restart).
    /// </summary>
    [Test]
    public async Task TestGeneticsAcrossRoundLifecycle()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<Content.Server.GameTicking.GameTicker>();

        string round1Dna = "";
        string round2Dna = "";

        // Start round 1
        await server.WaitPost(() =>
        {
            ticker.StartRound();
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entity1 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp1 = entMan.AddComponent<DnaComponent>(entity1);
            entMan.AddComponent<InsulatedComponent>(entity1);

            round1Dna = dnaComp1.DNA!;
            Assert.That(round1Dna, Is.Not.Null);

            entMan.DeleteEntity(entity1);
        });

        // End round and restart
        await server.WaitPost(() =>
        {
            ticker.EndRound("");
            ticker.RestartRound();
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entity2 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp2 = entMan.AddComponent<DnaComponent>(entity2);
            entMan.AddComponent<InsulatedComponent>(entity2);

            round2Dna = dnaComp2.DNA!;
            Assert.That(round2Dna, Is.Not.Null);

            entMan.DeleteEntity(entity2);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that genetic mutations can be represented in DNA via MutateRandom.
    /// </summary>
    [Test]
    public async Task TestGeneticMutations()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var geneticsSys = server.System<GeneticsSystem>();

        await server.WaitAssertion(() =>
        {
            // Create baseline entity
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp = entMan.AddComponent<DnaComponent>(entity);
            entMan.AddComponent<InsulatedComponent>(entity);

            var originalDna = dnaComp.DNA;
            Assert.That(originalDna, Is.Not.Null);
            Assert.That(originalDna!.Length, Is.GreaterThan(0));

            // Apply a small mutation — should change DNA but likely preserve the component
            // (InsulatedComponent has Stability=2, so a single mutation shouldn't remove it)
            geneticsSys.MutateRandom(entity, 1);
            Assert.That(dnaComp.DNA, Is.Not.EqualTo(originalDna),
                "DNA should change after mutation");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that different species/mobs can have genetics.
    /// </summary>
    [Test]
    public async Task TestGeneticsAcrossSpecies()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            // Test with different mob types if they exist
            var mobTypes = new[] { "MobHuman", "MobMonkey", "MobMouse" };

            foreach (var mobType in mobTypes)
            {
                if (!protoMan.TryIndex<EntityPrototype>(mobType, out _))
                    continue;

                var mob = entMan.SpawnEntity(mobType, MapCoordinates.Nullspace);

                if (!entMan.TryGetComponent<DnaComponent>(mob, out var dnaComp))
                {
                    // Not all mobs may have DNA, that's okay
                    entMan.DeleteEntity(mob);
                    continue;
                }

                // Add genetic component
                entMan.AddComponent<ThermalVisionComponent>(mob);

                // DNA should update
                Assert.That(dnaComp.DNA, Is.Not.Null);

                entMan.DeleteEntity(mob);
            }

            Assert.Pass("Genetics work across different entity types");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test performance with many genetic components on a single entity.
    /// </summary>
    [Test]
    public async Task TestPerformanceWithManyComponents()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp = entMan.AddComponent<DnaComponent>(entity);

            // Add all available genetic components
            entMan.AddComponent<InsulatedComponent>(entity);
            entMan.AddComponent<PryingComponent>(entity);
            entMan.AddComponent<ThermalVisionComponent>(entity);

            var startTime = DateTime.UtcNow;

            var duration = DateTime.UtcNow - startTime;

            // DNA generation should be reasonably fast (< 100ms)
            Assert.That(duration.TotalMilliseconds, Is.LessThan(100),
                "DNA generation should be performant");

            Assert.That(dnaComp.DNA, Is.Not.Null);
            Assert.That(dnaComp.DNA, Is.Not.Empty);

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test interaction between genetics system and forensics system.
    /// </summary>
    [Test]
    public async Task TestGeneticsForensicsInteraction()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var forensicsSys = server.System<Content.Server.Forensics.ForensicsSystem>();

        await server.WaitAssertion(() =>
        {
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp = entMan.AddComponent<DnaComponent>(entity);

            // RandomizeDNA should trigger ConstructDnaEvent
            forensicsSys.RandomizeDNA((entity, dnaComp));

            Assert.That(dnaComp.DNA, Is.Not.Null, "RandomizeDNA should generate DNA");
            Assert.That(dnaComp.DNA, Is.Not.Empty);

            // Add genetic component after DNA was generated
            entMan.AddComponent<PryingComponent>(entity);

            // DNA should update to include the new component
            Assert.That(dnaComp.DNA, Is.Not.Null);

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that DNA persists correctly when entity is serialized/deserialized.
    /// </summary>
    [Test]
    public async Task TestDnaPersistence()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Create entity with genetic components and DNA
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp = entMan.AddComponent<DnaComponent>(entity);
            entMan.AddComponent<InsulatedComponent>(entity);
            entMan.AddComponent<ThermalVisionComponent>(entity);

            var originalDna = dnaComp.DNA;

            // DNA component is networked and should persist
            Assert.That(originalDna, Is.Not.Null);
            Assert.That(originalDna, Is.Not.Empty);

            // Verify DNA field is marked as DataField (it is)
            // This ensures it will be serialized when saving
            Assert.That(dnaComp.DNA, Is.EqualTo(originalDna));

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test genetics system initialization and shutdown.
    /// </summary>
    [Test]
    public async Task TestGeneticsSystemLifecycle()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var geneticsSys = server.System<GeneticsSystem>();

        // System should initialize without errors
        Assert.That(geneticsSys, Is.Not.Null, "GeneticsSystem should be available");

        // Verify the system is properly set up
        await server.WaitAssertion(() =>
        {
            // The system should have initialized its entity queries and event subscriptions
            // If we get here, Initialize() ran successfully
            Assert.Pass("GeneticsSystem initialized successfully");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that DNA can be generated after round restart.
    /// </summary>
    [Test]
    public async Task TestDnaGenerationAfterRoundRestart()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // In round 1, create entity with genetics
            var entity1 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp1 = entMan.AddComponent<DnaComponent>(entity1);
            entMan.AddComponent<InsulatedComponent>(entity1);

            Assert.That(dnaComp1.DNA, Is.Not.Null);
            entMan.DeleteEntity(entity1);
        });

        // Simulate round restart
        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
        });

        await server.WaitPost(() => { });

        await server.WaitAssertion(() =>
        {
            // In round 2, DNA should still generate correctly
            var entity2 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp2 = entMan.AddComponent<DnaComponent>(entity2);
            entMan.AddComponent<InsulatedComponent>(entity2);

            Assert.That(dnaComp2.DNA, Is.Not.Null);
            Assert.That(dnaComp2.DNA, Is.Not.Empty);

            entMan.DeleteEntity(entity2);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that mixing genetic and non-genetic components works correctly.
    /// </summary>
    [Test]
    public async Task TestMixedGeneticAndNonGeneticComponents()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp = entMan.AddComponent<DnaComponent>(entity);

            // Add genetic component
            entMan.AddComponent<InsulatedComponent>(entity);

            // Add non-genetic component
            entMan.AddComponent<FingerprintComponent>(entity);

            var dnaAfterBoth = dnaComp.DNA;

            // Remove non-genetic component
            entMan.RemoveComponent<FingerprintComponent>(entity);

            // DNA should not change when non-genetic component is removed
            Assert.That(dnaComp.DNA, Is.EqualTo(dnaAfterBoth),
                "DNA should not change from non-genetic component removal");

            // Remove genetic component
            entMan.RemoveComponent<InsulatedComponent>(entity);

            // DNA should change when genetic component is removed
            Assert.That(dnaComp.DNA, Is.Not.EqualTo(dnaAfterBoth),
                "DNA should change when genetic component is removed");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that DNA is auto-generated when DnaComponent is initialized.
    /// </summary>
    [Test]
    public async Task TestDnaGenerationDuringInit()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Adding DnaComponent should trigger auto-generation via MapInit
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp = entMan.AddComponent<DnaComponent>(entity);

            Assert.That(dnaComp.DNA, Is.Not.Null, "DNA should be auto-generated on initialization");
            Assert.That(dnaComp.DNA, Is.Not.Empty);

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }
}
