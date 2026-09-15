using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Goobstation.StationRadio.Components; // Starlight - _Goob -> _Goobstation

[RegisterComponent, NetworkedComponent]
public sealed partial class VinylComponent : Component
{
    /// <summary>
    /// What song should be played when the vinyl is played
    /// </summary>
    [DataField] public SoundPathSpecifier? Song;
}
