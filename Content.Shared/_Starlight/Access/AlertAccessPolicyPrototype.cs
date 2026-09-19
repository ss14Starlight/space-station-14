using Content.Shared.Access;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Starlight.Access;

/// <summary>
/// Defines, per alert level, which temporary access grants are applied to ID cards
/// while the station is at that level. Grants can be level-wide (apply to every ID card
/// in a PDA on the station), department-specific, or job-specific, and stack together.
/// </summary>
[Prototype]
public sealed partial class AlertAccessPolicyPrototype : IPrototype
{
    /// <summary>
    /// The prototype ID for this policy, referenced from <c>AlertLevelSystem</c>
    /// (currently hardcoded to "StationAlertAccessPolicy").
    /// </summary>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Maps each alert level ID (e.g. "orange", "red") to the access grants that apply
    /// while the station is at that level.
    /// </summary>
    [DataField("levels", required: true)]
    public Dictionary<string, AlertAccessLevelData> Levels = new();

    /// <summary>
    /// The set of temporary access grants for a single alert level.
    /// </summary>
    [DataDefinition]
    public sealed partial class AlertAccessLevelData
    {
        /// <summary>
        /// Access levels granted to every ID card in a PDA on the
        /// station while it is at this alert level, regardless of job or department.
        /// </summary>
        [DataField("tempAccess")]
        public List<ProtoId<AccessLevelPrototype>> TempAccess = new();

        /// <summary>
        /// Access groups whose levels are granted to every ID card in a PDA on the
        /// station while it is at this alert level, regardless of job or department.
        /// </summary>
        [DataField("tempAccessGroups")]
        public List<ProtoId<AccessGroupPrototype>> TempAccessGroups = new();

        /// <summary>
        /// Additional access grants that apply only to ID cards belonging to specific jobs
        /// at this alert level, on top of any level-wide <see cref="TempAccess"/> grants.
        /// </summary>
        [DataField("jobSpecific")]
        public List<AlertAccessJobSpecificData> JobSpecific = new();

        /// <summary>
        /// Additional access grants that apply only to ID cards whose job belongs to a
        /// specific department at this alert level, on top of any level-wide
        /// <see cref="TempAccess"/> grants.
        /// </summary>
        [DataField("departmentSpecific")]
        public List<AlertAccessDepartmentSpecificData> DepartmentSpecific = new();
    }

    /// <summary>
    /// A job-specific temporary access grant for a single alert level.
    /// </summary>
    [DataDefinition]
    public sealed partial class AlertAccessJobSpecificData
    {
        /// <summary>
        /// The job this grant applies to.
        /// </summary>
        [DataField("job", required: true)]
        public ProtoId<JobPrototype> Job = default!;

        /// <summary>
        /// Access levels granted to ID cards with the above <see cref="Job"/>.
        /// </summary>
        [DataField("tempAccess")]
        public List<ProtoId<AccessLevelPrototype>> TempAccess = new();

        /// <summary>
        /// Access groups whose levels are granted to ID cards with the above <see cref="Job"/>.
        /// </summary>
        [DataField("tempAccessGroups")]
        public List<ProtoId<AccessGroupPrototype>> TempAccessGroups = new();
    }

    /// <summary>
    /// A department-specific temporary access grant for a single alert level.
    /// </summary>
    [DataDefinition]
    public sealed partial class AlertAccessDepartmentSpecificData
    {
        /// <summary>
        /// The <c>DepartmentPrototype</c> ID this grant applies to; membership is checked
        /// via that department's job roster against the ID card's resolved job.
        /// </summary>
        [DataField("department", required: true)]
        public ProtoId<DepartmentPrototype> Department = default!;

        /// <summary>
        /// Access levels granted to ID cards whose job belongs to the
        /// above <see cref="Department"/>.
        /// </summary>
        [DataField("tempAccess")]
        public List<ProtoId<AccessLevelPrototype>> TempAccess = new();

        /// <summary>
        /// Access groups whose levels are granted to ID cards whose job belongs to the
        /// above <see cref="Department"/>.
        /// </summary>
        [DataField("tempAccessGroups")]
        public List<ProtoId<AccessGroupPrototype>> TempAccessGroups = new();
    }
}
