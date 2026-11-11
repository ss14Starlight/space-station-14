// SPDX-FileCopyrightText: 2026 TrixxedHeart <46364955+TrixxedBit@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Funkystation.Traits.Assorted;

/// <summary>
/// Component for entities with Neuroaversion trait.
/// When combined with mindshield, causes migraines and seizures.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class NeuroAversionComponent : Component
{
    /// <summary>
    /// Time range between migraines (min, max).
    /// Default: 8-20 minutes
    /// </summary>
    [DataField]
    public (TimeSpan Min, TimeSpan Max) TimeBetweenMigraines = (TimeSpan.FromMinutes(8), TimeSpan.FromMinutes(20));

    /// <summary>
    /// Duration range for migraines (min, max).
    /// </summary>
    [DataField]
    public (TimeSpan Min, TimeSpan Max) MigraineDuration = (TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60));

    /// <summary>
    /// Time until next migraine.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan NextMigraineTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan MigrainePreventedUntil;

    /// <summary>
    /// Current seizure build level (0-100).
    /// Represents the percentage chance to have a seizure on each roll (every 3 seconds).
    /// Value is rounded to 4 decimal places to avoid floating point artifacts.
    /// </summary>
    [DataField, AutoNetworkedField]
    private float _seizureBuild;
    public float SeizureBuild
    {
        get => _seizureBuild;
        set => _seizureBuild = MathF.Round(MathF.Max(0f, MathF.Min(value, SeizureThreshold * 2f)), 4);
    }

    /// <summary>
    /// Seizure threshold (100 = full meter).
    /// </summary>
    [DataField]
    public float SeizureThreshold = 100f;

    /// <summary>
    /// Base passive seizure build per second when in good condition.
    /// Build percentage IS the seizure chance per roll (every 3 sec).
    /// 0.0015 = 0.0015%/sec
    /// Roundstart (1.0x): ~10% build after 2 hours = 10% chance per roll
    /// Mid-round (3.0x): ~32% build after 2 hours = 32% chance per roll
    /// </summary>
    [DataField]
    public float BaseSeizurePassivePerSec = 0.005f;

    /// <summary>
    /// Residual seizure build after a seizure occurs (percentage of max).
    /// </summary>
    [DataField]
    public float PostSeizureResidual = 10f;

    /// <summary>
    /// Time between seizure hazard checks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan NextSeizureCheckTime = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan SeizureCheckInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Additional hazard per point of seizure build.
    /// </summary>
    [DataField]
    public float BuildHazardFactor = 0.000004f;

    /// <summary>
    /// Whether the entity currently has a mindshield.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsMindShielded;

    /// <summary>
    /// Whether the entity started with a mindshield at round start.
    /// Affects severity multipliers.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool StartedMindShielded;

    /// <summary>
    /// Whether we've checked if they started mindshielded (only check once).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool StartedMindShieldedChecked;

    /// <summary>
    /// If true, seizure rolls and build accumulation are paused (such as while psicodine is metabolized).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SeizurePaused;

    // Condition multipliers based on health state
    [DataField]
    public float ConditionGoodMultiplier = 1.0f;

    [DataField]
    public float ConditionOkayMultiplier = 1.5f;

    [DataField]
    public float ConditionBadMultiplier = 4.0f;

    [DataField]
    public float ConditionCriticalMultiplier = 25.0f;

    // Severity multipliers based on when mindshield was applied
    [DataField]
    public float StartedMindShieldedMultiplier = 1.0f;

    [DataField]
    public float MidRoundMindShieldedMultiplier = 3.5f;

    /// <summary>
    /// Duration of seizures triggered by neuroaversion.
    /// </summary>
    [DataField]
    public TimeSpan NeuroAversionSeizureDuration = TimeSpan.FromSeconds(10);
}
