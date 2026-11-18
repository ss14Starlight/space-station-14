using Content.Shared.Damage;

// ReSharper disable CheckNamespace
namespace Content.Shared.Weapons.Ranged.Upgrades.Components;

public sealed partial class GunUpgradeDamageComponent
{
    /// <summary>
    /// Additional damage multiplied by the projectile's base damage.
    /// </summary>
    [DataField]
    public DamageSpecifier DamageMultiplier = new();
}
