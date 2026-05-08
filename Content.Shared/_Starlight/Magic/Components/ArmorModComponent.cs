using Robust.Shared.GameStates;
using Content.Shared._Starlight.Magic.Systems;
using Content.Shared.Damage;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Allows applying a DamageModifierSet and other attributes of an ArmorComponent as a StatusEffect.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedArmorModSystem))]
public sealed partial class ArmorModComponent : Component
{
    public Dictionary<EntityUid, ArmorMod> modifiers = new();
};

[DataDefinition]
[Serializable, NetSerializable]
[Virtual]
[Access(typeof(SharedArmorModSystem))]
public partial class ArmorMod
{
    public DamageModifierSet Modifiers = default!;
    public bool IgnoreKnockdown = false;
    public float StaminaDamageModifier = 1.0f;
    public float ExplosionResistance = 1.0f;

    public ArmorMod(DamageModifierSet modifiers, bool ignoreKnockdown, float staminaDamageModifier, float explosionResistance)
    {
        // i am a brainlet, and this is my deep copy constructor
        Modifiers = new DamageModifierSet(modifiers);
        IgnoreKnockdown = ignoreKnockdown;
        StaminaDamageModifier = staminaDamageModifier;
        ExplosionResistance = explosionResistance;
    }
};
