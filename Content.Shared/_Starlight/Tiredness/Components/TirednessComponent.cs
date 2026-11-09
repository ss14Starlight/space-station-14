using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Tiredness.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class TirednessComponent : Component
{
    [DataField, AutoNetworkedField] public TimeSpan CurrentTiredness = TimeSpan.Zero;

    [DataField] public TimeSpan MaximumSleepiness = TimeSpan.FromSeconds(20);

    [DataField] public bool CanInduceSleep = false;

    [DataField, AutoNetworkedField] public EntityUid? SleepAction;
}
