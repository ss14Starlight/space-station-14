using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Map;

namespace Content.Client.Weapons.Melee;

public sealed partial class MeleeWeaponSystem
{
    public bool TryHandleRemoteHeavyAttack(EntityUid user, MapCoordinates mousePosition)
    {
        if (!CombatMode.IsInCombatMode(user)
            || !TryGetWeapon(user, out var weaponUid, out var weapon))
            return false;

        if (TryComp<GunComponent>(weaponUid, out var gun) && gun.UseKey)
        {
            if (!TryComp<AltFireMeleeComponent>(weaponUid, out var altFire)
                || altFire.AttackType != AltFireAttackType.Heavy)
                return false;
        }
        else if (weapon.AltDisarm && weaponUid == user)
        {
            return false;
        }

        if (!Blocker.CanAttack(user, weapon: (weaponUid, weapon))
            || weapon.Attacking
            || weapon.NextAttack > Timing.CurTime)
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
