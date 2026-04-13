using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.Silicons.Glitching;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GlitchOnIonStormComponent : Component
{
    [DataField, AutoNetworkedField] public TimeSpan Duration = TimeSpan.FromSeconds(10);
    [DataField, AutoNetworkedField] public TimeSpan Ramp = TimeSpan.FromSeconds(1);
}
