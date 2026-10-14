using Robust.Shared.GameStates;
using Content.Shared._Starlight.Magic.Systems;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Allows applying various multipliers to target entities as a StatusEffect. See <see cref="BonusScalarStatusEffectComponent"/> and <see cref="SharedBonusScalarSystem"/>.
/// </summary>
[AutoGenerateComponentState]
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedBonusScalarSystem))]
public sealed partial class BonusScalarComponent : Component
{
    public Dictionary<EntityUid, BonusScalarCoefficients> modifiers = new();

    // computed totals:

    /// <summary>
    /// The effective total speed multiplier for attacking with unarmed (innate) weapons like punches. Bigger = faster.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float unarmedAttackRate = 1.0f;

    /// <summary>
    /// The effective total multiplier for damage from unarmed (innate) weapons like punches. Bigger = better.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float unarmedDamage = 1.0f;

    /// <summary>
    /// The effective total speed multiplier for attacking with melee weapons provided by equipment. Bigger = faster.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float meleeWeaponAttackRate = 1.0f;

    /// <summary>
    /// The effective total multiplier for damage with melee weapons provided by equipment. Bigger = better.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float meleeWeaponDamage = 1.0f;

    /* // TODO:
        /// <summary>
        /// The effective total multiplier for damage with ranged weapons provided by equipment. Bigger = better.
        /// </summary>
        [DataField, AutoNetworkedField]
        public float rangedWeaponDamage = 1.0f;

        /// <summary>
        /// The effective total speed multiplier for attacking with ranged weapons provided by equipment. Bigger = faster.
        /// </summary>
        [DataField, AutoNetworkedField]
        public float rangedWeaponAttackRate = 1.0f;
    */

    /// <summary>
    /// Coefficient for scaling the duration of DoAfters (interactions). Smaller = faster.
    /// </summary>
    [DataField, AutoNetworkedField]
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

    /* // TODO
    [DataField]
    public float rangedWeaponAttackRate = 1.0f;

    [DataField]
    public float rangedWeaponDamage = 1.0f; */

    [DataField]
    public float doAfterDelay = 1.0f;
}
