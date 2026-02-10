using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Preferences.Loadouts.Effects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;

namespace Content.Shared._Starlight.Silicons;

/// <summary>
/// Handles inserting brains into silicons
/// </summary>
public sealed partial class SiliconBrainLoadoutEffect : LoadoutEffect
{
    /// <summary>
    /// Specific brain proto to use.
    /// </summary>
    [DataField]
    public EntProtoId? BrainPrototype;

    /// <summary>
    /// Force the brain to be in MMI
    /// </summary>
    [DataField]
    public bool UseMMI = false;

    public override bool Validate(HumanoidCharacterProfile profile, RoleLoadout loadout, ICommonSession? session, IDependencyCollection collection, [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = null;
        return true;
    }
}
