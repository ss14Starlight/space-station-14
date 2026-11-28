using Robust.Shared.Map;

// ReSharper disable CheckNamespace
namespace Content.Client.Weapons.Melee;

public sealed partial class MeleeWeaponSystem
{
    public bool CanHandleRemoteHeavyAttack(EntityUid user)
        => CombatMode.IsInCombatMode(user)
            && TryGetWeapon(user, out var weaponUid, out var weapon)
            && CanUseRemoteHeavyAttackWeapon(user, weaponUid, weapon);

    /// <summary>
    /// Handles the remote heavy-attack input at the requested map position.
    /// </summary>
    /// <returns>
    /// Returns the success of the function if consumed and handled.
    /// </returns>
    public bool TryHandleRemoteHeavyAttack(EntityUid user, MapCoordinates mousePosition)
        => TryHandleRemoteHeavyAttack(user, mousePosition, ignoreClientAttackState: false);

    /// <summary>
    /// Handles remote heavy-attack input, optionally ignoring client-side attack state for hold repeats.
    /// </summary>
    /// <returns>
    /// Returns the success of the function if consumed and handled.
    /// </returns>
    public bool TryHandleRemoteHeavyAttack(
        EntityUid user,
        MapCoordinates mousePosition,
        bool ignoreClientAttackState)
    {
        if (!CombatMode.IsInCombatMode(user)
            || !TryGetWeapon(user, out var weaponUid, out var weapon))
            return false;

        if (!CanUseRemoteHeavyAttackWeapon(user, weaponUid, weapon))
            return false;

        if (!Blocker.CanAttack(user, weapon: (weaponUid, weapon))
            || (!ignoreClientAttackState && (weapon.Attacking || weapon.NextAttack > Timing.CurTime)))
            return true;

        if (mousePosition.MapId == MapId.Nullspace)
            return true;

        var coordinates = MapManager.TryFindGridAt(mousePosition, out var gridUid, out _)
            ? TransformSystem.ToCoordinates(gridUid, mousePosition)
            : TransformSystem.ToCoordinates(_map.GetMap(mousePosition.MapId), mousePosition);
        ClientHeavyAttack(user, coordinates, weaponUid, weapon);
        return true;
    }
}
