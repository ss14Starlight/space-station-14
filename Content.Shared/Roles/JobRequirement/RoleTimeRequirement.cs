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
        out FormattedMessage reason)
    {
        reason = new FormattedMessage();

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
        var formattedCurrent = ContentLocalizationManager.FormatPlaytime(roleTime); // Starlight
        var formattedRequired = ContentLocalizationManager.FormatPlaytime(Time); // Starlight
        var departmentColor = Color.Yellow;

        if (!entManager.EntitySysManager.TryGetEntitySystem(out SharedJobSystem? jobSystem))
            return false;

        var jobProto = jobSystem.GetJobPrototype(proto);

        // Starlight start
        // Handle non-job role time requirements
        reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
            Inverted ? "role-timer-not-too-high" : "role-timer-role-sufficient",
            ("current", formattedCurrent),
            ("required", formattedRequired),
            ("job", Loc.GetString(proto)),
            ("departmentColor", departmentColor.ToHex())));
        
        if (jobProto is null)
        {
            if (!protoManager.TryIndex<PlayTimeTrackerPrototype>(proto, out var tracker))
                return false;

            if (!Inverted)
            {
                if (roleDiff <= 0)
                    return true;

                reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
                    "role-timer-role-insufficient",
                    ("current", formattedCurrent), // Starlight
                    ("required", formattedRequired), // Starlight
                    ("job", tracker.LocalizedName),
                    ("departmentColor", departmentColor.ToHex())));
                return bypass; // NullLink
            }
            else
            {
                if (roleDiff <= 0)
                {
                    reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
                        "role-timer-role-too-high",
                        ("current", formattedCurrent), // Starlight
                        ("required", formattedRequired), // Starlight
                        ("job", tracker.LocalizedName),
                        ("departmentColor", departmentColor.ToHex())));
                    return bypass; // NullLink
                }
                return true;
            }
        }
        // Starlight end

        if (jobSystem.TryGetDepartment(jobProto, out var departmentProto))
            departmentColor = departmentProto.Color;

        if (!protoManager.TryIndex<JobPrototype>(jobProto, out var indexedJob))
            return bypass; // NullLink

        if (!Inverted)
        {
            if (roleDiff <= 0)
                return true;

            reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
                "role-timer-role-insufficient",
                ("current", formattedCurrent), // Starlight
                ("required", formattedRequired), // Starlight
                ("job", indexedJob.LocalizedName),
                ("departmentColor", departmentColor.ToHex())));
            return bypass; // NullLink
        }

        if (roleDiff <= 0)
        {
            reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
                "role-timer-role-too-high",
                ("current", formattedCurrent), // Starlight
                ("required", formattedRequired), // Starlight
                ("job", indexedJob.LocalizedName),
                ("departmentColor", departmentColor.ToHex())));
            return bypass; // NullLink
        }

        return true;
    }
}
