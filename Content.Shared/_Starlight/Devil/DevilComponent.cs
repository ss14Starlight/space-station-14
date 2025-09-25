using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Devil;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class DevilComponent : Component
{
    public readonly List<ProtoId<EntityPrototype>> BaseActions = new()
    {
        "ActionSummonDemonicContract"
    };

    [AutoNetworkedField]
    public string TrueName = "Hellish McEvil";
}