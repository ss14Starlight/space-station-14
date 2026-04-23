using System.Diagnostics.CodeAnalysis;
using Content.Shared.Preferences;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Roles;

/// <summary>
/// Requires the character to be older or younger than a certain age (inclusive)
/// </summary>
[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class AgeRequirement : JobRequirement
{
    [DataField(required: true)]
    public int RequiredAge;

    public override bool Check(IEntityManager entManager,
        ICommonSession? player,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan>? playTimes,
        out FormattedMessage reason) // Starlight: Always return reason
    {
        reason = new FormattedMessage();

        if (profile is null) //the profile could be null if the player is a ghost. In this case we don't need to block the role selection for ghostrole
            return true;

        // Starlight BEGIN: Reason is successful by default
        reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
            Inverted ? "role-timer-age-young-enough" : "role-timer-age-old-enough",
            ("age", RequiredAge)));
        // Starlight END

        if (!Inverted)
        {
            if (profile.Age >= RequiredAge) // Starlight BEGIN
                return true;

            reason = FormattedMessage.FromMarkupPermissive(Loc.GetString("role-timer-age-not-old-enough",
                ("age", RequiredAge)));
            return false; // Starlight END
        }
        else
        {
            if (profile.Age <= RequiredAge) // Starlight BEGIN
                return true;

            reason = FormattedMessage.FromMarkupPermissive(Loc.GetString("role-timer-age-not-young-enough",
                ("age", RequiredAge)));
            return false; // Starlight END
        }
    }
}
