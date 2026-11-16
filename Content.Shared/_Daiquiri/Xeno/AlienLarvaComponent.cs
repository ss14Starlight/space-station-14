using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Daiquiri.Xeno;

[RegisterComponent, NetworkedComponent]
public sealed partial class AlienLarvaComponent : Component
{
    [DataField(required: true)]
    public EntProtoId LarvaPrototype = default!;

    [DataField]
    public bool TransferMind = true;
}
