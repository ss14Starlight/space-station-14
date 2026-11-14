using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Player;

namespace Content.Shared.Weapons.Melee;

public abstract partial class SharedMeleeWeaponSystem
{
    #region Starlight

    public bool TryAttemptRemoteLightAttack(EntityUid user, EntityUid weaponUid, MeleeWeaponComponent weapon,
        EntityUid target)
    {
        if (!AttemptLightAttack(user, weaponUid, weapon, target))
            return false;

        weapon.Attacking = false;
        DirtyField(weaponUid, weapon, nameof(MeleeWeaponComponent.Attacking));
        return true;
    }

    /// <summary>
    /// Attempts to execute a heavy attack for the remotely controlled entity.
    /// </summary>
    /// <param name="user">The remote entity performing the attack.</param>
    /// <param name="attack">The heavy-attack request.</param>
    /// <param name="session">The session that sent the request, if any.</param>
    /// <returns>
    /// <see langword="true"/> only if the heavy attack is successfully initiated; otherwise,
    /// <see langword="false"/>.
    /// </returns>
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

    #endregion
}
