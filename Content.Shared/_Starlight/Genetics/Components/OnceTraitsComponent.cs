using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Genetics.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OnceTraitsComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public Dictionary<ProtoId<OnceTraitPrototype>, FixedPoint2> Traits = new();
}
