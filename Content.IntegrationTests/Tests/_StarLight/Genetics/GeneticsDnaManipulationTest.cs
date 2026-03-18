using System.Linq;
using System.Threading.Tasks;
using Content.Server.Genetics;
using Content.Shared.Electrocution;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Forensics.Components;
using Content.Shared.Prying.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Genetics;

/// <summary>
/// Tests for the DNA manipulation API on GeneticsSystem:
/// IncreaseCloseness, DecreaseCloseness, ReplaceDna, ReplaceDnaSegment, MutateRandom.
/// </summary>
[TestFixture]
public sealed class GeneticsDnaManipulationTest
{
    /// <summary>
    /// IncreaseCloseness should move DNA toward the canonical sequence.
    /// With a large enough amount it should add the component via reconciliation.
    /// </summary>
    [Test]
    public async Task TestIncreaseClosenessAddsComponent()
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
            // Entity with DNA but without InsulatedComponent
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<DnaComponent>(entity);

            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.False,
                "Entity should not start with InsulatedComponent");

            // Fix all mismatches — should cross the stability threshold and add the component
            var fixed1 = geneticsSys.IncreaseCloseness<InsulatedComponent>(entity, 100);
            Assert.That(fixed1, Is.GreaterThan(0), "Should have fixed at least one mismatch");

            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.True,
                "InsulatedComponent should be added after increasing closeness past stability threshold");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Calling IncreaseCloseness when the gene already fully matches should return 0.
    /// </summary>
    [Test]
    public async Task TestIncreaseClosenessNoOpWhenMatched()
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

            // DNA already has canonical sequence — nothing to fix
            var fixed1 = geneticsSys.IncreaseCloseness<InsulatedComponent>(entity, 1);
            Assert.That(fixed1, Is.EqualTo(0), "Should return 0 when gene already fully matches");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// DecreaseCloseness should move DNA away from the canonical sequence.
    /// With a large enough amount it should remove the component via reconciliation.
    /// </summary>
    [Test]
    public async Task TestDecreaseClosenessRemovesComponent()
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

            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.True);

            // Corrupt all matching codons — should cross threshold and remove the component
            var corrupted = geneticsSys.DecreaseCloseness<InsulatedComponent>(entity, 100);
            Assert.That(corrupted, Is.GreaterThan(0), "Should have corrupted at least one codon");

            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.False,
                "InsulatedComponent should be removed after decreasing closeness past stability threshold");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// DecreaseCloseness on a gene that is already fully mismatched should return 0.
    /// </summary>
    [Test]
    public async Task TestDecreaseClosenessNoOpWhenMismatched()
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
            // Entity without InsulatedComponent — gene region is fully scrambled
            var entity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<DnaComponent>(entity);

            var corrupted = geneticsSys.DecreaseCloseness<InsulatedComponent>(entity, 1);
            Assert.That(corrupted, Is.EqualTo(0), "Should return 0 when gene is already fully mismatched");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// IncreaseCloseness followed by DecreaseCloseness should round-trip:
    /// add then remove the component.
    /// </summary>
    [Test]
    public async Task TestIncreaseDecreaseRoundTrip()
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

            Assert.That(entMan.HasComponent<PryingComponent>(entity), Is.False);

            // Increase to add
            geneticsSys.IncreaseCloseness<PryingComponent>(entity, 100);
            Assert.That(entMan.HasComponent<PryingComponent>(entity), Is.True,
                "Component should be added after increasing closeness");

            var dnaWithComponent = dnaComp.DNA;

            // Decrease to remove
            geneticsSys.DecreaseCloseness<PryingComponent>(entity, 100);
            Assert.That(entMan.HasComponent<PryingComponent>(entity), Is.False,
                "Component should be removed after decreasing closeness");

            Assert.That(dnaComp.DNA, Is.Not.EqualTo(dnaWithComponent),
                "DNA should differ after decrease");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// ReplaceDna should wholesale-replace the DNA and reconcile components.
    /// Copying DNA from an entity with a component should grant that component to the target.
    /// </summary>
    [Test]
    public async Task TestReplaceDnaTransfersTraits()
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
            // Donor: has InsulatedComponent
            var donor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var donorDna = entMan.AddComponent<DnaComponent>(donor);
            entMan.AddComponent<InsulatedComponent>(donor);

            // Recipient: does not have InsulatedComponent
            var recipient = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<DnaComponent>(recipient);

            Assert.That(entMan.HasComponent<InsulatedComponent>(recipient), Is.False);

            // Transfer DNA
            var result = geneticsSys.ReplaceDna(recipient, donorDna.DNA!);
            Assert.That(result, Is.True, "ReplaceDna should succeed");

            // Recipient should now have InsulatedComponent
            Assert.That(entMan.HasComponent<InsulatedComponent>(recipient), Is.True,
                "Recipient should gain the donor's genetic components after DNA replacement");

            entMan.DeleteEntity(donor);
            entMan.DeleteEntity(recipient);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// ReplaceDna with a DNA from an entity lacking a component should remove that component.
    /// </summary>
    [Test]
    public async Task TestReplaceDnaRemovesTraits()
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
            // Source of "no InsulatedComponent" DNA
            var plain = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var plainDna = entMan.AddComponent<DnaComponent>(plain);

            // Target: has InsulatedComponent
            var target = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<DnaComponent>(target);
            entMan.AddComponent<InsulatedComponent>(target);

            Assert.That(entMan.HasComponent<InsulatedComponent>(target), Is.True);

            // Replace with plain DNA (no InsulatedComponent gene)
            geneticsSys.ReplaceDna(target, plainDna.DNA!);

            Assert.That(entMan.HasComponent<InsulatedComponent>(target), Is.False,
                "InsulatedComponent should be removed after replacing DNA with non-matching sequence");

            entMan.DeleteEntity(plain);
            entMan.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// ReplaceDna should reject DNA strings of the wrong length.
    /// </summary>
    [Test]
    public async Task TestReplaceDnaInvalidLength()
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

            var result = geneticsSys.ReplaceDna(entity, "ATGC");
            Assert.That(result, Is.False, "ReplaceDna should reject wrong-length DNA");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// ReplaceDnaSegment should only modify the specified region and reconcile
    /// overlapping genes.
    /// </summary>
    [Test]
    public async Task TestReplaceDnaSegmentPartialUpdate()
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

            var originalDna = dnaComp.DNA!;
            Assert.That(originalDna.Length, Is.GreaterThan(2));

            // Replace first 2 characters
            var result = geneticsSys.ReplaceDnaSegment(entity, 0, "AA");
            Assert.That(result, Is.True, "ReplaceDnaSegment should succeed");

            // First two chars should be 'A', rest unchanged
            Assert.That(dnaComp.DNA![0], Is.EqualTo('A'));
            Assert.That(dnaComp.DNA![1], Is.EqualTo('A'));
            Assert.That(dnaComp.DNA!.Substring(2), Is.EqualTo(originalDna.Substring(2)),
                "Characters outside the segment should be unchanged");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// ReplaceDnaSegment should reject out-of-bounds ranges.
    /// </summary>
    [Test]
    public async Task TestReplaceDnaSegmentBoundsCheck()
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

            var originalDna = dnaComp.DNA!;

            // Try to write past the end
            var result = geneticsSys.ReplaceDnaSegment(entity, originalDna.Length - 1, "AAAA");
            Assert.That(result, Is.False, "Should reject out-of-bounds segment");

            // Negative start
            result = geneticsSys.ReplaceDnaSegment(entity, -1, "A");
            Assert.That(result, Is.False, "Should reject negative start index");

            // DNA should be unchanged
            Assert.That(dnaComp.DNA, Is.EqualTo(originalDna), "DNA should not change on failed segment replace");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// MutateRandom should change exactly the requested number of codons.
    /// </summary>
    [Test]
    public async Task TestMutateRandomChangesDna()
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

            var originalDna = dnaComp.DNA!;

            var mutated = geneticsSys.MutateRandom(entity, 3);
            Assert.That(mutated, Is.EqualTo(3), "Should report 3 mutations");

            // DNA should have changed
            Assert.That(dnaComp.DNA, Is.Not.EqualTo(originalDna), "DNA should differ after mutation");

            // Length preserved
            Assert.That(dnaComp.DNA!.Length, Is.EqualTo(originalDna.Length),
                "DNA length should be preserved after mutation");

            // Still valid nucleotides
            Assert.That(dnaComp.DNA!.All(c => c is 'A' or 'T' or 'G' or 'C'), Is.True,
                "Mutated DNA should only contain valid nucleotides");

            // Exactly 3 positions should differ
            var diffs = 0;
            for (var i = 0; i < originalDna.Length; i++)
            {
                if (originalDna[i] != dnaComp.DNA![i])
                    diffs++;
            }
            Assert.That(diffs, Is.EqualTo(3), "Exactly 3 codons should have changed");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Enough random mutations in a gene region should eventually remove the component.
    /// We use a targeted approach: mutate the full DNA to guarantee hitting the gene.
    /// </summary>
    [Test]
    public async Task TestMutateRandomCanRemoveComponent()
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

            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.True);

            // Mutate every single codon — guaranteed to hit the gene region
            geneticsSys.MutateRandom(entity, dnaComp.DNA!.Length);

            // The gene region should now be scrambled, component should be gone
            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.False,
                "Component should be removed after mutating every codon");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// All DNA manipulation methods should preserve DNA length.
    /// </summary>
    [Test]
    public async Task TestAllManipulationsPreserveLength()
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
            var expectedLen = dnaComp.DNA!.Length;

            // IncreaseCloseness
            geneticsSys.IncreaseCloseness<InsulatedComponent>(entity, 2);
            Assert.That(dnaComp.DNA!.Length, Is.EqualTo(expectedLen), "IncreaseCloseness should preserve length");

            // DecreaseCloseness
            geneticsSys.DecreaseCloseness<InsulatedComponent>(entity, 2);
            Assert.That(dnaComp.DNA!.Length, Is.EqualTo(expectedLen), "DecreaseCloseness should preserve length");

            // MutateRandom
            geneticsSys.MutateRandom(entity, 5);
            Assert.That(dnaComp.DNA!.Length, Is.EqualTo(expectedLen), "MutateRandom should preserve length");

            // ReplaceDnaSegment
            geneticsSys.ReplaceDnaSegment(entity, 0, "ATG");
            Assert.That(dnaComp.DNA!.Length, Is.EqualTo(expectedLen), "ReplaceDnaSegment should preserve length");

            // ReplaceDna (build a valid-length string)
            var replacement = new string('A', expectedLen);
            geneticsSys.ReplaceDna(entity, replacement);
            Assert.That(dnaComp.DNA!.Length, Is.EqualTo(expectedLen), "ReplaceDna should preserve length");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Manipulation methods should gracefully handle entities without DnaComponent.
    /// </summary>
    [Test]
    public async Task TestManipulationWithoutDna()
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

            // All methods should return 0/false without errors
            Assert.That(geneticsSys.IncreaseCloseness<InsulatedComponent>(entity), Is.EqualTo(0));
            Assert.That(geneticsSys.DecreaseCloseness<InsulatedComponent>(entity), Is.EqualTo(0));
            Assert.That(geneticsSys.ReplaceDna(entity, "ATGC"), Is.False);
            Assert.That(geneticsSys.ReplaceDnaSegment(entity, 0, "A"), Is.False);
            Assert.That(geneticsSys.MutateRandom(entity), Is.EqualTo(0));

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// DNA manipulation on one gene should not affect other genes.
    /// </summary>
    [Test]
    public async Task TestManipulationIsolation()
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
            entMan.AddComponent<PryingComponent>(entity);

            // Decrease closeness for Insulated only
            geneticsSys.DecreaseCloseness<InsulatedComponent>(entity, 100);

            // Insulated should be gone, Prying should remain
            Assert.That(entMan.HasComponent<InsulatedComponent>(entity), Is.False,
                "Targeted gene should be affected");
            Assert.That(entMan.HasComponent<PryingComponent>(entity), Is.True,
                "Other genetic components should not be affected");

            entMan.DeleteEntity(entity);
        });

        await pair.CleanReturnAsync();
    }
}
