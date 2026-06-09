using System.Diagnostics.CodeAnalysis;
using System.Text;
using Content.Shared._Starlight.Traits;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Traits;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Roles;

/// <summary>
/// Requires a character to have, or not have, certain traits
/// </summary>
[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class TraitsRequirement : JobRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<TraitPrototype>> Traits = new();

    public override bool Check(IEntityManager entManager,
        ICommonSession? player,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan>? playTimes,
        out FormattedMessage reason) // Starlink: Always return reason
    {
        reason = new FormattedMessage();

        if (profile is null) //the profile could be null if the player is a ghost. In this case we don't need to block the role selection for ghostrole
            return true;

        var sb = new StringBuilder();
        // Starlight: No color here, in .ftl instead
        foreach (var t in Traits)
        {
            sb.Append(Loc.GetString(protoManager.Index(t).Name) + " ");
        }
        // Starlight: No color here, in .ftl instead

        // Starlight BEGIN
        reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
            Inverted ? "role-timer-blacklisted-traits-pass" : "role-timer-whitelisted-traits-pass",
            ("traits", sb)));
        var hasAnyTrait = Traits.Overlaps(profile.TraitPreferences);

        // !Inverted = Whitelist mode, meaning player must have ONE of the traits.
        // Inverted = Blacklist mode, meaning player must have NONE of the traits.
        if (!Inverted == hasAnyTrait)
            return true;

        // Change to fail message.
        reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
            Inverted ? "role-timer-blacklisted-traits-fail" : "role-timer-whitelisted-traits-fail",
            ("traits", sb)));
        return false;

        /*
        if (!Inverted)
        {
            reason = FormattedMessage.FromMarkupPermissive($"{Loc.GetString("role-timer-whitelisted-traits")}\n{sb}");
            //at least one of
            foreach (var trait in Traits)
            {
                if (profile.TraitPreferences.Contains(trait))
                    return true;
            }
            return false;
        }
        else
        {
            reason = FormattedMessage.FromMarkupPermissive($"{Loc.GetString("role-timer-blacklisted-traits")}\n{sb}");

            foreach (var trait in Traits)
            {
                if (profile.TraitPreferences.Contains(trait))
                    return false;
            }
        }

        return true; */ // Starlight END
    }
}
