using Content.Shared._Starlight.Genetics.Genes.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Genetics.Genes.Components;

/// <summary>
/// A collection of traits along with associated metadata, like the name.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IndividualGeneComponent : Component
{
    /// <summary>
    /// The maximum length of a player-set <see cref="Name"/>.
    /// </summary>
    public const int MaxNameLength = 20;

    /// <summary>
    /// The traits influenced by this gene.
    /// </summary>
    [DataField, AutoNetworkedField] public TraitDict Traits = new TraitDict();

    /// <summary>
    /// The unchanging "technical name" of a gene, i.e. PRKN (the name of a gene that creates the Parkin protein, mutations in which can cause parkinsons, hence the name).
    /// Can be procedurally generated.
    /// </summary>
    [DataField, AutoNetworkedField] public string TechnicalName = string.Empty;

    /// <summary>
    /// The informal name set by players and/or history.
    /// </summary>
    [DataField, AutoNetworkedField] public string? Name;

    /// <summary>
    /// The DNA sequence for this gene. Randomly generated if null.
    /// Should be roughly 16 characters, consisting of A, C, T, and G.
    /// </summary>
    [DataField, AutoNetworkedField] public string? Sequence;

    /// <summary>
    /// If non-null, the gene prototype this individual gene refers to.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public ProtoId<SampleGenePrototype>? Prototype = null;

    /// <summary>
    /// Genes that are ancestors of this gene. Tracked via technical name since gene entities can be cloned.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public HashSet<string> AncestorGenes = [];
}
