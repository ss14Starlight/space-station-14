using Robust.Shared.GameStates;
using Content.Shared._Starlight.Magic.Systems;
using Content.Shared.Damage;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Allows applying a DamageModifierSet and other attributes of an ArmorComponent as a StatusEffect.
/// </summary>
[RegisterComponent]
[Access(typeof(SharedBonusArmorSystem))]
public sealed partial class BonusArmorComponent : Component
{
    public Dictionary<EntityUid, BonusArmor> modifiers = new();
}

[DataDefinition]
[Serializable, NetSerializable]
[Access(typeof(SharedBonusArmorSystem))]
public partial struct BonusArmor
{
    public DamageModifierSet Modifiers = default!;
    public bool IgnoreKnockdown = false;
    public float StaminaDamageModifier = 1.0f;
    public float ExplosionResistance = 1.0f;
}
