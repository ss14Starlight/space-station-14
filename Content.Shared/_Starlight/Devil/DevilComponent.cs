using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Devil;

namespace Content.Shared._Starlight.Devil;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class DevilComponent : Component
{
    public readonly List<ProtoId<EntityPrototype>> BaseActions = new()
    {
        "ActionSummonDemonicContract"
    };

    public List<ProtoId<DamnationPrototype>> AvailableDamnations = new()
    {
        "Cluwnification"
    };

    [AutoNetworkedField]
    public string TrueName = "Hellish McEvil";
}