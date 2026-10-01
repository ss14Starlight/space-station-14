using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.StationRadio.Events;

[Serializable, NetSerializable]
public sealed class StationRadioMediaPlayedEvent : EntityEventArgs
{
    public SoundPathSpecifier MediaPlayed { get; }
    public TimeSpan StartTime;
    public StationRadioMediaPlayedEvent(SoundPathSpecifier media, TimeSpan startTime = default)
    {
        MediaPlayed = media;
        StartTime = startTime;
    }
}
