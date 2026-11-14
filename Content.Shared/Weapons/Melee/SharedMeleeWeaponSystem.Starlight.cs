using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Player;

namespace Content.Shared.Weapons.Melee;

public abstract partial class SharedMeleeWeaponSystem
{
    public bool TryAttemptRemoteHeavyAttack(EntityUid user, HeavyAttackEvent attack, ICommonSession? session)
    {
        if (!TryGetWeapon(user, out var weaponUid, out var weapon)
            || weaponUid != GetEntity(attack.Weapon)
            || !AttemptAttack(user, weaponUid, weapon, attack, session))
            return false;

        weapon.Attacking = false;
        DirtyField(weaponUid, weapon, nameof(MeleeWeaponComponent.Attacking));
        return true;
    }
}
