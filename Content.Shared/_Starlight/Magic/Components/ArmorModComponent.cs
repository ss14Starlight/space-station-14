using Content.Shared._Starlight.Scaling;
using Robust.Shared.GameStates;
using Content.Shared._Starlight.Magic.Systems;
using Content.Shared.Armor;
using Content.Shared.Damage;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Allows applying a DamageModifierSet and other attributes of an ArmorComponent as a StatusEffect.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedArmorModSystem), typeof(SharedScalingSystem))]
public sealed partial class ArmorModComponent : Component
{
    /// <summary>
    /// The damage reduction
    /// </summary>
    [DataField(required: true)]
    public DamageModifierSet Modifiers = default!;

    /// <summary>
    /// If true, ignores knockdown from tasers.
    /// </summary>
    [DataField]
    public bool IgnoreKnockdown = false;

    /// <summary>
    /// Stamina damage reduction
    /// </summary>
    [DataField("staminaModifier")]
    public float StaminaDamageModifier = 1.0f;
}
