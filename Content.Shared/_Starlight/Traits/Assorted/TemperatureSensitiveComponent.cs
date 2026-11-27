using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Traits.Assorted;

/// <summary>
/// Makes the entity take more damage of certain types.
/// The modifiers apply to regular damage, and the matching multiplier scales damage from body temperature, which ignores resistances.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TemperatureSensitiveComponent : Component
{
    /// <summary>
    /// Damage modifiers applied to all incoming damage that respects resistances.
    /// </summary>
    [DataField(required: true)]
    public DamageModifierSet Modifiers = new();

    /// <summary>
    /// Multiplier for damage taken from being overheated. 1 leaves it unchanged.
    /// </summary>
    [DataField]
    public float HeatTemperatureMultiplier = 1f;

    /// <summary>
    /// Multiplier for damage taken from being overcooled. 1 leaves it unchanged.
    /// </summary>
    [DataField]
    public float ColdTemperatureMultiplier = 1f;

    /// <summary>
    /// The temperature damage the entity had before the multipliers were applied, restored when this component is removed.
    /// </summary>
    [ViewVariables]
    public DamageSpecifier? OriginalHeatDamage;

    [ViewVariables]
    public DamageSpecifier? OriginalColdDamage;
}
