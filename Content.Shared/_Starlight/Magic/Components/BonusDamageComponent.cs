using Robust.Shared.GameStates;
using Content.Shared._Starlight.Magic.Systems;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;
using Content.Shared.Damage;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Allows applying various flat damage bonuses to target entities as a StatusEffect.
/// See <see cref="BonusDamageStatusEffectComponent"/> and <see cref="SharedBonusDamageSystem"/>.
/// If you want to apply bonus damage as a permanent trait instead of via a status effect,
/// consider wizden's original <see cref="BonusMeleeDamageComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedBonusDamageSystem))]
public sealed partial class BonusDamageComponent : Component
{
    public Dictionary<EntityUid, BonusDamageMod> modifiers = new();

    // computed totals:
    public DamageSpecifier? UnarmedBonusDamage;
    public DamageSpecifier? MeleeWeaponBonusDamage;
}

[DataDefinition]
[Serializable, NetSerializable]
[Access(typeof(SharedBonusDamageSystem))]
public partial struct BonusDamageMod
{
    public DamageSpecifier Damage;

    public bool AffectsUnarmed = false;

    public bool AffectsMeleeWeapons = false;

    // todo:    public bool AffectsRangedWeapons = false;

    public bool OverwriteOnRefresh = false;
}
