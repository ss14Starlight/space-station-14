using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Genetics.Genes.Components;

/// <summary>
/// A collection of traits along with associated metadata, like the name.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IndividualGeneComponent : Component
{
    /// <summary>
    /// The traits influenced by this gene.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public TraitDict Traits = new TraitDict();

    /// <summary>
    /// The unchanging "technical name" of a gene, i.e. PRKN (the name of a gene that creates the Parkin protein, mutations in which can cause parkinsons, hence the name).
    /// Can be procedurally generated.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public string TechnicalName = string.Empty;

    /// <summary>
    /// The informal name set by players and/or history.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public string? Name;

    /// <summary>
    /// If non-null, the gene prototype this individual gene refers to.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public ProtoId<SampleGenePrototype>? Prototype = null;
}
