using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using System.Linq;
using Robust.Shared.Map;
using Robust.Shared.Maths;
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

    public bool TryAttemptRemoteHeavyAttackAt(EntityUid user, EntityCoordinates coordinates, ICommonSession? session)
    {
        if (!CombatMode.IsInCombatMode(user)
            || !TryGetWeapon(user, out var weaponUid, out var weapon))
            return false;

        if (!Blocker.CanAttack(user, weapon: (weaponUid, weapon))
            || weapon.Attacking
            || weapon.NextAttack > Timing.CurTime)
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

        var originUid = GetOriginEntity(user);
        if (!TryComp(originUid, out TransformComponent? originXform))
            return false;

        var targetMap = TransformSystem.ToMapCoordinates(coordinates);
        if (targetMap.MapId != originXform.MapID)
            return false;

        var originPos = TransformSystem.GetWorldPosition(originXform);
        var direction = targetMap.Position - originPos;
        var distance = MathF.Min(weapon.Range, direction.Length());
        var entities = GetNetEntitySet(ArcRayCast(
            originPos,
            direction.ToWorldAngle(),
            weapon.Angle,
            distance,
            originXform.MapID,
            originUid));
        entities.Remove(GetNetEntity(user));
        entities = entities.Take(Math.Min(MaxTargets, entities.Count)).ToHashSet();

        var attack = new HeavyAttackEvent(
            GetNetEntity(weaponUid),
            entities,
            GetNetCoordinates(coordinates));
        return TryAttemptRemoteHeavyAttack(user, attack, session);
    }

    #endregion
}
