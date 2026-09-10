using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Sleepiness.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SleepinessStatusEffectComponent : Component
{
    [DataField]
    public TimeSpan SleepThreshold = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan RecoveryThreshold = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public bool SleepTriggered;
}
