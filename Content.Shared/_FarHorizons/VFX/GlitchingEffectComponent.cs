using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.VFX;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GlitchingEffectComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool Animated;

    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float Intensity = 0;
    
    [ViewVariables, AutoNetworkedField] public TimeSpan StartAt = TimeSpan.MaxValue;
    [ViewVariables, AutoNetworkedField] public TimeSpan FinishAt = TimeSpan.MaxValue;
    [ViewVariables, AutoNetworkedField] public TimeSpan RampDuration = TimeSpan.FromSeconds(1);
}