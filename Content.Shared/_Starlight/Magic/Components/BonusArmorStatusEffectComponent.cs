using Content.Shared._Starlight.Magic.Systems;
using Content.Shared.Damage;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Factory for adding <see cref="BonusArmorComponent"/>s to mobs. These are temporary <see cref="ArmorComponent"/>s,
/// granted as a status effect with no corresponding inventory slot. Multiple may be applied simultaneously from
/// different sources, so long as each source is its own entity with <see cref="BonusArmorStatusEffectComponent"/>.
/// </summary>
[RegisterComponent]
[Access(typeof(SharedBonusArmorSystem))]
public sealed partial class BonusArmorStatusEffectComponent : Component
{
    /// <summary>
    /// The damage reduction
    /// </summary>
    [DataField(required: true)]
    public DamageModifierSet Modifiers = default!;

    /// <summary>
    /// If true, the effect will reset itself if already active from the same source; useful for decaying buffs.
    /// </summary>
    [DataField]
    public bool OverwriteOnRefresh = false;

    /// <summary>
    /// If true, ignores knockdown from tasers.
    /// </summary>
    [DataField]
    public bool IgnoreKnockdown = false;

    /// <summary>
    /// Stamina damage reduction
    /// </summary>
    [DataField("staminaDamageModifier")]
    public float StaminaDamageModifier = 1.0f;

    /// <summary>
    /// Explosion resistance
    /// </summary>
    [DataField("explosionResistance")]
    public float ExplosionResistance = 1.0f;
}

// will process normal CoefficientQueryEvents from ArmorComponent.cs
