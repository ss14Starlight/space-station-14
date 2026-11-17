using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Blimpuf.Contraband;

/// <summary>
/// Marks an entity as contraband and records its legal tier, type, and authorized users.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(ContrabandSystem)), AutoGenerateComponentState]
public sealed partial class ContrabandComponent : Component
{
    /// <summary>
    /// The legal severity tier of this contraband.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public ProtoId<ContrabandTierPrototype> Tier = "Tier1";

    /// <summary>
    /// An optional origin or category, such as Syndicate or magical contraband.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public ProtoId<ContrabandTypePrototype>? ContrabandType;

    /// <summary>
    /// Departments authorized to possess this contraband.
    /// Child prototype entries are added to inherited entries instead of replacing them.
    /// </summary>
    [DataField]
    [AlwaysPushInheritance]
    [AutoNetworkedField]
    public HashSet<ProtoId<DepartmentPrototype>> AllowedDepartments = new();

    /// <summary>
    /// Jobs authorized to possess this contraband in addition to the allowed departments.
    /// Child prototype entries are added to inherited entries instead of replacing them.
    /// </summary>
    [DataField]
    [AlwaysPushInheritance]
    [AutoNetworkedField]
    public HashSet<ProtoId<JobPrototype>> AllowedJobs = new();
}
