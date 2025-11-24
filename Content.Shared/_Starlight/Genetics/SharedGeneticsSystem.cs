using System.Diagnostics.CodeAnalysis;
using Content.Shared.Forensics.Components;
using Robust.Shared.GameObjects;
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

    /// <summary>
    /// Metadata about a component that participates in the genetics system.
    /// </summary>
    protected sealed record GeneticComponentInfo(Type ComponentType, int Complexity, int Stability);

    /// <summary>
    /// Maps a component type to its per-round DNA region and canonical sequence.
    /// </summary>
    protected sealed record RoundGeneticRecord(
        GeneticComponentInfo Gene,
        string CanonicalSequence,
        int StartIndex)
    {
        public int Length => Gene.Complexity + Gene.Stability;
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
            codingLength += info.Complexity + info.Stability;
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
            var geneLength = info.Complexity + info.Stability;

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

    /// <summary>
    /// Called by generated code when a genetic component is added to an entity.
    /// Writes the canonical gene sequence into the entity's DNA.
    /// </summary>
    protected void OnGeneticComponentAdded(EntityUid uid, Type componentType)
    {
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

        // Write the canonical sequence at the gene's position
        for (var i = 0; i < record.Length; i++)
            chars[record.StartIndex + i] = record.CanonicalSequence[i];

        dnaComp.DNA = new string(chars);
        Dirty(uid, dnaComp);
    }

    /// <summary>
    /// Called by generated code when a genetic component is removed from an entity.
    /// Scrambles the gene region in the entity's DNA so it no longer matches.
    /// </summary>
    protected void OnGeneticComponentRemoved(EntityUid uid, Type componentType)
    {
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
