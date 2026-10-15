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
    /// Container for the gene pool.
    /// </summary>
    [ViewVariables] public Container GenePoolContainer = null!;

    /// <summary>
    /// Container for the input pool.
    /// </summary>
    [ViewVariables] public Container InputGenesContainer = null!;

    /// <summary>
    /// All genes currently present in the console, irrespective of the pool container it is currently in.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public readonly HashSet<EntityUid> AllGenes = [];

    public const string GenePoolContainerId = "gene_pool";
    public const string InputGenesContainerId = "input_genes";

    /// <summary>
    /// Sorting type index of the gene pool.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public int GenePoolSortIndex;

    /// <summary>
    /// Sorting type index of the input pool.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public int InputGenesSortIndex;

    /// <summary>
    /// Genes that will be populated on component startup.
    /// </summary>
    [DataField] public HashSet<ProtoId<SampleGenePrototype>> DefaultGenes = [];

    /// <summary>
    /// The entity prototype to spawn when dispensing a sampler.
    /// </summary>
    [DataField, AutoNetworkedField] public EntProtoId SamplerPrototype;

    /// <summary>
    /// Popup for dispensing sampler.
    /// </summary>
    [DataField, AutoNetworkedField] public string? SamplerDispensePopup;

    /// <summary>
    /// Popup for accepting sampler with gene data.
    /// </summary>
    [DataField, AutoNetworkedField] public string? SamplerInsertPopup;
}
