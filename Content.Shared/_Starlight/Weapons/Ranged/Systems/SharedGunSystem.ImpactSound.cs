using Content.Shared.Damage;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    public const float DamagePitchVariation = 0.05f;

    /// <summary>
    /// Gets the impact sound of a hitscan shot. A shooter that predicts its hitscans has already heard it.
    /// </summary>
    public SoundSpecifier? GetImpactSound(
        EntityUid otherEntity,
        DamageSpecifier? modifiedDamage,
        SoundSpecifier? weaponSound,
        bool forceWeaponSound,
        out bool variation)
    {
        variation = false;

        if (!forceWeaponSound
            && modifiedDamage != null
            && modifiedDamage.GetTotal() > 0
            && TryComp<RangedDamageSoundComponent>(otherEntity, out var rangedSound))
        {
            var type = SharedMeleeWeaponSystem.GetHighestDamageSound(modifiedDamage, ProtoManager);

            if (type != null && rangedSound.SoundTypes?.TryGetValue(type, out var damageSoundType) == true)
            {
                variation = true;
                return damageSoundType;
            }

            if (type != null && rangedSound.SoundGroups?.TryGetValue(type, out var damageSoundGroup) == true)
            {
                variation = true;
                return damageSoundGroup;
            }
        }

        return weaponSound;
    }

    /// <summary>
    /// Plays the impact sound of a hitscan shot. A shooter that predicts its hitscans has already heard it.
    /// </summary>
    public virtual void PlayImpactSound(
        EntityUid otherEntity,
        DamageSpecifier? modifiedDamage,
        SoundSpecifier? weaponSound,
        bool forceWeaponSound,
        EntityUid? shooter)
        => PlayImpactSound(otherEntity, modifiedDamage, weaponSound, forceWeaponSound);
}
