using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.Objectives.Components;

[RegisterComponent]
public sealed partial class LocationObjectiveComponent : Component
{

    [DataField(required: true)]
    public string Title = string.Empty;

    [DataField]
    public string? Target;
}
