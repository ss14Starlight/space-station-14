using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Genetics.Genes.Prototypes;

/// <summary>
/// Describes genes that are not procedurally generated during the round.
/// </summary>
[Prototype("sampleGene")]
[DataDefinition]
public sealed partial class SampleGenePrototype : IPrototype
{
    [ViewVariables, IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The traits influenced by this gene.
    /// </summary>
    [ViewVariables, DataField(required: true)]
    public TraitDict Traits = new TraitDict();

    /// <summary>
    /// The unchanging "technical name" of a gene, i.e. PRKN (the name of a gene that creates the Parkin protein, mutations in which can cause parkinsons, hence the name).
    /// Can be procedurally generated.
    /// </summary>
    [ViewVariables, DataField(required: true)]
    public string TechnicalName = string.Empty;

    /// <summary>
    /// The informal name set by players and/or history.
    /// </summary>
    [ViewVariables, DataField(required: true)]
    public string? Name = string.Empty;
}
