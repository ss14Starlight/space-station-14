using Robust.Shared.Prototypes;

namespace Content.Server._Daiquiri.Xeno.Components;

[RegisterComponent]
public sealed partial class AlienInfectedSpawnerComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Prototype = default!;
}
