using Robust.Shared.Audio; // Starlight - Add Station Radio Resume Play

namespace Content.Shared._Goobstation.StationRadio.Components; // Starlight - _Goob -> _Goobstation

[RegisterComponent] // Starlight edit - did not need to be networked.
public sealed partial class StationRadioServerComponent : Component
    // Starlight - Add the ability for Station Radios to resume play.
{
    /// <summary>
    /// The song currently being broadcasted.
    /// Null if nothing is playing.
    /// </summary>
    [DataField]
    public SoundPathSpecifier? CurrentSong;

    /// <summary>
    /// For determining where the sound should resume.
    /// </summary>
    [DataField]
    public TimeSpan? PlaybackStartTime;
}
    // Starlight - End
