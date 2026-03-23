using System.Diagnostics.CodeAnalysis;
using Content.Shared.Localizations;
using Content.Shared.Starlight;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Content.Shared._NullLink;

namespace Content.Shared.Roles;

[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class OverallPlaytimeRequirement : JobRequirement
{
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

        //NullLink start
        var bypass = player is not null &&
                     IoCManager.Resolve<ISharedNullLinkPlayerRolesReqManager>().IsAllRolesAvailable(player);
        //NullLink end

        var overallTime = playTimes.GetValueOrDefault(PlayTimeTrackingShared.TrackerOverall);
        var overallDiffSpan = Time - overallTime;
        var overallDiff = overallDiffSpan.TotalMinutes;
        var formattedCurrent = ContentLocalizationManager.FormatPlaytime(overallTime);
        var formattedRequired = ContentLocalizationManager.FormatPlaytime(Time);

        details = FormattedMessage.FromMarkupPermissive(Loc.GetString(
            Inverted ? "role-timer-overall-not-too-high" : "role-timer-overall-sufficient",
            ("current", formattedCurrent),
            ("required", formattedRequired)));

        if (!Inverted)
        {
            if (overallDiff <= 0 || overallTime >= Time)
                return true;

            details = FormattedMessage.FromMarkupPermissive(Loc.GetString(
                "role-timer-overall-insufficient",
                ("current", formattedCurrent),
                ("required", formattedRequired)));
            return bypass;
        }

        if (overallDiff <= 0 || overallTime >= Time)
        {
            details = FormattedMessage.FromMarkupPermissive(
                Loc.GetString("role-timer-overall-too-high",
                ("current", formattedCurrent),
                ("required", formattedRequired)));
            return bypass;
        }

        return true;
    }
}
