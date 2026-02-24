using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OnSolutionChangedTraitsComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Dictionary<OnSolutionChangedTraitPrototype, FixedPoint2> Traits = new();
}
