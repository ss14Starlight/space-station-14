using Content.Shared.Weapons.Melee;

namespace Content.Shared._Starlight.Cuffs;

/// <summary>
/// Raised on a cuffed entity when it tries to attack. Set <see cref="Allowed"/> to let the attack through despite the cuffs.
/// </summary>
[ByRefEvent]
public record struct CuffedAttackAttemptEvent(EntityUid? Target, Entity<MeleeWeaponComponent>? Weapon, bool Disarm)
{
    public bool Allowed;
}
