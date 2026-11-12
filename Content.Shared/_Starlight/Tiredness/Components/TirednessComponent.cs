using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Tiredness.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class TirednessComponent : Component
{
    [DataField, AutoNetworkedField] public TimeSpan CurrentTiredness = TimeSpan.Zero;

    [DataField] public TimeSpan MaximumSleepiness = TimeSpan.FromSeconds(20);

    // At full tiredness, the net sleepiness accumulation is 0.25 seconds per second after natural decay.
    [DataField] public float MaximumSleepinessSecondsPerSecond = 1.25f;

    [DataField] public float SleepRecoveryMultiplier = 15f;

    [DataField] public float SevereTirednessMovementSpeedModifier = 0.95f;

    [DataField] public float MaximumTirednessMovementSpeedModifier = 0.75f;

    [DataField] public bool CanInduceSleep = false;

    [DataField, AutoNetworkedField] public EntityUid? SleepAction;
}
