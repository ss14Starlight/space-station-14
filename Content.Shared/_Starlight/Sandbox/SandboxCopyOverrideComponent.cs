using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Sandbox;

[RegisterComponent]
public sealed partial class SandboxCopyOverrideComponent : Component
{
    [DataField(required: true), ViewVariables(VVAccess.ReadOnly)]
    public EntProtoId Override;
}
