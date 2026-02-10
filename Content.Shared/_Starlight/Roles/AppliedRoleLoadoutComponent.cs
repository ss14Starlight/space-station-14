using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Robust.Shared.GameStates;

namespace Content.Shared.Roles;

/// <summary>
/// Stores the applied role loadout and character profile for an entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AppliedRoleLoadoutComponent : Component
{
    [DataField]
    public RoleLoadout? Loadout;

    [DataField]
    public HumanoidCharacterProfile? Profile;
}
