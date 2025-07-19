using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.Objectives.Components;

[RegisterComponent]
public sealed partial class PickDepartmentObjectiveComponent : Component
{
    [DataField]
    public HashSet<ProtoId<DepartmentPrototype>> DepartmentBlacklist = new();

    [DataField]
    public HashSet<ProtoId<DepartmentPrototype>> DepartmentWhitelist = new();

    /// <summary>
    /// Whether the department the person is currently working is able to be selected
    /// </summary>
    [DataField]
    public bool AllowSameDepartment;

    [DataField]
    public bool AllowNonPrimary;

    [DataField]
    public bool AllowHidden;
}
