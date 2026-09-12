using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Sleepiness.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SleepinessStatusEffectComponent : Component
{
    [DataField]
    public TimeSpan SleepThreshold = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan RecoveryThreshold = TimeSpan.FromSeconds(10);

    [DataField]
    public ProtoId<ReagentPrototype>? SleepInductionReagent;

    [DataField]
    public FixedPoint2 SleepInductionThreshold = FixedPoint2.Zero;

    [DataField, AutoNetworkedField]
    public bool WakeRequested;

    [AutoNetworkedField]
    public TimeSpan SleepResistance = TimeSpan.Zero;
}
