using System.Threading.Tasks;
using Content.Shared.Electrocution;
using Content.Shared.Forensics.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Genetics;

/// <summary>
/// Critical bug verification tests for the genetics code generator.
/// These tests document bugs that must be fixed before the system can work.
/// </summary>
[TestFixture]
public sealed class GeneticsGeneratorBugTest
{
    /// <summary>
    /// CRITICAL BUG: The generator searches for a generic attribute `GeneticComponentAttribute`1`
    /// but the actual attribute is not generic.
    ///
    /// Expected: AttributeName should be "Content.Shared.Genetics.GeneticComponentAttribute"
    /// Actual: AttributeName is "Content.Shared.Genetics.GeneticComponentAttribute`1"
    ///
    /// This causes the generator to never find any components, so no code is generated.
    /// </summary>
    [Test]
    public async Task TestGeneratorFindsGeneticComponents()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // If the generator worked, adding a genetic component should trigger generated handlers
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp = entMan.AddComponent<DnaComponent>(entity);

            // DNA is auto-generated at the correct DnaLength by OnConstructDna.
            // Do NOT overwrite it with a hard-coded string — DnaLength depends on
            // the number of registered genetic components and will grow over time.
            var originalDna = dnaComp.DNA;
            Assert.That(originalDna, Is.Not.Null, "DNA should be auto-generated on init");

            // Add a component that should be recognized by the generator
            // InsulatedComponent is annotated with [GeneticComponent(5,2)]
            entMan.AddComponent<InsulatedComponent>(entity);

            // If the generator found the attribute and generated handlers,
            // the OnComponentInitInsulatedComponent should have fired and updated DNA
            // If the attribute name is wrong, DNA will remain unchanged

            // This assertion will FAIL until the bug is fixed
            Assert.That(dnaComp.DNA, Is.Not.EqualTo(originalDna),
                "DNA should change when genetic component is added. " +
                "If this fails, the generator likely didn't find the GeneticComponent attribute. " +
                "Check that AttributeName in GeneticsSystemGenerator matches the actual attribute name.");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verify that InitializeGenerated() is actually being called and has content.
    /// If the generator doesn't find any components, InitializeGenerated() will be empty.
    /// </summary>
    [Test]
    public async Task TestGeneratorProducedSubscriptions()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Check if generated file exists by seeing if event handlers work
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp = entMan.AddComponent<DnaComponent>(entity);

            // DNA is auto-generated at the correct DnaLength by OnConstructDna.
            Assert.That(dnaComp.DNA, Is.Not.Null, "DNA should be auto-generated on init");

            // Track whether DNA changes occur on component operations
            var changeCount = 0;
            var lastDna = dnaComp.DNA;

            // Try multiple genetic components
            entMan.AddComponent<InsulatedComponent>(entity);
            if (dnaComp.DNA != lastDna) changeCount++;
            lastDna = dnaComp.DNA;

            entMan.RemoveComponent<InsulatedComponent>(entity);
            if (dnaComp.DNA != lastDna) changeCount++;

            // If the generator worked, we should see DNA changes
            Assert.That(changeCount, Is.GreaterThan(0),
                "No DNA changes detected. Generator may not have produced any subscriptions. " +
                "Expected at least 2 changes (one on add, one on remove). " +
                "This likely means the attribute name is wrong or no components were found.");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }
}
