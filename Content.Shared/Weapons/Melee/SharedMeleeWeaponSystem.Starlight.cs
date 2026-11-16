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

    /// <summary>
    /// Attempts a remote heavy attack toward the specified position.
    /// </summary>
    /// <param name="user">The entity performing the remote attack.</param>
    /// <param name="coordinates">The aimed-at position in entity coordinates, converted to map coordinates for the attack ray.</param>
    /// <param name="session">The session associated with the request, if available; it is passed to the normal heavy-attack attempt.</param>
    /// <returns>
    /// <see langword="true"/> if the generated attack is accepted by <see cref="TryAttemptRemoteHeavyAttack"/>; otherwise,
    /// <see langword="false"/>. A true return does not guarantee that a target was hit.
    /// </returns>
    /// <remarks>
    /// The user must be in combat mode and have a melee weapon that is not already attacking or on cooldown, and
    /// <see cref="Blocker.CanAttack"/> must allow the attack. A use-key gun must have heavy melee alt-fire, and an
    /// unarmed weapon with <see cref="MeleeWeaponComponent.AltDisarm"/> enabled is not eligible. The user's origin
    /// entity must have a transform, and the target position must be on the same map. The method then raycasts toward
    /// the position and submits the resulting attack to the normal heavy-attack attempt.
    /// </remarks>
    public bool TryAttemptRemoteHeavyAttackAt(EntityUid user, EntityCoordinates coordinates, ICommonSession? session)
    {
        if (!CombatMode.IsInCombatMode(user)
            || !TryGetWeapon(user, out var weaponUid, out var weapon))
            return false;

        if (!Blocker.CanAttack(user, weapon: (weaponUid, weapon))
            || weapon.Attacking
            || weapon.NextAttack > Timing.CurTime)
            return false;

        if (!CanUseRemoteHeavyAttackWeapon(user, weaponUid, weapon))
            return false;

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

    protected bool CanUseRemoteHeavyAttackWeapon(EntityUid user, EntityUid weaponUid, MeleeWeaponComponent weapon) =>
        TryComp<GunComponent>(weaponUid, out var gun) && gun.UseKey
            ? TryComp<AltFireMeleeComponent>(weaponUid, out var altFire)
                && altFire.AttackType == AltFireAttackType.Heavy
            : !weapon.AltDisarm || weaponUid != user;

    #endregion
}
