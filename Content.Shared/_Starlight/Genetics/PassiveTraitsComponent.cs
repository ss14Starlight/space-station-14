using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PassiveTraitsComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Dictionary<PassiveTraitPrototype, (FixedPoint2, TimeSpan)> Traits = new();
}
