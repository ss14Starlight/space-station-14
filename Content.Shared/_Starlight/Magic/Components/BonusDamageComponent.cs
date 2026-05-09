using Robust.Shared.GameStates;
using Content.Shared._Starlight.Magic.Systems;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Allows applying various multipliers to target entities as a StatusEffect. See <see cref="BonusDamageStatusEffectComponent"/> and <see cref="SharedBonusDamageSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedBonusDamageSystem))]
public sealed partial class BonusDamageComponent : Component
{
    public Dictionary<EntityUid, BonusDamageMod> modifiers = new();

    // computed totals:
    public Dictionary<string, float> UnarmedBonusDamage = new();
    public Dictionary<string, float> MeleeWeaponBonusDamage = new();
    public Dictionary<string, float> RangedWeaponBonusDamage = new();
}

[DataDefinition]
[Serializable, NetSerializable]
[Access(typeof(SharedBonusDamageSystem))]
public partial struct BonusDamageMod
{
    [DataField("damageTypes", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<float, DamageTypePrototype>), required: true)]
    public Dictionary<string, float> DamageTypes = new();

    [DataField]
    public bool AffectsUnarmed = false;

    [DataField]
    public bool AffectsMeleeWeapons = false;

    [DataField]
    public bool AffectsRangedWeapons = false;

    [DataField]
    public bool OverwriteOnRefresh = false;
}
