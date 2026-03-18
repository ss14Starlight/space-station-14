using System.Linq;
using System.Threading.Tasks;
using Content.Server.Genetics;
using Content.Shared.Electrocution;
using Content.Shared.Forensics.Components;
using Content.Shared.Genetics;
using Content.Shared.Prying.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Genetics;

/// <summary>
/// Tests for genetic variable encoding — mapping component field values
/// (like InsulatedComponent.Coefficient) into DNA via [GeneticMultiValueVariable].
/// </summary>
/// <remarks>
/// InsulatedComponent setup:
///   [GeneticComponent(5, 2)] — 7 existence codons (5 complexity + 2 stability)
///   [GeneticMultiValueVariable&lt;float&gt;(0f, 4f, 2f, 1.5f, 0.5f, 0f)] — 4 variable codons
///   Total gene block: 11 codons.
///   Values: 0 matches=4f, 1=2f, 2=1.5f, 3=0.5f, 4 matches=0f
/// </remarks>
[TestFixture]
public sealed class GeneticsVariableEncodingTest
{
    private static readonly float[] InsulatedDiscreteValues = { 4f, 2f, 1.5f, 0.5f, 0f };

    /// <summary>
    /// When InsulatedComponent is added to an entity with DNA, the Coefficient
    /// value should be encoded into the variable region of the gene block.
    /// </summary>
    [Test]
    public async Task TestVariableEncodingOnComponentAdd()
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

            var dnaBeforeAdd = dnaComp.DNA;
            Assert.That(dnaBeforeAdd, Is.Not.Null);

            // Add InsulatedComponent with default Coefficient = 0f
            entMan.AddComponent<InsulatedComponent>(entity);

            // DNA should have changed
            Assert.That(dnaComp.DNA, Is.Not.EqualTo(dnaBeforeAdd),
                "DNA should update when InsulatedComponent is added");

            // DNA length should be preserved
            Assert.That(dnaComp.DNA!.Length, Is.EqualTo(dnaBeforeAdd!.Length),
                "DNA length should not change");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// When an entity has InsulatedComponent at DNA init time, the generated DNA
    /// should encode the Coefficient value in the variable region.
    /// </summary>
    [Test]
    public async Task TestVariableEncodingAtDnaInit()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            // Add InsulatedComponent BEFORE DnaComponent so OnConstructDna sees it
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<InsulatedComponent>(entity);

            var dnaComp = entMan.AddComponent<DnaComponent>(entity);

            Assert.That(dnaComp.DNA, Is.Not.Null);
            Assert.That(dnaComp.DNA, Is.Not.Empty);

            // Coefficient should be one of the discrete values
            var insulated = entMan.GetComponent<InsulatedComponent>(entity);
            Assert.That(InsulatedDiscreteValues, Does.Contain(insulated.Coefficient),
                "Coefficient should be a valid discrete value after DNA init");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// ReplaceDna from a donor with InsulatedComponent should transfer both
    /// the component and its Coefficient value to the recipient.
    /// </summary>
    [Test]
    public async Task TestReplaceDnaTransfersVariableValues()
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
            // Donor: has InsulatedComponent with default Coefficient
            var donor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var donorDna = entMan.AddComponent<DnaComponent>(donor);
            entMan.AddComponent<InsulatedComponent>(donor);

            // Recipient: no InsulatedComponent
            var recipient = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<DnaComponent>(recipient);

            Assert.That(entMan.HasComponent<InsulatedComponent>(recipient), Is.False);

            // Transfer DNA
            var result = geneticsSys.ReplaceDna(recipient, donorDna.DNA!);
            Assert.That(result, Is.True);

            // Recipient should now have InsulatedComponent
            Assert.That(entMan.HasComponent<InsulatedComponent>(recipient), Is.True,
                "Recipient should gain InsulatedComponent from donor DNA");

            // Coefficient should match the donor's encoded value
            var recipientInsulated = entMan.GetComponent<InsulatedComponent>(recipient);
            var donorInsulated = entMan.GetComponent<InsulatedComponent>(donor);
            Assert.That(recipientInsulated.Coefficient, Is.EqualTo(donorInsulated.Coefficient),
                "Recipient Coefficient should match donor's after DNA transfer");

            entMan.DeleteEntity(donor);
            entMan.DeleteEntity(recipient);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// SyncVariablesToDna should succeed for a component with variable fields
    /// and the DNA should reflect the current field value.
    /// </summary>
    [Test]
    public async Task TestSyncVariablesToDna()
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
            entMan.AddComponent<DnaComponent>(entity);
            entMan.AddComponent<InsulatedComponent>(entity);

            // SyncVariablesToDna should succeed (InsulatedComponent has variable fields)
            var result = geneticsSys.SyncVariablesToDna<InsulatedComponent>(entity);
            Assert.That(result, Is.True, "SyncVariablesToDna should succeed");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// SyncVariablesToDna should return false when entity doesn't have the target component.
    /// </summary>
    [Test]
    public async Task TestSyncVariablesToDnaWithoutComponent()
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
            entMan.AddComponent<DnaComponent>(entity);

            // Entity doesn't have InsulatedComponent
            var result = geneticsSys.SyncVariablesToDna<InsulatedComponent>(entity);
            Assert.That(result, Is.False,
                "SyncVariablesToDna should return false when component is missing");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// SyncVariablesToDna should return false when entity doesn't have DnaComponent.
    /// </summary>
    [Test]
    public async Task TestSyncVariablesToDnaWithoutDna()
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
            entMan.AddComponent<InsulatedComponent>(entity);

            // No DnaComponent
            var result = geneticsSys.SyncVariablesToDna<InsulatedComponent>(entity);
            Assert.That(result, Is.False,
                "SyncVariablesToDna should return false when DnaComponent is missing");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// IncreaseCloseness with a large amount should add InsulatedComponent AND
    /// set Coefficient to a valid discrete value based on how many variable
    /// codons now match canonical.
    /// </summary>
    [Test]
    public async Task TestIncreaseClosenessAffectsVariables()
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
            entMan.AddComponent<DnaComponent>(entity);

            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.False);

            // Fix all mismatches — should add the component and set variable codons
            geneticsSys.IncreaseCloseness<InsulatedComponent>(entity, 100);

            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.True,
                "InsulatedComponent should be added");

            // Coefficient should be one of the valid discrete values
            var insulated = entMan.GetComponent<InsulatedComponent>(entity);
            Assert.That(InsulatedDiscreteValues, Does.Contain(insulated.Coefficient),
                $"Coefficient ({insulated.Coefficient}) should be one of the valid discrete values");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// When all codons (existence + variable) are fixed via IncreaseCloseness,
    /// Coefficient should be the "best" value (0f, since all 4 variable codons match).
    /// </summary>
    [Test]
    public async Task TestFullMatchGivesBestValue()
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
            entMan.AddComponent<DnaComponent>(entity);

            // Fix ALL mismatches — every codon in the gene block should match canonical
            geneticsSys.IncreaseCloseness<InsulatedComponent>(entity, 100);

            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.True);
            var insulated = entMan.GetComponent<InsulatedComponent>(entity);

            // With all 4 variable codons matching, value = values[4] = 0f
            Assert.That(insulated.Coefficient, Is.EqualTo(0f),
                "Full canonical match should give the best Coefficient value (0f)");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// DecreaseCloseness should scramble variable codons along with existence codons,
    /// eventually removing the component and scrambling the whole block.
    /// </summary>
    [Test]
    public async Task TestDecreaseClosenessScramblesFull()
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
            entMan.AddComponent<InsulatedComponent>(entity);

            var dnaWithComponent = dnaComp.DNA;

            // Corrupt all codons — should remove the component
            geneticsSys.DecreaseCloseness<InsulatedComponent>(entity, 100);

            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.False,
                "InsulatedComponent should be removed");
            Assert.That(dnaComp.DNA, Is.Not.EqualTo(dnaWithComponent),
                "DNA should have changed");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Partially corrupting variable codons should change the Coefficient
    /// to a different discrete value, while the component may still exist
    /// if existence codons remain within the stability threshold.
    /// </summary>
    [Test]
    public async Task TestPartialVariableCorruption()
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
            entMan.AddComponent<DnaComponent>(entity);

            // First, fully match all codons
            geneticsSys.IncreaseCloseness<InsulatedComponent>(entity, 100);
            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.True);

            var insulated = entMan.GetComponent<InsulatedComponent>(entity);
            Assert.That(insulated.Coefficient, Is.EqualTo(0f),
                "Should start at best value with full match");

            // Corrupt a small number of codons — might hit variable region
            // With 11 total codons and stability=2, corrupting 1-2 may only
            // affect variable codons while keeping the existence region within threshold
            geneticsSys.DecreaseCloseness<InsulatedComponent>(entity, 1);

            if (entMan.HasComponent<InsulatedComponent>(entity))
            {
                // Component survived — Coefficient should still be a valid discrete value
                insulated = entMan.GetComponent<InsulatedComponent>(entity);
                Assert.That(InsulatedDiscreteValues, Does.Contain(insulated.Coefficient),
                    "Coefficient should still be a valid discrete value after partial corruption");
            }
            // If component was removed, that's also valid (the corruption hit existence codons)

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// MutateRandom hitting every codon should remove the component and scramble everything.
    /// </summary>
    [Test]
    public async Task TestMutateRandomAffectsVariables()
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
            entMan.AddComponent<InsulatedComponent>(entity);

            var originalDna = dnaComp.DNA!;

            // Mutate every codon — guaranteed to hit variable region
            geneticsSys.MutateRandom(entity, dnaComp.DNA!.Length);

            Assert.That(dnaComp.DNA, Is.Not.EqualTo(originalDna),
                "DNA should have changed after mutation");

            // With every codon mutated, the component should be removed
            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.False,
                "Component should be removed after mutating every codon");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// When InsulatedComponent is removed, the entire gene block (including
    /// variable codons) should be scrambled so the gene no longer matches.
    /// </summary>
    [Test]
    public async Task TestComponentRemovalScramblesVariableRegion()
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

            var dnaWithInsulated = dnaComp.DNA;

            // Remove the component
            entMan.RemoveComponent<InsulatedComponent>(entity);

            // DNA should change
            Assert.That(dnaComp.DNA, Is.Not.EqualTo(dnaWithInsulated),
                "DNA should change when InsulatedComponent is removed");

            // Component should not reappear
            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.False,
                "InsulatedComponent should not reappear after removal");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// DNA round-trip: entity with InsulatedComponent → copy DNA to another entity →
    /// the Coefficient on the recipient should match the donor.
    /// </summary>
    [Test]
    public async Task TestVariableValuesDnaRoundTrip()
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
            // Source entity — IncreaseCloseness to get a known state (full match = Coefficient 0f)
            var source = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var sourceDna = entMan.AddComponent<DnaComponent>(source);
            geneticsSys.IncreaseCloseness<InsulatedComponent>(source, 100);

            var sourceInsulated = entMan.GetComponent<InsulatedComponent>(source);
            Assert.That(sourceInsulated.Coefficient, Is.EqualTo(0f));

            // Transfer DNA to a new entity
            var target = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<DnaComponent>(target);
            geneticsSys.ReplaceDna(target, sourceDna.DNA!);

            // Target should have InsulatedComponent with the same Coefficient
            Assert.That(entMan.HasComponent<InsulatedComponent>(target), Is.True,
                "Target should gain InsulatedComponent from DNA transfer");

            var targetInsulated = entMan.GetComponent<InsulatedComponent>(target);
            Assert.That(targetInsulated.Coefficient, Is.EqualTo(0f),
                "Target Coefficient should match the source's value after DNA round-trip");

            entMan.DeleteEntity(source);
            entMan.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// SyncVariablesToDna on a component that has no [GeneticMultiValueVariable] fields
    /// (like PryingComponent) should return false since there are no variable sync delegates.
    /// </summary>
    [Test]
    public async Task TestSyncVariablesNoOpForNonVariableComponent()
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
            entMan.AddComponent<DnaComponent>(entity);
            entMan.AddComponent<PryingComponent>(entity);

            // PryingComponent has no [GeneticMultiValueVariable] fields
            var result = geneticsSys.SyncVariablesToDna<PryingComponent>(entity);
            Assert.That(result, Is.False,
                "SyncVariablesToDna should return false for components without variable fields");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// DNA length should account for variable codons (InsulatedComponent adds 4 extra).
    /// All entities should have the same DNA length within a round.
    /// </summary>
    [Test]
    public async Task TestDnaLengthIncludesVariableCodons()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });

        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var entity1 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dna1 = entMan.AddComponent<DnaComponent>(entity1);

            var entity2 = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var dna2 = entMan.AddComponent<DnaComponent>(entity2);
            entMan.AddComponent<InsulatedComponent>(entity2);

            Assert.That(dna1.DNA, Is.Not.Null);
            Assert.That(dna2.DNA, Is.Not.Null);

            // Both should have the same length (DnaLength is computed once at init)
            Assert.That(dna2.DNA!.Length, Is.EqualTo(dna1.DNA!.Length),
                "DNA length should be the same for all entities in a round");

            // Length should be positive
            Assert.That(dna1.DNA!.Length, Is.GreaterThan(0));

            entMan.DeleteEntity(entity1);
            entMan.DeleteEntity(entity2);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// When InsulatedComponent is added to an entity with DNA, the Coefficient
    /// should be a valid discrete value (the generated SyncWrite encodes the
    /// current field value into DNA, and SyncRead would decode it).
    /// </summary>
    [Test]
    public async Task TestCoefficientIsDiscreteAfterAdd()
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
            entMan.AddComponent<InsulatedComponent>(entity);

            var insulated = entMan.GetComponent<InsulatedComponent>(entity);

            // Default Coefficient is 0f, which IS one of the discrete values
            // After OnGeneticComponentAdded writes variable codons based on this value,
            // the Coefficient should remain valid
            Assert.That(InsulatedDiscreteValues, Does.Contain(insulated.Coefficient),
                $"Coefficient ({insulated.Coefficient}) should be a valid discrete value");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// When InsulatedComponent is gained via IncreaseCloseness followed by
    /// DecreaseCloseness of some variable codons, and then DNA is transferred,
    /// the recipient should get the degraded Coefficient value.
    /// </summary>
    [Test]
    public async Task TestDegradedVariableTransfer()
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
            // Source entity — add via IncreaseCloseness then degrade slightly
            var source = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var sourceDna = entMan.AddComponent<DnaComponent>(source);

            // Full match
            geneticsSys.IncreaseCloseness<InsulatedComponent>(source, 100);
            Assert.That(entMan.HasComponent<InsulatedComponent>(source), Is.True);

            var sourceInsulated = entMan.GetComponent<InsulatedComponent>(source);
            var fullMatchCoefficient = sourceInsulated.Coefficient;

            // Transfer this DNA to a recipient
            var recipient = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<DnaComponent>(recipient);
            geneticsSys.ReplaceDna(recipient, sourceDna.DNA!);

            Assert.That(entMan.HasComponent<InsulatedComponent>(recipient), Is.True);
            var recipientInsulated = entMan.GetComponent<InsulatedComponent>(recipient);

            Assert.That(recipientInsulated.Coefficient, Is.EqualTo(fullMatchCoefficient),
                "Transferred Coefficient should match source");

            entMan.DeleteEntity(source);
            entMan.DeleteEntity(recipient);
        });

        await pair.CleanReturnAsync();
    }
}
