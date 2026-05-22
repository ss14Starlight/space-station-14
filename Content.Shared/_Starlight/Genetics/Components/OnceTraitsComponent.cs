using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OnceTraitsComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Dictionary<ProtoId<OnceTraitPrototype>, FixedPoint2> Traits = new();
}
