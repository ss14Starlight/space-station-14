using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OnceTraitsComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Dictionary<OnceTraitPrototype, float> Traits = new();
}