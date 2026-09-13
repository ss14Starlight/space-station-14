using Robust.Shared.GameStates;
using Robust.Shared.Audio; // Starlight - Add Station Radio Resume Play

namespace Content.Shared._Goobstation.StationRadio.Components; // Starlight - _Goob -> _Goobstation

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationRadioServerComponent : Component
    // Starlight - Add the ability for Station Radios to resume play.
{
    /// <summary>
    /// The song currently being broadcasted.
    /// Null if nothing is playing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundPathSpecifier? CurrentSong;

    /// <summary>
    /// For determining where the sound should resume.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? PlaybackStartTime;
}
    // Starlight - End
