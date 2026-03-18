using System.Diagnostics.CodeAnalysis;
using Content.Shared.Forensics.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.Random;

namespace Content.Shared.Genetics;

/// <summary>
/// Base class for GeneticsSystem, handling genetics for components defined in Content.Shared.
/// </summary>
/// <remarks>
/// Note that it's abstract; functionality here is mostly to support us having genetics methods
/// in generated code for SharedGeneticsSystem.
/// </remarks>
public abstract partial class SharedGeneticsSystem : EntitySystem
{
    [Dependency] protected readonly IRobustRandom _random = default!;
    [Dependency] protected readonly IComponentFactory _compFactory = default!;

    /// <summary>
    /// Metadata about a component that participates in the genetics system.
    /// </summary>
    protected sealed record GeneticComponentInfo(
        Type ComponentType,
        int Complexity,
        int Stability,
        int VariableCodonCount);

    /// <summary>
    /// Maps a component type to its per-round DNA region and canonical sequence.
    /// </summary>
    protected sealed record RoundGeneticRecord(
        GeneticComponentInfo Gene,
        string CanonicalSequence,
        int StartIndex)
    {
        /// <summary>Total codons for this gene: existence region + variable region.</summary>
        public int Length => Gene.Complexity + Gene.Stability + Gene.VariableCodonCount;

        /// <summary>Number of codons in the existence-check region only.</summary>
        public int ExistenceLength => Gene.Complexity + Gene.Stability;
    }

    /// <summary>
    /// Table of all genetic components, keyed by component type.
    /// Populated at initialization by generated code in both SharedGeneticsSystem and GeneticsSystem.
    /// </summary>
    protected readonly Dictionary<Type, GeneticComponentInfo> GeneticComponents = new();

    /// <summary>
    /// Maps component type to its gene record for the current round.
    /// Used to find which codons to update when a component is added/removed.
    /// </summary>
    protected readonly Dictionary<Type, RoundGeneticRecord> CurrentRoundRecords = new();

    /// <summary>
    /// Maps each coding DNA index to the gene record that owns it.
    /// Used to determine which component is affected when a codon is mutated.
    /// </summary>
    protected readonly Dictionary<int, RoundGeneticRecord> CurrentRoundIndexToType = new();

    /// <summary>
    /// Per-component delegate that writes current field values into DNA variable codons.
    /// Registered by generated code for components that have <c>[GeneticMultiValueVariable]</c> fields.
    /// Signature: (EntityUid uid, char[] dnaChars, RoundGeneticRecord record).
    /// </summary>
    protected readonly Dictionary<Type, Action<EntityUid, char[], RoundGeneticRecord>> VariableSyncWriteDna = new();

    /// <summary>
    /// Per-component delegate that reads DNA variable codons and applies values to component fields.
    /// Registered by generated code for components that have <c>[GeneticMultiValueVariable]</c> fields.
    /// Signature: (EntityUid uid, string dna, RoundGeneticRecord record).
    /// </summary>
    protected readonly Dictionary<Type, Action<EntityUid, string, RoundGeneticRecord>> VariableSyncReadDna = new();

    // How much to inflate genomes with non-coding genes — makes everyone more
    // resistant to mutagens, but harder to isolate useful genes to flip.
    protected const float GeneticsStabilityFactor = 1.3f;

    protected static readonly char[] Nucleotides = { 'A', 'T', 'G', 'C' };

    /// <summary>
    /// The total length of a DNA sequence for this build, in codons.
    /// Computed from the sum of (Complexity + Stability) across all genetic
    /// components, scaled by <see cref="GeneticsStabilityFactor"/> to pad
    /// the genome with non-coding regions.
    /// </summary>
    protected int DnaLength;

    /// <summary>
    /// The number of non-coding codons in the DNA sequence — the padding
    /// between gene regions that absorbs random mutations.
    /// </summary>
    protected int NonCodingLength;

    /// <summary>
    /// When true, the OnGeneticComponentAdded/Removed handlers skip DNA updates.
    /// Set during reconciliation to avoid circular updates when we add/remove
    /// components in response to DNA changes.
    /// </summary>
    private bool _reconcilingDna;

    private EntityQuery<DnaComponent> _dnaQuery;

    public override void Initialize()
    {
        base.Initialize();

        _dnaQuery = GetEntityQuery<DnaComponent>();

        InitializeGenerated();

        // Use EntityManager's global ComponentRemoved event for removal tracking.
        // We can't use directed SubscribeLocalEvent<TComp, ComponentRemove> because
        // Robust only allows one handler per (component, event) pair, and some genetic
        // components already have ComponentRemove handlers in other systems.
        EntityManager.ComponentRemoved += OnAnyComponentRemoved;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        EntityManager.ComponentRemoved -= OnAnyComponentRemoved;
    }

    private void OnAnyComponentRemoved(RemovedComponentEventArgs args)
    {
        if (args.Terminating)
            return;

        var compType = args.BaseArgs.Component.GetType();
        if (GeneticComponents.ContainsKey(compType))
            OnGeneticComponentRemoved(args.BaseArgs.Owner, compType);
    }

    /// <summary>
    /// Compute the total DNA length and non-coding padding from the registered genetic components.
    /// Must be called after all InitializeGenerated() calls have populated GeneticComponents.
    /// </summary>
    protected void ComputeDnaLength()
    {
        var codingLength = 0;
        foreach (var info in GeneticComponents.Values)
        {
            codingLength += info.Complexity + info.Stability + info.VariableCodonCount;
        }

        DnaLength = (int) MathF.Ceiling(codingLength * GeneticsStabilityFactor);
        NonCodingLength = DnaLength - codingLength;
    }

    /// <summary>
    /// Build (or rebuild) the per-round gene position map.
    /// Shuffles gene order and assigns random canonical sequences.
    /// </summary>
    protected void SetupRoundRecords()
    {
        CurrentRoundRecords.Clear();
        CurrentRoundIndexToType.Clear();

        var components = new List<GeneticComponentInfo>(GeneticComponents.Values);

        // Shuffle component order so gene positions vary each round
        for (var i = components.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (components[i], components[j]) = (components[j], components[i]);
        }

        // Randomly distribute non-coding gaps between, before, and after gene regions
        var gaps = new int[components.Count + 1];
        for (var i = 0; i < NonCodingLength; i++)
            gaps[_random.Next(gaps.Length)]++;

        // Walk the DNA, placing each gene in its region with a random canonical sequence
        var currentIndex = gaps[0];
        for (var g = 0; g < components.Count; g++)
        {
            var info = components[g];
            var geneLength = info.Complexity + info.Stability + info.VariableCodonCount;

            // Generate a random canonical sequence — the "most stable encoding"
            // of this gene for this round.
            var sequenceChars = new char[geneLength];
            for (var i = 0; i < geneLength; i++)
                sequenceChars[i] = Nucleotides[_random.Next(Nucleotides.Length)];

            var record = new RoundGeneticRecord(
                info,
                new string(sequenceChars),
                currentIndex);

            CurrentRoundRecords[info.ComponentType] = record;

            for (var i = 0; i < geneLength; i++)
                CurrentRoundIndexToType[currentIndex + i] = record;

            currentIndex += geneLength + gaps[g + 1];
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Gene matching helpers
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Count the number of mismatched codons between the DNA at the gene's
    /// existence region and the canonical sequence.
    /// </summary>
    protected static int CountMismatches(ReadOnlySpan<char> dna, RoundGeneticRecord record)
    {
        var mismatches = 0;
        for (var i = 0; i < record.ExistenceLength; i++)
        {
            if (dna[record.StartIndex + i] != record.CanonicalSequence[i])
                mismatches++;
        }
        return mismatches;
    }

    /// <summary>
    /// Returns true if the gene's existence region in the DNA matches the canonical
    /// sequence within the gene's Stability tolerance (Hamming distance).
    /// </summary>
    protected static bool CheckGeneMatch(ReadOnlySpan<char> dna, RoundGeneticRecord record)
    {
        return CountMismatches(dna, record) <= record.Gene.Stability;
    }

    /// <summary>
    /// Count how many codons in a variable's sub-region match the canonical sequence.
    /// </summary>
    /// <param name="dna">The full DNA string.</param>
    /// <param name="record">The gene record for the component.</param>
    /// <param name="variableOffset">Offset of this variable within the gene block (after existence codons).</param>
    /// <param name="variableCodonCount">Number of codons this variable uses.</param>
    protected static int CountVariableMatches(
        ReadOnlySpan<char> dna,
        RoundGeneticRecord record,
        int variableOffset,
        int variableCodonCount)
    {
        var matches = 0;
        var start = record.StartIndex + record.ExistenceLength + variableOffset;
        var canonStart = record.ExistenceLength + variableOffset;
        for (var i = 0; i < variableCodonCount; i++)
        {
            if (dna[start + i] == record.CanonicalSequence[canonStart + i])
                matches++;
        }
        return matches;
    }

    /// <summary>
    /// Write exactly <paramref name="targetMatches"/> canonical codons into a variable's
    /// sub-region, scrambling the rest. Codons are matched from the start of the region.
    /// </summary>
    protected void WriteVariableMatches(
        char[] chars,
        RoundGeneticRecord record,
        int variableOffset,
        int variableCodonCount,
        int targetMatches)
    {
        var start = record.StartIndex + record.ExistenceLength + variableOffset;
        var canonStart = record.ExistenceLength + variableOffset;
        targetMatches = Math.Clamp(targetMatches, 0, variableCodonCount);

        for (var i = 0; i < variableCodonCount; i++)
        {
            if (i < targetMatches)
            {
                chars[start + i] = record.CanonicalSequence[canonStart + i];
            }
            else
            {
                var canonical = record.CanonicalSequence[canonStart + i];
                char newChar;
                do
                {
                    newChar = Nucleotides[_random.Next(Nucleotides.Length)];
                } while (newChar == canonical);
                chars[start + i] = newChar;
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Reconciliation — sync component state to DNA
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Check whether a single gene's DNA region matches its canonical sequence
    /// and add or remove the corresponding component as necessary.
    /// </summary>
    protected void ReconcileGene(EntityUid uid, ReadOnlySpan<char> dna, RoundGeneticRecord record)
    {
        var matches = CheckGeneMatch(dna, record);
        var has = EntityManager.HasComponent(uid, record.Gene.ComponentType);

        if (matches && !has)
        {
            var comp = _compFactory.GetComponent(record.Gene.ComponentType);
            _reconcilingDna = true;
            try
            {
                EntityManager.AddComponent(uid, comp);
            }
            finally
            {
                _reconcilingDna = false;
            }

            // Apply variable values from DNA to the newly-added component
            if (VariableSyncReadDna.TryGetValue(record.Gene.ComponentType, out var syncRead))
                syncRead(uid, dna.ToString(), record);
        }
        else if (!matches && has)
        {
            _reconcilingDna = true;
            try
            {
                EntityManager.RemoveComponent(uid, record.Gene.ComponentType);
            }
            finally
            {
                _reconcilingDna = false;
            }
        }
        else if (matches && has)
        {
            // Component still present — but variable codons may have changed, so resync values
            if (VariableSyncReadDna.TryGetValue(record.Gene.ComponentType, out var syncRead))
                syncRead(uid, dna.ToString(), record);
        }
    }

    /// <summary>
    /// Check every gene region against the DNA and add/remove components to match.
    /// </summary>
    protected void ReconcileAllGenes(EntityUid uid, ReadOnlySpan<char> dna)
    {
        foreach (var record in CurrentRoundRecords.Values)
            ReconcileGene(uid, dna, record);
    }

    /// <summary>
    /// Reconcile only the genes whose regions overlap with the modified span
    /// <c>[startIndex, startIndex + length)</c>.
    /// </summary>
    protected void ReconcileOverlappingGenes(EntityUid uid, ReadOnlySpan<char> dna, int startIndex, int length)
    {
        var endIndex = startIndex + length;
        foreach (var record in CurrentRoundRecords.Values)
        {
            var geneEnd = record.StartIndex + record.Length;
            if (record.StartIndex < endIndex && geneEnd > startIndex)
                ReconcileGene(uid, dna, record);
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Component lifecycle handlers (called by generated code / global event)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by generated code when a genetic component is added to an entity.
    /// Writes the canonical gene sequence into the entity's DNA.
    /// </summary>
    protected void OnGeneticComponentAdded(EntityUid uid, Type componentType)
    {
        if (_reconcilingDna)
            return;

        if (!TryGetDnaForUpdate(uid, out var dnaComp, out var dna))
            return;

        if (!CurrentRoundRecords.TryGetValue(componentType, out var record))
        {
            Log.Error($"Entity {ToPrettyString(uid)} had a genetic component of type {componentType.Name} added, but no gene record was found for it in CurrentRoundRecords.");
            return;
        }

        if (dnaComp.DNA == null)
        {
            Log.Error($"Entity {ToPrettyString(uid)} has an initialized DnaComponent, but the DNA on it is null.");
            return;
        }

        var chars = dna.ToCharArray();

        // Write the canonical sequence for the existence region only
        for (var i = 0; i < record.ExistenceLength; i++)
            chars[record.StartIndex + i] = record.CanonicalSequence[i];

        // For variable codons, encode the component's current field values
        if (VariableSyncWriteDna.TryGetValue(componentType, out var syncWrite))
            syncWrite(uid, chars, record);
        else
        {
            // No variable fields — write canonical for any variable codons too
            for (var i = record.ExistenceLength; i < record.Length; i++)
                chars[record.StartIndex + i] = record.CanonicalSequence[i];
        }

        dnaComp.DNA = new string(chars);
        Dirty(uid, dnaComp);
    }

    /// <summary>
    /// Called by generated code when a genetic component is removed from an entity.
    /// Scrambles the gene region in the entity's DNA so it no longer matches.
    /// </summary>
    protected void OnGeneticComponentRemoved(EntityUid uid, Type componentType)
    {
        if (_reconcilingDna)
            return;

        if (!TryGetDnaForUpdate(uid, out var dnaComp, out var dna))
            return;

        if (!CurrentRoundRecords.TryGetValue(componentType, out var record))
        {
            Log.Error($"Entity {ToPrettyString(uid)} had a genetic component of type {componentType.Name} added, but no gene record was found for it in CurrentRoundRecords.");
            return;
        }

        var chars = dna.ToCharArray();

        // Scramble every codon to differ from canonical, maximizing the Hamming distance
        for (var i = 0; i < record.Length; i++)
        {
            var canonical = record.CanonicalSequence[i];
            char newChar;
            do
            {
                newChar = Nucleotides[_random.Next(Nucleotides.Length)];
            } while (newChar == canonical);
            chars[record.StartIndex + i] = newChar;
        }

        dnaComp.DNA = new string(chars);
        Dirty(uid, dnaComp);
    }

    /// <summary>
    /// Performs standard checks to see if we can do DNA updates, and returns the dna component if so.
    /// </summary>
    protected bool TryGetDnaForUpdate(EntityUid uid, [NotNullWhen(true)] out DnaComponent? dnaComp, [NotNullWhen(true)] out string? dna)
    {
        dna = null;
        // We skip over doing updates before a dna component is added/initialized; this gets around component
        // initialization order issues where a genetic component might be added before the DnaComponent is added.
        if (!_dnaQuery.TryGetComponent(uid, out dnaComp) || !dnaComp.Initialized)
        {
            return false;
        }

        if (dnaComp.DNA == null)
        {
            Log.Error($"Entity {ToPrettyString(uid)} has an initialized DnaComponent, but the DNA on it is null.");
            return false;
        }

        dna = dnaComp.DNA;
        return true;
    }
}
