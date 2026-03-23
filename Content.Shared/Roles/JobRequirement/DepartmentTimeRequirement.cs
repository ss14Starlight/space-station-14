using System.Diagnostics.CodeAnalysis;
using Content.Shared._NullLink;
using Content.Shared.Localizations;
using Content.Shared.Preferences;
using Content.Shared.Starlight;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Roles;

[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class DepartmentTimeRequirement : JobRequirement
{
    /// <summary>
    /// Which department needs the required amount of time.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<DepartmentPrototype> Department;

    /// <summary>
    /// How long (in seconds) this requirement is.
    /// </summary>
    [DataField(required: true)]
    public TimeSpan Time;

    public override bool Check(IEntityManager entManager,
        ICommonSession? player,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan>? playTimes,
        out FormattedMessage reason) // Starlight: Always return reason
    {
        reason = new FormattedMessage();

        // If playTimes is null, we're not going to check against playtime requirements
        if (playTimes == null)
            return true;

        var playtime = TimeSpan.Zero;

        //NullLink start
        var bypass = player is not null &&
                     IoCManager.Resolve<ISharedNullLinkPlayerRolesReqManager>().IsAllRolesAvailable(player);
        //NullLink end

        // Check all jobs' departments
        var department = protoManager.Index(Department);
        var jobs = department.Roles;
        string proto;

        // Check all jobs' playtime
        foreach (var other in jobs)
        {
            // The schema is stored on the Job role but we want to explode if the timer isn't found anyway.
            proto = protoManager.Index(other).PlayTimeTracker;

            playTimes.TryGetValue(proto, out var otherTime);
            playtime += otherTime;
        }

        var deptDiffSpan = Time - playtime;
        var deptDiff = deptDiffSpan.TotalMinutes;
        var formattedCurrent = ContentLocalizationManager.FormatPlaytime(playtime); // Starlight
        var formattedRequired = ContentLocalizationManager.FormatPlaytime(Time); // Starlight
        var nameDepartment = "role-timer-department-unknown";

        if (protoManager.Resolve(Department, out var departmentIndexed))
        {
            nameDepartment = departmentIndexed.Name;
        }

        // Starlight BEGIN: Default reason is success
        reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
            Inverted ? "role-timer-department-not-too-high" : "role-timer-department-sufficient",
            ("current", formattedCurrent),
            ("required", formattedRequired),
            ("department", Loc.GetString(nameDepartment)),
            ("departmentColor", department.Color.ToHex())));
        // Starlight END

        if (!Inverted)
        {
            if (deptDiff <= 0)
                return true;

            reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
                "role-timer-department-insufficient",
                ("current", formattedCurrent), // Starlight
                ("required", formattedRequired), // Starlight
                ("department", Loc.GetString(nameDepartment)),
                ("departmentColor", department.Color.ToHex())));
            return bypass; // NullLink
        }

        if (deptDiff <= 0)
        {
            reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
                "role-timer-department-too-high",
                ("current", formattedCurrent), // Starlight
                ("required", formattedRequired), // Starlight
                ("department", Loc.GetString(nameDepartment)),
                ("departmentColor", department.Color.ToHex())));
            return bypass; // NullLink
        }

        return true;
    }
}
