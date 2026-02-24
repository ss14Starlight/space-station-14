using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Xenobiology.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GeneticSlimeComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public List<Gene> Genes = new();

    [ViewVariables, AutoNetworkedField]
    public TraitDict Traits = new();
    
    [ViewVariables, AutoNetworkedField]
    public Dictionary<PassiveSlimeTraitPrototype, TimeSpan> PassiveTraits = new();
}