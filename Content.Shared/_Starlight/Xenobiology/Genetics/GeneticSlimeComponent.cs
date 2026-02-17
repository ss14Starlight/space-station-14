using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Xenobiology.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GeneticSlimeComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public TraitDict Genes = new();
}