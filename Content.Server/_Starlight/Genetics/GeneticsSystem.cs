using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;
using Content.Shared.Genetics;

namespace Content.Server.Genetics;

/// <summary>
/// Governs the mapping of DNA to traits and components.
/// </summary>
public sealed partial class GeneticsSystem : SharedGeneticsSystem
{
    public override void Initialize()
    {
        base.Initialize();

        // On round start, we need to make a new DNA table for the new round
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        // When we're generating a new DNA sequence for a new entity, we want to
        // hook that and inject a DNA string obtained by scanning that entity.
        SubscribeLocalEvent<DnaComponent, ConstructDnaEvent>(OnConstructDna);

        // This method gets source-generated; it adds methods that track the
        // addition and removal of components covered by the genetics system
        // and ensures that the DNA gets updated appropriately.
        InitializeGenerated();

        // Now that both shared and server components have been registered into
        // GeneticComponents, compute the total DNA length and set up initial round records.
        ComputeDnaLength();

        // Set up the records for the first round.
        SetupRoundRecords();
    }

    /// <summary>
    /// Sets up the records for the current round. This should be called after the end of a round,
    /// and once before the first round.
    /// </summary>
    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        SetupRoundRecords();
    }

    /// <summary>
    /// Called during the initialization of DnaComponent; replaces the previous totally-random 
    private void OnConstructDna(Entity<DnaComponent> subject, ref ConstructDnaEvent ev)
    {
        if (ev.DNA is not null)
        {
            // If the event already has a DNA string, then something else has already generated a DNA for this entity.
            // In that case, we don't want to mess with it. There's probably going to be interesting behavior though!
            return;
        }

        // We want to generate a sequence that's random, and then overwrite the parts of it that correspond to traits
        // with either non-matching sequences (if the entity doesn't have the trait) or matching sequences (if it does).
        var chars = new char[DnaLength];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Nucleotides[_random.Next(Nucleotides.Length)];

        // For each genetic component, check if the entity has it and write
        // the canonical sequence into its gene region. Conversely, if it doesn't
        // have it, write a non-matching sequence to ensure that the gene region doesn't accidentally match.
        foreach (var (type, record) in CurrentRoundRecords)
        {
            if (EntityManager.HasComponent(subject.Owner, type))
            {
                for (var i = 0; i < record.Length; i++)
                    chars[record.StartIndex + i] = record.CanonicalSequence[i];
            }
            else
            {
                for (var i = 0; i < record.Length; i++)
                {
                    while(chars[record.StartIndex + i] == record.CanonicalSequence[i])
                        chars[record.StartIndex + i] = Nucleotides[_random.Next(Nucleotides.Length)];
                }
            }
        }

        ev.DNA = new string(chars);
    }

    // ──────────────────────────────────────────────────────────────
    //  Public DNA manipulation API
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Move the gene region for <typeparamref name="T"/> closer to its canonical
    /// sequence by fixing up to <paramref name="amount"/> random mismatched codons.
    /// If this causes the gene to match within the stability threshold, the
    /// component will be added to the entity.
    /// </summary>
    /// <returns>The number of codons actually fixed (may be less than amount if fewer mismatches existed).</returns>
    public int IncreaseCloseness<T>(EntityUid uid, int amount = 1) where T : IComponent
    {
        if (!TryGetDnaForUpdate(uid, out var dnaComp, out var dna))
            return 0;

        if (!CurrentRoundRecords.TryGetValue(typeof(T), out var record))
        {
            Log.Warning($"IncreaseCloseness<{typeof(T).Name}>: no gene record found.");
            return 0;
        }

        var chars = dna.ToCharArray();

        // Collect indices of mismatched codons
        var mismatched = new List<int>();
        for (var i = 0; i < record.Length; i++)
        {
            if (chars[record.StartIndex + i] != record.CanonicalSequence[i])
                mismatched.Add(i);
        }

        if (mismatched.Count == 0)
            return 0;

        // Fix up to `amount` random mismatches
        var toFix = Math.Min(amount, mismatched.Count);
        _random.Shuffle(mismatched);

        for (var i = 0; i < toFix; i++)
            chars[record.StartIndex + mismatched[i]] = record.CanonicalSequence[mismatched[i]];

        dnaComp.DNA = new string(chars);
        Dirty(uid, dnaComp);

        ReconcileGene(uid, dnaComp.DNA.AsSpan(), record);
        return toFix;
    }

    /// <summary>
    /// Move the gene region for <typeparamref name="T"/> further from its canonical
    /// sequence by corrupting up to <paramref name="amount"/> random matching codons.
    /// If this causes the gene to no longer match within the stability threshold,
    /// the component will be removed from the entity.
    /// </summary>
    /// <returns>The number of codons actually corrupted.</returns>
    public int DecreaseCloseness<T>(EntityUid uid, int amount = 1) where T : IComponent
    {
        if (!TryGetDnaForUpdate(uid, out var dnaComp, out var dna))
            return 0;

        if (!CurrentRoundRecords.TryGetValue(typeof(T), out var record))
        {
            Log.Warning($"DecreaseCloseness<{typeof(T).Name}>: no gene record found.");
            return 0;
        }

        var chars = dna.ToCharArray();

        // Collect indices of matching codons
        var matched = new List<int>();
        for (var i = 0; i < record.Length; i++)
        {
            if (chars[record.StartIndex + i] == record.CanonicalSequence[i])
                matched.Add(i);
        }

        if (matched.Count == 0)
            return 0;

        // Corrupt up to `amount` random matches
        var toCorrupt = Math.Min(amount, matched.Count);
        _random.Shuffle(matched);

        for (var i = 0; i < toCorrupt; i++)
        {
            var idx = record.StartIndex + matched[i];
            var canonical = record.CanonicalSequence[matched[i]];
            char newChar;
            do
            {
                newChar = Nucleotides[_random.Next(Nucleotides.Length)];
            } while (newChar == canonical);
            chars[idx] = newChar;
        }

        dnaComp.DNA = new string(chars);
        Dirty(uid, dnaComp);

        ReconcileGene(uid, dnaComp.DNA.AsSpan(), record);
        return toCorrupt;
    }

    /// <summary>
    /// Replace the entity's entire DNA string, then reconcile all gene regions
    /// to add/remove components as needed.
    /// </summary>
    /// <returns>True if the DNA was successfully replaced.</returns>
    public bool ReplaceDna(EntityUid uid, string newDna)
    {
        if (!TryGetDnaForUpdate(uid, out var dnaComp, out _))
            return false;

        if (newDna.Length != DnaLength)
        {
            Log.Warning($"ReplaceDna: expected length {DnaLength}, got {newDna.Length}.");
            return false;
        }

        dnaComp.DNA = newDna;
        Dirty(uid, dnaComp);

        ReconcileAllGenes(uid, newDna.AsSpan());
        return true;
    }

    /// <summary>
    /// Overwrite a segment of the entity's DNA starting at <paramref name="startIndex"/>,
    /// then reconcile any gene regions that overlap with the modified range.
    /// </summary>
    /// <returns>True if the segment was successfully written.</returns>
    public bool ReplaceDnaSegment(EntityUid uid, int startIndex, string segment)
    {
        if (!TryGetDnaForUpdate(uid, out var dnaComp, out var dna))
            return false;

        if (startIndex < 0 || startIndex + segment.Length > DnaLength)
        {
            Log.Warning($"ReplaceDnaSegment: range [{startIndex}..{startIndex + segment.Length}) is out of bounds (DnaLength={DnaLength}).");
            return false;
        }

        var chars = dna.ToCharArray();
        segment.AsSpan().CopyTo(chars.AsSpan(startIndex));

        dnaComp.DNA = new string(chars);
        Dirty(uid, dnaComp);

        ReconcileOverlappingGenes(uid, dnaComp.DNA.AsSpan(), startIndex, segment.Length);
        return true;
    }

    /// <summary>
    /// Randomly mutate <paramref name="amount"/> codons anywhere in the DNA,
    /// then reconcile only the specific gene regions that were affected.
    /// </summary>
    /// <returns>The number of codons actually mutated.</returns>
    public int MutateRandom(EntityUid uid, int amount = 1)
    {
        if (!TryGetDnaForUpdate(uid, out var dnaComp, out var dna))
            return 0;

        var chars = dna.ToCharArray();
        amount = Math.Min(amount, DnaLength);

        // Pick `amount` unique random positions
        var positions = new List<int>(DnaLength);
        for (var i = 0; i < DnaLength; i++)
            positions.Add(i);
        _random.Shuffle(positions);

        // Track which gene records were touched so we only reconcile those
        HashSet<RoundGeneticRecord>? affectedGenes = null;

        for (var i = 0; i < amount; i++)
        {
            var pos = positions[i];
            char newChar;
            do
            {
                newChar = Nucleotides[_random.Next(Nucleotides.Length)];
            } while (newChar == chars[pos]);
            chars[pos] = newChar;

            if (CurrentRoundIndexToType.TryGetValue(pos, out var record))
            {
                affectedGenes ??= new HashSet<RoundGeneticRecord>();
                affectedGenes.Add(record);
            }
        }

        dnaComp.DNA = new string(chars);
        Dirty(uid, dnaComp);

        if (affectedGenes != null)
        {
            var span = dnaComp.DNA.AsSpan();
            foreach (var record in affectedGenes)
                ReconcileGene(uid, span, record);
        }

        return amount;
    }
}
