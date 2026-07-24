using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Goob.StationRadio.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VinylComponent : Component
{
    /// <summary>
    /// What song should be played when the vinyl is played
    /// </summary>
    [DataField] public SoundPathSpecifier? Song;
}
