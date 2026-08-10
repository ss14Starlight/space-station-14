using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Goobstation.StationRadio.Events; // Starlight - _Goob -> _Goobstation

[Serializable, NetSerializable]
public sealed class StationRadioMediaPlayedEvent : EntityEventArgs
{
    public SoundPathSpecifier MediaPlayed { get; }
    public TimeSpan PlayOffset; // Starlight - Add Station Radio Resume Play
    public StationRadioMediaPlayedEvent(SoundPathSpecifier Media, TimeSpan playOffset = default) // Starlight - Add Station Radio Resume Play
    {
        MediaPlayed = Media;
        PlayOffset = playOffset; // Starlight - Add Station Radio Resume Play
    }
}
