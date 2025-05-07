namespace Content.Server._Moffstation.Objectives.Components;

[RegisterComponent]
public sealed partial class TargetDepartmentOverrideComponent : Component
{
    [DataField]
    public string? Target;
}
