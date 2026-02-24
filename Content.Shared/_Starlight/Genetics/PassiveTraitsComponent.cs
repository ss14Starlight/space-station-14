using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PassiveTraitsComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Dictionary<PassiveTraitPrototype, (float, TimeSpan)> Traits = new();
}