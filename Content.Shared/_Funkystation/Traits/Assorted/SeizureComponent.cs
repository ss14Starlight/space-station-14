// SPDX-FileCopyrightText: 2025 TrixxedHeart <46364955+TrixxedBit@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;
using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.Traits.Assorted;

/// <summary>
/// Seizure episode phases.
/// </summary>
public enum SeizureState : byte
{
    /// <summary>
    /// Warning phase with faint visual overlay and gradual movement slowdown.
    /// </summary>
    Prodrome = 0,

    /// <summary>
    /// Active seizure with harsh overlay, stunning, and visual effects.
    /// </summary>
    Seizure = 1,

    /// <summary>
    /// Recovery phase with gradual movement speed restoration.
    /// </summary>
    Recovery = 2,

    /// <summary>
    /// Final fade phase where overlay fades while component cleans up.
    /// </summary>
    Fading = 3
}

/// <summary>
/// Component for entities experiencing a seizure episode.
/// Manages timing and state for prodrome, seizure, recovery, and fading phases.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SeizureComponent : Component
{
    /// <summary>
    /// Duration range for seizure episodes, (min, max) in seconds.
    /// Default: 25-30 seconds to match the audio length
    /// </summary>
    [DataField]
    public Vector2 SeizureDuration = new(25f, 30f);

    /// <summary>
    /// How long the prodrome warning phase lasts before seizure begins.
    /// </summary>
    [DataField]
    public float ProdromeDuration = 10f;

    /// <summary>
    /// How long the recovery phase lasts.
    /// </summary>
    [DataField]
    public float RecoveryDuration = 6f;

    /// <summary>
    /// Visual jittering amplitude during seizure.
    /// </summary>
    [DataField]
    public float JitterAmplitude = 25f;

    /// <summary>
    /// Visual jittering frequency during seizure.
    /// </summary>
    [DataField]
    public float JitterFrequency = 6f;

    /// <summary>
    /// Current seizure phase state.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SeizureState CurrentState = SeizureState.Prodrome;

    /// <summary>
    /// Time remaining in the current phase.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RemainingTime;

    /// <summary>
    /// Current movement speed multiplier.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MovementSpeedMultiplier = 1.0f;

    /// <summary>
    /// Target movement speed for interpolation.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TargetMovementSpeed = 1.0f;

    /// <summary>
    /// Custom popup key for the prodrome phase.
    /// </summary>
    [DataField]
    public string? ProdromePopupKey;

    /// <summary>
    /// Custom popup key for the seizure phase.
    /// </summary>
    [DataField]
    public string? SeizurePopupKey;

    /// <summary>
    /// Custom popup key for the recovery phase.
    /// </summary>
    [DataField]
    public string? RecoveryPopupKey;

    /// <summary>
    /// How much damage can be dealt before the entity will stop taking damage from drag
    /// </summary>
    [DataField]
    public FixedPoint2 DamageUpperBound = 100;
}
