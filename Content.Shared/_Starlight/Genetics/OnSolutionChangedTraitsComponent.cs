using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OnSolutionChangedTraitsComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Dictionary<OnSolutionChangedTraitPrototype, float> Traits = new();
}