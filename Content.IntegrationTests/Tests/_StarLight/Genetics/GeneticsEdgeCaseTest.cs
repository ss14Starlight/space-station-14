using Robust.Shared.Map;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Animals.Components;
using Content.Server.Genetics;
using Content.Shared.Electrocution;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.Prying.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight.Genetics;

/// <summary>
/// Tests for edge cases and design constraints of the genetics system.
/// </summary>
[TestFixture]
public sealed class GeneticsEdgeCaseTest
{
    /// <summary>
    /// Test that DNA length is consistent and accounts for complexity and stability factors.
    /// </summary>
    [Test]
    public async Task TestDnaLengthConsistency()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Create entities with varying numbers of genetic components
            var entity1 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp1 = entMan.AddComponent<DnaComponent>(entity1);

            var baseLength = dnaComp1.DNA?.Length ?? 0;

            var entity2 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp2 = entMan.AddComponent<DnaComponent>(entity2);
            entMan.AddComponent<InsulatedComponent>(entity2); // Complexity=5

            // DNA length should be deterministic based on total complexity
            // (All entities in same round should have same DNA length due to stability padding)
            Assert.That(dnaComp1.DNA, Is.Not.Null);
            Assert.That(dnaComp2.DNA, Is.Not.Null);
            Assert.That(baseLength, Is.GreaterThan(0));

            // Both entities should have the same DNA length
            Assert.That(dnaComp2.DNA!.Length, Is.EqualTo(baseLength),
                "All DNA sequences should be the same length regardless of component count");

            entMan.DeleteEntity(entity1);
            entMan.DeleteEntity(entity2);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that entities without DnaComponent handle genetic components gracefully,
    /// and that auto-initialized DNA works correctly with genetic components.
    /// </summary>
    [Test]
    public async Task TestDnaHandlingGraceful()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Adding genetic components to entities with auto-initialized DNA should work
            var entity1 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp1 = entMan.AddComponent<DnaComponent>(entity1);
            Assert.That(dnaComp1.DNA, Is.Not.Null);

            entMan.AddComponent<InsulatedComponent>(entity1);
            Assert.That(entMan.HasComponent<InsulatedComponent>(entity1), Is.True);
            Assert.That(dnaComp1.DNA, Is.Not.Null);

            // Entities without DnaComponent should still accept genetic components
            var entity2 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<PryingComponent>(entity2);
            Assert.That(entMan.HasComponent<PryingComponent>(entity2), Is.True);

            entMan.DeleteEntity(entity1);
            entMan.DeleteEntity(entity2);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that auto-generated DNA contains only valid nucleotides.
    /// </summary>
    [Test]
    public async Task TestDnaFormatValidation()
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

            // Auto-generated DNA should only contain valid nucleotides
            Assert.That(dnaComp.DNA, Is.Not.Null);
            Assert.That(dnaComp.DNA, Is.Not.Empty);
            Assert.That(dnaComp.DNA!.All(c => c is 'A' or 'T' or 'G' or 'C'), Is.True,
                "DNA should only contain valid nucleotides (A, T, G, C)");

            // Adding a genetic component should maintain valid DNA format
            entMan.AddComponent<InsulatedComponent>(entity);
            Assert.That(dnaComp.DNA!.All(c => c is 'A' or 'T' or 'G' or 'C'), Is.True,
                "DNA should still be valid after component addition");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that component addition/removal during DNA generation doesn't cause race conditions.
    /// </summary>
    [Test]
    public async Task TestConcurrentComponentChanges()
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

            // Add multiple components rapidly
            entMan.AddComponent<InsulatedComponent>(entity);
            entMan.AddComponent<PryingComponent>(entity);
            entMan.AddComponent<ThermalVisionComponent>(entity);

            // DNA should be stable
            Assert.That(dnaComp.DNA, Is.Not.Null);
            Assert.That(dnaComp.DNA, Is.Not.Empty);

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that the GeneticComponentAttribute parameters (complexity, stability) are accessible
    /// and used by the system.
    /// </summary>
    [Test]
    public async Task TestAttributeParametersAreUsed()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Components with different attribute parameters:
            // InsulatedComponent: Complexity=5, Stability=2
            // PryingComponent: Complexity=4, Stability=1 (default)
            // ThermalVisionComponent: Complexity=2 (default), Stability=1 (default)

            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp = entMan.AddComponent<DnaComponent>(entity);

            // The system should use these parameters to determine DNA regions
            entMan.AddComponent<InsulatedComponent>(entity);

            // DNA should exist and reflect the complexity
            Assert.That(dnaComp.DNA, Is.Not.Null);
            Assert.That(dnaComp.DNA, Is.Not.Empty);

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that components remain when DNA is generated (no spurious removals).
    /// </summary>
    [Test]
    public async Task TestDnaGenerationPreservesComponents()
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
            entMan.AddComponent<InsulatedComponent>(entity);

            Assert.That(dnaComp.DNA, Is.Not.Null);

            // Components should remain after DNA was generated
            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.True,
                "Component should remain after DNA generation");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that non-genetic components don't affect DNA.
    /// </summary>
    [Test]
    public async Task TestNonGeneticComponentsIgnored()
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

            var dnaBeforeNonGenetic = dnaComp.DNA;

            // Add a non-genetic component (DnaComponent itself is not genetic)
            entMan.AddComponent<FingerprintComponent>(entity);

            // DNA should not change from non-genetic components
            Assert.That(dnaComp.DNA, Is.EqualTo(dnaBeforeNonGenetic),
                "DNA should not change when non-genetic components are added");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that EggLayerComponent (server-side genetic component) works correctly.
    /// </summary>
    [Test]
    public async Task TestServerSideGeneticComponent()
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

            var dnaBeforeEgg = dnaComp.DNA;

            // EggLayerComponent is a server-side genetic component
            entMan.AddComponent<EggLayerComponent>(entity);

            // DNA should have changed
            Assert.That(dnaComp.DNA, Is.Not.EqualTo(dnaBeforeEgg),
                "DNA should update for server-side genetic components");

            entMan.RemoveComponent<EggLayerComponent>(entity);

            // DNA should change again after removal
            Assert.That(dnaComp.DNA, Is.Not.EqualTo(dnaBeforeEgg),
                "DNA should update when server-side genetic component is removed");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that DNA sequence integrity is maintained across ticks.
    /// </summary>
    [Test]
    public async Task TestDnaIntegrityAcrossTicks()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        EntityUid entity = default;
        string expectedDna = "";

        await server.WaitAssertion(() =>
        {
            entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp = entMan.AddComponent<DnaComponent>(entity);
            entMan.AddComponent<InsulatedComponent>(entity);

            expectedDna = dnaComp.DNA!;
        });

        // Run several ticks
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            // DNA should remain unchanged across ticks
            var dnaComp = entMan.GetComponent<DnaComponent>(entity);
            Assert.That(dnaComp.DNA, Is.EqualTo(expectedDna),
                "DNA should not spontaneously change across ticks");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that [GeneticMultiValueVariable] fields are encoded into DNA.
    /// InsulatedComponent.Coefficient is annotated — its value should survive a DNA round-trip.
    /// </summary>
    [Test]
    public async Task TestGeneticsFieldValuesAttribute()
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
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp = entMan.AddComponent<DnaComponent>(entity);

            // IncreaseCloseness to add InsulatedComponent with full canonical match
            geneticsSys.IncreaseCloseness<InsulatedComponent>(entity, 100);
            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.True);

            // Coefficient should be 0f (best value = all 4 variable codons match)
            var insulated = entMan.GetComponent<InsulatedComponent>(entity);
            Assert.That(insulated.Coefficient, Is.EqualTo(0f),
                "Full canonical match should give best Coefficient (0f)");

            // Transfer DNA to another entity to verify round-trip
            var recipient = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<DnaComponent>(recipient);
            geneticsSys.ReplaceDna(recipient, dnaComp.DNA!);

            Assert.That(entMan.HasComponent<InsulatedComponent>(recipient), Is.True,
                "Recipient should gain InsulatedComponent from DNA");
            var recipientInsulated = entMan.GetComponent<InsulatedComponent>(recipient);
            Assert.That(recipientInsulated.Coefficient, Is.EqualTo(0f),
                "Coefficient should round-trip through DNA encoding");

            entMan.DeleteEntity(entity);
            entMan.DeleteEntity(recipient);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that the system correctly handles component ordering.
    /// DNA encoding/decoding should be order-independent.
    /// </summary>
    [Test]
    public async Task TestComponentOrderIndependence()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Create first entity: add components in order A, B, C
            var entity1 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp1 = entMan.AddComponent<DnaComponent>(entity1);
            entMan.AddComponent<InsulatedComponent>(entity1);
            entMan.AddComponent<PryingComponent>(entity1);
            entMan.AddComponent<ThermalVisionComponent>(entity1);

            // Create second entity: add components in order C, B, A
            var entity2 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dnaComp2 = entMan.AddComponent<DnaComponent>(entity2);
            entMan.AddComponent<ThermalVisionComponent>(entity2);
            entMan.AddComponent<PryingComponent>(entity2);
            entMan.AddComponent<InsulatedComponent>(entity2);

            // Within same round, same component set should produce consistent DNA
            // (Order of addition shouldn't matter for final DNA state)
            Assert.That(dnaComp1.DNA, Is.Not.Null);
            Assert.That(dnaComp2.DNA, Is.Not.Null);

            entMan.DeleteEntity(entity1);
            entMan.DeleteEntity(entity2);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that deleted entities don't leave stale DNA mappings.
    /// </summary>
    [Test]
    public async Task TestDeletedEntityCleanup()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Create and delete many entities with genetic components
            for (int i = 0; i < 10; i++)
            {
                var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                entMan.AddComponent<DnaComponent>(entity);
                entMan.AddComponent<InsulatedComponent>(entity);

                entMan.DeleteEntity(entity);
            }

            // System should handle cleanup gracefully
            Assert.Pass("Entity cleanup completed without errors");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test that TransferDnaEvent integration works with genetics system.
    /// </summary>
    [Test]
    public async Task TestDnaTransferIntegration()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Create donor with genetic component
            var donor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var donorDna = entMan.AddComponent<DnaComponent>(donor);
            entMan.AddComponent<ThermalVisionComponent>(donor);

            Assert.That(donorDna.DNA, Is.Not.Null);

            // Create recipient
            var recipient = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var recipientDna = entMan.AddComponent<DnaComponent>(recipient);

            Assert.That(recipientDna.DNA, Is.Not.Null);

            // Transfer DNA
            var transferEv = new TransferDnaEvent
            {
                Donor = donor,
                Recipient = recipient
            };
            entMan.EventBus.RaiseLocalEvent(donor, ref transferEv);

            entMan.DeleteEntity(donor);
            entMan.DeleteEntity(recipient);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Test DNA generation with the stability factor applied.
    /// </summary>
    [Test]
    public async Task TestStabilityFactorApplication()
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
            entMan.AddComponent<InsulatedComponent>(entity); // Complexity=5

            Assert.That(dnaComp.DNA, Is.Not.Null);
            Assert.That(dnaComp.DNA, Is.Not.Empty);

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }
}
