using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Objectives.Components;

[RegisterComponent]
public sealed partial class DepartmentObjectiveComponent : Component
{
    /// <summary>
    /// Locale id for the objective title.
    /// It is passed a "department" argument.
    /// </summary>
    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public LocId Title = string.Empty;

    /// <summary>
    /// ProtoID of Target Department
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<DepartmentPrototype>? TargetDepartment;

    [DataField]
    public SpriteSpecifier Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Devices/goldwatch.rsi"), "goldwatch");
}
