using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GenesComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public List<Gene> Genes = new();
}