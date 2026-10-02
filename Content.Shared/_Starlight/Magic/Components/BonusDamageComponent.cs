using Robust.Shared.GameStates;
using Content.Shared._Starlight.Magic.Systems;
using Robust.Shared.Serialization;
using Content.Shared.Damage;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Allows applying various flat damage bonuses to target entities as a StatusEffect, or directly.
/// See <see cref="BonusDamageStatusEffectComponent"/> and <see cref="SharedBonusDamageSystem"/>.
/// May be used on either weapons or wielders.
///
/// If you want to apply bonus damage as a permanent trait instead of via a status effect,
/// consider Wizden's original <see cref="BonusMeleeDamageComponent"/>, as this component
/// will be automatically removed when the last BonusDamageStatusEffect ends.
/// </summary>

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedBonusDamageSystem))]
public sealed partial class BonusDamageComponent : Component
{
    public Dictionary<EntityUid, BonusDamageMod> modifiers = new();

    // computed totals:
    [DataField, AutoNetworkedField]
    public DamageSpecifier? UnarmedBonusDamage;

    [DataField, AutoNetworkedField]
    public DamageSpecifier? MeleeWeaponBonusDamage;
}

[DataDefinition]
[Serializable, NetSerializable]
[Access(typeof(SharedBonusDamageSystem))]
public partial struct BonusDamageMod
{
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage;

    [DataField, AutoNetworkedField]
    public bool AffectsUnarmed = false;

    [DataField, AutoNetworkedField]
    public bool AffectsMeleeWeapons = false;

    // todo:    public bool AffectsRangedWeapons = false;
    [DataField, AutoNetworkedField]
    public bool OverwriteOnRefresh = false;
}
