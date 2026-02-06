// Humanoid EMP Component
// Created by Killer Tamashi and Princess Gurchi for the FH project.
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135

using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Silicons.HumanoidEMP;

/// <summary>
/// Component that defines how a humanoid silicon reacts to EMP pulses.
/// Allows for stuns, knockdowns, damage, and other effects.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HumanoidEMPComponent : Component
{
    /// <summary>
    /// How long to stun the entity when hit by EMP (in seconds).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StunTime = 5f;

    /// <summary>
    /// Damage to apply when hit by EMP.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier? Damage = null;

    /// <summary>
    /// How long to knock down the entity (in seconds).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float KnockdownTime = 0f;

    /// <summary>
    /// How long to slow down the entity (in seconds).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SlowdownTime = 0f;

    /// <summary>
    /// Walk speed modifier while slowed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WalkSpeedModifier = 1f;

    /// <summary>
    /// Sprint speed modifier while slowed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SprintSpeedModifier = 1f;

    /// <summary>
    /// Whether to drop all held items.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool DropHeldItems = false;

    /// <summary>
    /// Additional status effects to apply (effect prototype ID -> duration).
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId, TimeSpan> StatusEffects = new();

    /// <summary>
    /// Multiplier for EMP effect duration based on EMP strength.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float EffectMultiplier = 1f;
}
