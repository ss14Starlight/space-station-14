using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.StationRadio.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class StationRadioMixerComponent : Component
{
    /// <summary>
    /// Whether the mixer needs power to change anything.
    /// </summary>
    [DataField]
    public bool RequiresPower = true;
}
