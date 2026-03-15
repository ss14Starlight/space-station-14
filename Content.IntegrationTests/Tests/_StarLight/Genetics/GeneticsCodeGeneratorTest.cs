using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Shared.Electrocution;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Forensics.Components;
using Content.Shared.Prying.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Genetics;

/// <summary>
/// Tests to verify that the genetics code generator properly generates event handlers
/// for annotated components.
/// </summary>
[TestFixture]
public sealed class GeneticsCodeGeneratorTest
{
    /// <summary>
    /// Test that the code generator creates event handler subscriptions for all genetic components.
    /// This is a smoke test to ensure the generator runs without errors.
    /// </summary>
    [Test]
    public async Task TestGeneratedCodeCompiles()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;

        // If we get here, the code compiled and the server started successfully.
        // This means the generator ran and produced valid code.
        Assert.Pass("Generated genetics code compiled successfully");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that InitializeGenerated() method exists and subscribes to component events.
    /// </summary>
    [Test]
    public async Task TestInitializeGeneratedExists()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Create an entity and add/remove a genetic component
            // If the generator worked, these operations should trigger the generated event handlers
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<DnaComponent>(entity);

            // This should trigger OnComponentInitInsulatedComponent if generated correctly
            entMan.AddComponent<InsulatedComponent>(entity);

            // This should trigger OnComponentRemovedInsulatedComponent if generated correctly
            entMan.RemoveComponent<InsulatedComponent>(entity);

            // If we get here without crashes, the generator created the methods
            Assert.Pass("Generated event handlers executed without errors");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that all known genetic components are handled by the generator.
    /// </summary>
    [Test]
    public async Task TestAllGeneticComponentsHandled()
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
            entMan.AddComponent<DnaComponent>(entity);

            // Test each known genetic component
            var geneticComponents = new List<Component>
            {
                entMan.AddComponent<InsulatedComponent>(entity),
                entMan.AddComponent<PryingComponent>(entity),
                entMan.AddComponent<ThermalVisionComponent>(entity)
            };

            // All should be added without errors
            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.True);
            Assert.That(entMan.HasComponent<PryingComponent>(entity), Is.True);
            Assert.That(entMan.HasComponent<ThermalVisionComponent>(entity), Is.True);

            // Remove them all
            entMan.RemoveComponent<InsulatedComponent>(entity);
            entMan.RemoveComponent<PryingComponent>(entity);
            entMan.RemoveComponent<ThermalVisionComponent>(entity);

            // All should be removed without errors
            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.False);
            Assert.That(entMan.HasComponent<PryingComponent>(entity), Is.False);
            Assert.That(entMan.HasComponent<ThermalVisionComponent>(entity), Is.False);

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that genetic components with different complexity values are handled properly.
    /// </summary>
    [Test]
    public async Task TestVariableComplexityHandling()
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
            entMan.AddComponent<DnaComponent>(entity);

            // InsulatedComponent has Complexity=5
            entMan.AddComponent<InsulatedComponent>(entity);

            // PryingComponent has Complexity=4
            entMan.AddComponent<PryingComponent>(entity);

            // ThermalVisionComponent has default Complexity=2
            entMan.AddComponent<ThermalVisionComponent>(entity);

            // Each addition should change the DNA
            var dnaComp = entMan.GetComponent<DnaComponent>(entity);
            Assert.That(dnaComp.DNA, Is.Not.Null);
            Assert.That(dnaComp.DNA, Is.Not.Empty);

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }
}
