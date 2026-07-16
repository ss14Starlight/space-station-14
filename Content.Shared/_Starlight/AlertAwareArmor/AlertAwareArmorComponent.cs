using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.AlertAwareArmor;

/// <summary>
/// A component that is an alternative for a regular ArmorComponent, it allows to define armor levels per station alert level
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedAlertAwareArmorSystem))]
public sealed partial class AlertAwareArmorComponent : Component
{
    /// <summary>
    /// The default set of modifiers, used when no alert matches.
    /// </summary>
    [DataField(required: true)]
    public DamageModifierSet Modifiers = default!;

    /// <summary>
    /// A multiplier applied to the calculated point value
    /// to determine the monetary value of the armor.
    /// </summary>
    [DataField]
    public float PriceMultiplier = 1;

    /// <summary>
    /// If true, you can examine the armor to see the protection. If false, the verb won't appear.
    /// </summary>
    [DataField]
    public bool ShowArmorOnExamine = true;

    /// <summary>
    /// If true, ignores knockdown from tasers, unless the current alert level overrides it.
    /// </summary>
    [DataField]
    public bool IgnoreKnockdown = false;

    /// <summary>
    /// Stamina damage reduction, unless the current alert level overrides it.
    /// </summary>
    [DataField("staminaModifier")]
    public float StaminaDamageModifier = 1.0f;

    /// <summary>
    /// Modifiers per alert level.
    /// </summary>
    [DataField]
    public Dictionary<string, AlertArmorLevel> Levels = new();
}

/// <summary>
/// Set of Modifiers per alert level.
/// </summary>
[DataDefinition]
public sealed partial class AlertArmorLevel
{
    /// <summary>
    /// The damage reduction for this alert level.
    /// </summary>
    [DataField(required: true)]
    public DamageModifierSet Modifiers = default!;

    /// <summary>
    /// Stamina damage reduction for this alert level.
    /// </summary>
    [DataField("staminaModifier")]
    public float? StaminaDamageModifier;

    /// <summary>
    /// Whether taser knockdown is ignored for this alert level.
    /// </summary>
    [DataField]
    public bool? IgnoreKnockdown;
}
