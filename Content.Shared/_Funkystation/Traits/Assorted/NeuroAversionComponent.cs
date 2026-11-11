// SPDX-FileCopyrightText: 2026 TrixxedHeart <46364955+TrixxedBit@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Funkystation.Traits.Assorted;

/// <summary>
/// Component for entities with neuro aversion trait.
/// Handles migraine timing and seizure build-up when mind-shielded.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class NeuroAversionComponent : Component
{
    // =| MIGRAINES |=

    /// <summary>
    /// Time range between migraine episodes while mind-shielded, (min, max) in seconds.
    /// Default: 8-20 minutes. Frequency is affected by health.
    /// </summary>
    [DataField]
    public Vector2 TimeBetweenMigraines { get; private set; } = new(480f, 1200f);

    /// <summary>
    /// Duration range of migraine episodes, (min, max) in seconds.
    /// </summary>
    [DataField]
    public Vector2 MigraineDuration { get; private set; } = new(12f, 15f);

    // =| SEIZURES |=

    /// <summary>
    /// Base seizure build gain per second (0-1 scale).
    /// Takes ~10 hours to reach 100% build in good health.
    /// Targets 1 seizure every 5 rounds on average.
    /// </summary>
    [DataField]
    public float BaseSeizurePassivePerSec { get; private set; } = 0.000028f;

    /// <summary>
    /// Seizure meter threshold for triggering seizures.
    /// </summary>
    [DataField]
    public float SeizureThreshold { get; private set; } = 1.0f;

    /// <summary>
    /// Residual meter value after seizure (prevents immediate retriggering).
    /// </summary>
    [DataField]
    public float PostSeizureResidual { get; private set; } = 0.1f;

    // =| HEALTH MULTIPLIERS |=

    /// <summary>
    /// Build multiplier for good health condition.
    /// </summary>
    [DataField]
    public float ConditionGoodMultiplier { get; private set; } = 1.0f;

    /// <summary>
    /// Build multiplier for okay health condition.
    /// </summary>
    [DataField]
    public float ConditionOkayMultiplier { get; private set; } = 2.0f;

    /// <summary>
    /// Build multiplier for bad health condition.
    /// Results in ~1 seizure every 3 rounds.
    /// </summary>
    [DataField]
    public float ConditionBadMultiplier { get; private set; } = 3.33f;

    /// <summary>
    /// Build multiplier for critical health condition.
    /// Results in near-guaranteed seizure after 1 hour critical.
    /// </summary>
    [DataField]
    public float ConditionCriticalMultiplier { get; private set; } = 10.0f;

    // =| MIND SHIELD MULTIPLIERS |=

    /// <summary>
    /// Severity multiplier for entities that started with mindshields (normal severity).
    /// </summary>
    [DataField]
    public float StartedMindShieldedMultiplier { get; private set; } = 1.0f;

    /// <summary>
    /// Severity multiplier for entities that got mindshielded mid-round (more severe effects).
    /// </summary>
    [DataField]
    public float MidRoundMindShieldedMultiplier { get; private set; } = 2.5f;

    // =| EVERYTHING ELSE |=

    /// <summary>
    /// Current seizure meter accumulation (0-1 scale).
    /// Builds toward SeizureThreshold to trigger seizures.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float SeizureBuild;

    /// <summary>
    /// Whether this entity currently has a mind shield implant.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsMindShielded;

    /// <summary>
    /// Whether this entity started with a mind shield at round start.
    /// Used to determine severity levels those who are roundstart (command/sec/etc) mindshielded get normal effects,
    /// those who get mindshielded midround get more severe effects.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool StartedMindShielded;

    /// <summary>
    /// Whether the StartedMindShielded flag has been checked and set. Used to ensure it is only set once.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool StartedMindShieldedChecked;

    /// <summary>
    /// Time until next migraine episode (in seconds).
    /// </summary>
    public float NextMigraineTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan MigrainePreventedUntil;

    /// <summary>
    /// Time until next seizure chance roll (3 second intervals).
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float NextSeizureRollTime = 3f;

    // =| API STUFF |=

    /// <summary>
    /// Modifies the seizure meter by the specified amount.
    /// </summary>
    /// <param name="amount">Amount to modify (positive increases, negative decreases)</param>
    public void ModifySeizureBuild(float amount)
    {
        var newBuild = SeizureBuild + amount;
        SeizureBuild = MathF.Max(0f, MathF.Min(SeizureThreshold * 10f, newBuild));
    }


}
