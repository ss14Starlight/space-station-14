using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GenesComponent : Component
{
    /// <summary>
    /// The list of genes that this entity currently has.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<Gene> Genes = new();

    /// <summary>
    /// The traits that can be pulled from to make a gene.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<AbstractTrait> AvailableTraits = new();
}
