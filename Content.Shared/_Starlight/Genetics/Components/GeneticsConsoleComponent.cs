using Content.Shared._Starlight.Genetics.Genes.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Genetics.Components;

/// <summary>
/// A collection of traits known to the genetics console, along with an ordering players can adjust.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GeneticsConsoleComponent : Component
{
    /// <summary>
    /// The known genes available at this console, ordered from top to bottom.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public readonly List<EntityUid> Genes = [];

    public const string GenePoolContainerId = "gene_pool";

    [ViewVariables] public Container GenePoolContainer = null!;

    public const string InputGenesContainerId = "input_genes";

    [ViewVariables] public Container InputGenesContainer = null!;

    [ViewVariables] public int GenePoolSortIndex;
    [ViewVariables] public int InputGenesSortIndex;

    /// <summary>
    /// Genes that will be populated on component startup.
    /// </summary>
    [DataField] public HashSet<ProtoId<SampleGenePrototype>> DefaultGenes = [];
}
