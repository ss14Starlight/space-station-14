using System.Diagnostics.CodeAnalysis;
using Content.Shared._NullLink;
using Content.Shared.Localizations;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Roles.Jobs;
using Content.Shared.Starlight;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Roles;

[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class RoleTimeRequirement : JobRequirement
{
    /// <summary>
    /// What particular role they need the time requirement with.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<PlayTimeTrackerPrototype> Role;

    /// <inheritdoc cref="DepartmentTimeRequirement.Time"/>
    [DataField(required: true)]
    public TimeSpan Time;

    public override bool Check(IEntityManager entManager,
        ICommonSession? player,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan>? playTimes,
        out FormattedMessage details)
    {
        details = new FormattedMessage();

        // If playTimes is null, we're not going to check against playtime requirements
        if (playTimes == null)
            return true;

        string proto = Role;
        //NullLink start
        var bypass = player is not null &&
                     IoCManager.Resolve<ISharedNullLinkPlayerRolesReqManager>().IsAllRolesAvailable(player);
        //NullLink end

        playTimes.TryGetValue(proto, out var roleTime);
        var roleDiffSpan = Time - roleTime;
        var roleDiff = roleDiffSpan.TotalMinutes;
        var formattedCurrent = ContentLocalizationManager.FormatPlaytime(roleTime);
        var formattedRequired = ContentLocalizationManager.FormatPlaytime(Time);
        var departmentColor = Color.Yellow;

        if (!entManager.EntitySysManager.TryGetEntitySystem(out SharedJobSystem? jobSystem))
            return false;

        var jobProto = jobSystem.GetJobPrototype(proto);

        // Starlight start
        // Handle non-job role time requirements
        if (jobProto is null)
        {
            if (!protoManager.TryIndex<PlayTimeTrackerPrototype>(proto, out var tracker))
                return false;

            if (!Inverted)
            {
                if (roleDiff <= 0)
                    return true;

                details = FormattedMessage.FromMarkupPermissive(Loc.GetString(
                    "role-timer-role-insufficient",
                    ("current", formattedCurrent),
                    ("required", formattedRequired),
                    ("job", tracker.LocalizedName),
                    ("departmentColor", departmentColor.ToHex())));
                return bypass;
            }
            else
            {
                if (roleDiff <= 0)
                {
                    details = FormattedMessage.FromMarkupPermissive(Loc.GetString(
                        "role-timer-role-too-high",
                        ("current", formattedCurrent),
                        ("required", formattedRequired),
                        ("job", tracker.LocalizedName),
                        ("departmentColor", departmentColor.ToHex())));
                    return bypass;
                }
                return true;
            }
        }
        // Starlight end

        if (jobSystem.TryGetDepartment(jobProto, out var departmentProto))
            departmentColor = departmentProto.Color;

        if (!protoManager.TryIndex<JobPrototype>(jobProto, out var indexedJob))
            return bypass;

        details = FormattedMessage.FromMarkupPermissive(Loc.GetString(
            Inverted ? "role-timer-not-too-high" : "role-timer-role-sufficient",
            ("current", formattedCurrent),
            ("required", formattedRequired),
            ("job", Loc.GetString(proto)),
            ("departmentColor", departmentColor.ToHex())));

        if (!Inverted)
        {
            if (roleDiff <= 0)
                return true;

            details = FormattedMessage.FromMarkupPermissive(Loc.GetString(
                "role-timer-role-insufficient",
                ("current", formattedCurrent),
                ("required", formattedRequired),
                ("job", indexedJob.LocalizedName),
                ("departmentColor", departmentColor.ToHex())));
            return bypass;
        }

        if (roleDiff <= 0)
        {
            details = FormattedMessage.FromMarkupPermissive(Loc.GetString(
                "role-timer-role-too-high",
                ("current", formattedCurrent),
                ("required", formattedRequired),
                ("job", indexedJob.LocalizedName),
                ("departmentColor", departmentColor.ToHex())));
            return bypass;
        }

        return true;
    }
}
