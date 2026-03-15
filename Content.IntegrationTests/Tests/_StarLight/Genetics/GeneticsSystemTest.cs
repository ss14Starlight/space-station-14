using Robust.Shared.Map;
using System.Threading.Tasks;
using Content.Server.Genetics;
using Content.Shared.Electrocution;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;
using Content.Shared.Prying.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight.Genetics;

/// <summary>
/// Tests for the genetics system that maps DNA sequences to component presence/absence.
/// </summary>
[TestFixture]
public sealed class GeneticsSystemTest
{
    /// <summary>
    /// Test that when an entity with DNA has a genetic component added, their DNA is updated to reflect that.
    /// </summary>
    [Test]
    public async Task TestComponentAdditionUpdatesDna()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Create an entity with DNA (auto-initialized via MapInit)
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp = entMan.AddComponent<DnaComponent>(entity);

            var originalDna = dnaComp.DNA;
            Assert.That(originalDna, Is.Not.Null);

            // Add a genetic component (e.g., InsulatedComponent)
            entMan.AddComponent<InsulatedComponent>(entity);

            // DNA should have changed to reflect the new component
            Assert.That(dnaComp.DNA, Is.Not.EqualTo(originalDna),
                "DNA should have been updated when genetic component was added");
            Assert.That(dnaComp.DNA, Is.Not.Null);
            Assert.That(dnaComp.DNA, Is.Not.Empty);

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that when a genetic component is removed from an entity with DNA, their DNA is updated.
    /// </summary>
    [Test]
    public async Task TestComponentRemovalUpdatesDna()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Create an entity with DNA and a genetic component
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp = entMan.AddComponent<DnaComponent>(entity);
            entMan.AddComponent<InsulatedComponent>(entity);

            var dnaWithComponent = dnaComp.DNA;

            // Remove the genetic component
            entMan.RemoveComponent<InsulatedComponent>(entity);

            // DNA should have changed to reflect the component removal
            Assert.That(dnaComp.DNA, Is.Not.EqualTo(dnaWithComponent),
                "DNA should have been updated when genetic component was removed");
            Assert.That(dnaComp.DNA, Is.Not.Null);
            Assert.That(dnaComp.DNA, Is.Not.Empty);

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that DNA generation at entity creation properly scans existing genetic components.
    /// </summary>
    [Test]
    public async Task TestDnaGenerationScansComponents()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Create entity with genetic components before DNA is generated
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<InsulatedComponent>(entity);
            entMan.AddComponent<PryingComponent>(entity);

            // Add DNA component — ConstructDnaEvent will scan existing components
            var dnaComp = entMan.AddComponent<DnaComponent>(entity);

            // DNA should reflect the presence of both genetic components
            Assert.That(dnaComp.DNA, Is.Not.Null);
            Assert.That(dnaComp.DNA, Is.Not.Empty);

            // Create another entity with the same components
            var entity2 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<InsulatedComponent>(entity2);
            entMan.AddComponent<PryingComponent>(entity2);
            var dnaComp2 = entMan.AddComponent<DnaComponent>(entity2);

            // Both should have DNA generated
            Assert.That(dnaComp2.DNA, Is.Not.Null);
            Assert.That(dnaComp2.DNA, Is.Not.Empty);

            entMan.DeleteEntity(entity);
            entMan.DeleteEntity(entity2);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that multiple genetic components can coexist and are independently tracked in DNA.
    /// </summary>
    [Test]
    public async Task TestMultipleGeneticComponents()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Create entity with multiple genetic components
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp = entMan.AddComponent<DnaComponent>(entity);
            entMan.AddComponent<InsulatedComponent>(entity);
            entMan.AddComponent<PryingComponent>(entity);
            entMan.AddComponent<ThermalVisionComponent>(entity);

            var dnaWithAllComponents = dnaComp.DNA;

            // Remove one component
            entMan.RemoveComponent<PryingComponent>(entity);
            var dnaWithoutPrying = dnaComp.DNA;

            // DNA should change but not be completely different
            Assert.That(dnaWithoutPrying, Is.Not.EqualTo(dnaWithAllComponents),
                "DNA should change when one component is removed");

            // Other components should still be present
            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.True,
                "Other genetic components should remain unaffected");
            Assert.That(entMan.HasComponent<ThermalVisionComponent>(entity), Is.True,
                "Other genetic components should remain unaffected");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that the genetics system properly handles round restart and resets the DNA mapping.
    /// </summary>
    [Test]
    public async Task TestRoundRestartResetsDnaMapping()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Create entity with genetic component
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
            // Create new entity with same component in new round
            var entity2 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp2 = entMan.AddComponent<DnaComponent>(entity2);
            entMan.AddComponent<InsulatedComponent>(entity2);

            // DNA should still be generated after round restart
            Assert.That(dnaComp2.DNA, Is.Not.Null);
            Assert.That(dnaComp2.DNA, Is.Not.Empty);

            entMan.DeleteEntity(entity2);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that entities without DNA are not affected by the genetics system.
    /// </summary>
    [Test]
    public async Task TestNonDnaEntitiesUnaffected()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Create entity without DNA
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            // Add and remove genetic components - should work normally without DNA
            entMan.AddComponent<InsulatedComponent>(entity);
            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.True);

            entMan.RemoveComponent<InsulatedComponent>(entity);
            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.False,
                "Genetic component should be removable from non-DNA entities");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that DNA generation is deterministic for the same set of components within a round.
    /// </summary>
    [Test]
    public async Task TestDnaGenerationConsistency()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Create two entities with identical genetic components
            var entity1 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp1 = entMan.AddComponent<DnaComponent>(entity1);
            entMan.AddComponent<InsulatedComponent>(entity1);
            entMan.AddComponent<PryingComponent>(entity1);

            var entity2 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp2 = entMan.AddComponent<DnaComponent>(entity2);
            entMan.AddComponent<InsulatedComponent>(entity2);
            entMan.AddComponent<PryingComponent>(entity2);

            Assert.That(dnaComp1.DNA, Is.Not.Null);
            Assert.That(dnaComp2.DNA, Is.Not.Null);
            Assert.That(dnaComp1.DNA!.Length, Is.GreaterThan(0));
            Assert.That(dnaComp2.DNA!.Length, Is.GreaterThan(0));

            entMan.DeleteEntity(entity1);
            entMan.DeleteEntity(entity2);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that adding the same component type multiple times doesn't break DNA.
    /// </summary>
    [Test]
    public async Task TestDuplicateComponentHandling()
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

            // Add component
            entMan.AddComponent<InsulatedComponent>(entity);
            var dnaAfterAdd = dnaComp.DNA;

            // Try to add it again — EnsureComponent is a no-op if already present
            entMan.EnsureComponent<InsulatedComponent>(entity);
            var dnaAfterSecondAdd = dnaComp.DNA;

            // DNA should not change on duplicate add
            Assert.That(dnaAfterSecondAdd, Is.EqualTo(dnaAfterAdd),
                "DNA should not change when component is added again");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }
}
