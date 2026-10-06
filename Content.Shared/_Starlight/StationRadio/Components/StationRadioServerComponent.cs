using Robust.Shared.Audio;

namespace Content.Shared._Starlight.StationRadio.Components;

[RegisterComponent]
public sealed partial class StationRadioServerComponent : Component
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

