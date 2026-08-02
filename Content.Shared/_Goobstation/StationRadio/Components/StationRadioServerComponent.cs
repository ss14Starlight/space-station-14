using Robust.Shared.GameStates;
using Robust.Shared.Audio;

namespace Content.Shared._Goobstation.StationRadio.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationRadioServerComponent : Component
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
