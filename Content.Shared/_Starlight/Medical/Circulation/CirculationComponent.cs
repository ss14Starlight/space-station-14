// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.Medical.Circulation;

/// <summary>
/// Blood circulation of a body: the heart pumps the blood, the lungs oxygenate it, and the tissues get
/// <see cref="OxygenDelivery"/> = <see cref="SpO2"/> × <see cref="Perfusion"/>. Too little of it is hypoxia, which
/// replaces the old flat damage from blood loss and from suffocation.
/// </summary>
/// <remarks>
/// Simulated on the server; the values here are the summary the client shows (analyzer, alerts).
/// A body that has no heart when it is set up (IPC and other bodies without blood circulation) is left alone.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class CirculationComponent : Component
{
    /// <summary>
    /// How well the blood flows, 0..1: the heart's output times how much blood there is to pump.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Perfusion = 1f;

    /// <summary>
    /// Oxygen saturation of the blood, 0..1.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpO2 = 0.98f;

    /// <summary>
    /// Oxygen the tissues get, 0..1.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float OxygenDelivery = 1f;

    /// <summary>
    /// Beats per minute; 0 with a stopped heart.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HeartRate = 75f;

    /// <summary>
    /// The body had a heart when it was set up, so it circulates blood at all.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool HasCirculation = true;

    [DataField]
    public bool Checked;

    /// <summary>
    /// The body does not circulate blood even with a heart (an IPC's pump moves coolant).
    /// </summary>
    [DataField]
    public bool Disabled;

    /// <summary>
    /// Blood level (fraction of normal) at which perfusion is full, and at which it is gone.
    /// </summary>
    [DataField]
    public float FullPerfusionBloodLevel = 0.9f;

    [DataField]
    public float NoPerfusionBloodLevel = 0.4f;

    /// <summary>
    /// The heart beats up to this much faster to make up for missing blood.
    /// </summary>
    [DataField]
    public float MaxCompensation = 0.8f;

    /// <summary>
    /// Oxygen delivery below this hurts; at zero delivery the full <see cref="HypoxiaDamage"/> applies each update.
    /// </summary>
    [DataField]
    public float HypoxiaThreshold = 0.7f;

    /// <summary>
    /// Oxygen delivery above this slowly heals the hypoxia damage.
    /// </summary>
    [DataField]
    public float RecoveryThreshold = 0.85f;

    /// <summary>
    /// Seconds of no oxygen delivery at all the tissues can take before hypoxia hurts; spent faster the worse the
    /// delivery is, refilled while it is good.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float OxygenReserve = 60f;

    [DataField]
    public float MaxOxygenReserve = 60f;

    [DataField]
    public DamageSpecifier HypoxiaDamage = new() { DamageDict = { ["Asphyxiation"] = 4 } };

    [DataField]
    public DamageSpecifier RecoveryDamage = new() { DamageDict = { ["Asphyxiation"] = -1 } };

    /// <summary>
    /// How fast <see cref="SpO2"/> follows the lungs, per update.
    /// </summary>
    [DataField]
    public float SpO2Response = 0.5f;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);

    [DataField]
    public float UpdateIntervalMultiplier = 1f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;
}
