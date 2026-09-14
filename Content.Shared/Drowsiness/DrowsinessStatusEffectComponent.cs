using System.Numerics;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Drowsiness;

/// <summary>
/// Exists for use as a status effect. Adds a shader to the client that scales with the effect duration.
/// Use only in conjunction with <see cref="StatusEffectComponent"/>, on the status effect entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class DrowsinessStatusEffectComponent : Component
{
    // Starlight
    [DataField]
    public bool SleepIncident = true;

    // Starlight
    [DataField]
    public bool KnockdownIncident = false;

    /// <summary>
    /// The random time between knockdown incidents, (min, max).
    /// </summary>
    [DataField]
    public Vector2 TimeBetweenIncidents = new(5f, 60f);

    /// <summary>
    /// The duration of knockdown incidents, (min, max).
    /// </summary>
    [DataField]
    public Vector2 DurationOfIncident = new(2, 5);

    /// <summary>
    /// The random time between sleepiness incidents, (min, max).
    /// </summary>
    [DataField]
    public Vector2 SleepinessTimeBetweenIncidents = new(1, 2);

    /// <summary>
    /// The amount of sleepiness added by an incident, (min, max).
    /// </summary>
    [DataField]
    public Vector2 SleepinessIncrement = new(2, 3);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextIncidentTime = TimeSpan.Zero;
}
