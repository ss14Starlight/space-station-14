using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Deathmatch;

[RegisterComponent, NetworkedComponent]
public sealed partial class DeathmatchComponent : Component
{
    [DataField]
    public List<ProtoId<EntityPrototype>> BaseDeathmatchActions = new()
    {
        "ActionCreateRobustToolbox"
    };
}
