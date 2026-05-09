using Robust.Shared.GameStates;
using Content.Shared._Starlight.Magic.Systems;
using Content.Shared.Damage;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Allows applying various multipliers to target entities as a StatusEffect. See <see cref="BonusScalarStatusEffectComponent"/> and <see cref="SharedBonusScalarSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedBonusScalarSystem))]
public sealed partial class BonusScalarComponent : Component
{
    public Dictionary<EntityUid, BonusScalarCoefficients> modifiers = new();

    // computed totals:

    /// <summary>
    /// The effective total inverse speed multiplier for attacking with unarmed (innate) weapons like punches.
    /// </summary>
    public float unarmedAttackRate = 1.0f;
    /// <summary>
    /// The effective total multiplier for damage from unarmed (innate) weapons like punches.
    /// </summary>
    public float unarmedDamage = 1.0f;
    /// <summary>
    /// The effective total inverse speed multiplier for attacking with melee weapons provided by equipment.
    /// </summary>
    public float meleeWeaponAttackRate = 1.0f;
    /// <summary>
    /// The effective total multiplier for damage with melee weapons provided by equipment.
    /// </summary>
    public float meleeWeaponDamage = 1.0f;
    /// <summary>
    /// The effective total multiplier for damage with ranged weapons provided by equipment.
    /// </summary>
    public float rangedWeaponDamage = 1.0f;
    /// <summary>
    /// The effective total inverse speed multiplier for attacking with ranged weapons provided by equipment.
    /// </summary>
    public float rangedWeaponAttackRate = 1.0f;
    /// <summary>
    /// Coefficient for scaling the duration of DoAfters (interactions).
    /// </summary>
    public float doAfterDelay = 1.0f;
}

[DataDefinition]
[Serializable, NetSerializable]
[Access(typeof(SharedBonusScalarSystem))]
public partial struct BonusScalarCoefficients
{
    [DataField]
    public float unarmedAttackRate = 1.0f;

    [DataField]
    public float unarmedDamage = 1.0f;

    [DataField]
    public float meleeWeaponAttackRate = 1.0f;

    [DataField]
    public float meleeWeaponDamage = 1.0f;

    [DataField]
    public float rangedWeaponAttackRate = 1.0f;

    [DataField]
    public float rangedWeaponDamage = 1.0f;

    [DataField]
    public float doAfterDelay = 1.0f;
}
