using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Genetics.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PassiveTraitsComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Dictionary<ProtoId<PassiveTraitPrototype>, FixedPoint2> Traits = new();

    [ViewVariables, AutoNetworkedField]
    public Dictionary<ProtoId<PassiveTraitPrototype>, TimeSpan> Cooldowns = new();

    [AutoNetworkedField]
    public bool Paused = false;
}
