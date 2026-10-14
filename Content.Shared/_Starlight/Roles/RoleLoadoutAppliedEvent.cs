using Content.Shared.Preferences.Loadouts;

namespace Content.Shared._Starlight.Roles;

/// <summary>
/// Raised on a jobEntity-spawned mob after its role loadout is applied.
/// </summary>
public sealed class RoleLoadoutAppliedEvent : EntityEventArgs
{
    public readonly RoleLoadout Loadout;

    public RoleLoadoutAppliedEvent(RoleLoadout loadout)
    {
        Loadout = loadout;
    }
}
