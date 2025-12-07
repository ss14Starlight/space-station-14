using System.Diagnostics.CodeAnalysis;
using Content.Shared.Traits;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences.Loadouts.Effects;

/// <summary>
/// Excludes loadouts if the character has any of the specified traits.
/// Used to prevent conflicting loadouts (e.g., nitrogen gear for characters with OxygenBreather trait).
/// </summary>
public sealed partial class TraitExclusionLoadoutEffect : LoadoutEffect
{
    [DataField(required: true)]
    public List<ProtoId<TraitPrototype>> Traits = new();

    public override bool Validate(HumanoidCharacterProfile profile, RoleLoadout loadout, ICommonSession? session, IDependencyCollection collection,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        // Check if the character has any of the excluded traits
        foreach (var excludedTrait in Traits)
        {
            if (profile.TraitPreferences.Contains(excludedTrait.Id))
            {
                reason = FormattedMessage.FromUnformatted(Loc.GetString("loadout-trait-exclusion"));
                return false;
            }
        }

        reason = null;
        return true;
    }
}
