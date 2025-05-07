using Content.Server._Moffstation.Objectives.Systems;

namespace Content.Server._Moffstation.Objectives.Components;

[RegisterComponent, Access(typeof(TargetDepartmentSystem))]
public sealed partial class TargetDepartmentComponent : Component
{

    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public string Title = string.Empty;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string? Target;
}
