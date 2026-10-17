// ReSharper disable CheckNamespace
using Content.Server.Spawners.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Spawners.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Spawners.Components;

public sealed partial class ContainerSpawnPointComponent

{
    /// <summary>
    /// An optional department specifier
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<DepartmentPrototype>? Department;
}
