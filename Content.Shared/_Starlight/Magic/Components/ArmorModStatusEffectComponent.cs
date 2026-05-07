using Content.Shared._Starlight.Scaling;
using Robust.Shared.GameStates;
using Content.Shared._Starlight.Magic.Systems;
using Content.Shared.Armor;
using Content.Shared.Damage;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Factory for adding ArmorModComponents to mobs. These are temporary ArmorComponents granted as a status effect with no corresponding inventory slot.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedArmorModSystem), typeof(SharedScalingSystem))]
public sealed partial class ArmorModStatusEffectComponent : Component
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

// will attempt to process normal CoefficientQueryEvents from ArmorComponent.cs, regardless of slot flags
