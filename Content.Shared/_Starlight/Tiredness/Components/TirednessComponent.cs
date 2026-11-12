using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Tiredness.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class TirednessComponent : Component
{
    /// <summary>
    /// Accumulated tiredness, in seconds, clamped to 30 minutes.
    /// </summary>
    [DataField, AutoNetworkedField] public TimeSpan CurrentTiredness = TimeSpan.Zero;

    /// <summary>
    /// Maximum sleepiness duration that may be accumulated, in seconds.
    /// </summary>
    [DataField] public TimeSpan MaximumSleepiness = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Additional sleepiness duration applied per real second at maximum tiredness, before natural decay.
    /// </summary>
    [DataField] public float MaximumSleepinessSecondsPerSecond = 1.25f;

    /// <summary>
    /// Multiplier for tiredness recovery per second while sleeping.
    /// </summary>
    [DataField] public float SleepRecoveryMultiplier = 15f;

    /// <summary>
    /// Movement-speed multiplier applied at severe tiredness.
    /// </summary>
    [DataField] public float SevereTirednessMovementSpeedModifier = 0.95f;

    /// <summary>
    /// Movement-speed multiplier applied at maximum tiredness.
    /// </summary>
    [DataField] public float MaximumTirednessMovementSpeedModifier = 0.75f;

    /// <summary>
    /// Whether tiredness-generated sleepiness is allowed to induce sleep.
    /// </summary>
    [DataField] public bool CanInduceSleep = false;

    /// <summary>
    /// The sleep action granted while the entity is inside a container.
    /// </summary>
    [DataField, AutoNetworkedField] public EntityUid? SleepAction;
}
