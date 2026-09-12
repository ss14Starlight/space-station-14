using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Goobstation.StationRadio.Events; // Starlight - _Goob -> _Goobstation

[Serializable, NetSerializable]
public sealed class StationRadioMediaPlayedEvent : EntityEventArgs
{
    public SoundPathSpecifier MediaPlayed { get; }
    public TimeSpan StartTime; // Starlight - Add Station Radio Resume Play
    public StationRadioMediaPlayedEvent(SoundPathSpecifier media, TimeSpan startTime = default) // Starlight - Add Station Radio Resume Play
    {
        MediaPlayed = media;
        StartTime = startTime; // Starlight - Add Station Radio Resume Play
    }
}
