using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Objectives.Components;

/// <summary>
/// Possess several named crew ID cards from one populated department.
/// </summary>
[RegisterComponent]
public sealed partial class DepartmentIdStealConditionComponent : Component
{
    [DataField] public int MinCount = 3;
    [DataField] public int MaxCount = 5;
    [ViewVariables] public int Count;
    [ViewVariables] public ProtoId<DepartmentPrototype>? Department;
    [ViewVariables] public string? OwnName;
}
