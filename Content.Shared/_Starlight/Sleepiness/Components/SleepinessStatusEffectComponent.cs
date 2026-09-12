using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Sleepiness.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SleepinessStatusEffectComponent : Component
{
    /// <summary>
    /// Duration of sleepiness required before the entity falls asleep. Defaults to 60 seconds.
    /// </summary>
    [DataField]
    public TimeSpan SleepThreshold = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Remaining sleepiness duration at which recovery automatically wakes the entity. Defaults to 10 seconds.
    /// </summary>
    [DataField]
    public TimeSpan RecoveryThreshold = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Reagent used to induce sleep when the configured induction threshold is reached.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype>? SleepInductionReagent;

    /// <summary>
    /// Minimum amount of the induction reagent required to induce sleep. Defaults to zero, disabling the requirement.
    /// </summary>
    [DataField]
    public FixedPoint2 SleepInductionThreshold = FixedPoint2.Zero;

    /// <summary>
    /// Whether a wake request is pending and should be processed when sleepiness reaches the wake threshold.
    /// Defaults to false.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool WakeRequested;

    /// <summary>
    /// Duration of accumulated sleepiness used to calculate resistance to further sleepiness. Defaults to zero.
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan SleepResistance = TimeSpan.Zero;
}
