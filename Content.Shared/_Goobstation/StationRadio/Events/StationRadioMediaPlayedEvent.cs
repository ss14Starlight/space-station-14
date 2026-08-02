using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Goobstation.StationRadio.Events;

[Serializable, NetSerializable]
public sealed class StationRadioMediaPlayedEvent : EntityEventArgs
{
    public SoundPathSpecifier MediaPlayed { get; }
    public TimeSpan PlayOffset;
    public StationRadioMediaPlayedEvent(SoundPathSpecifier Media, TimeSpan playOffset = default)
    {
        MediaPlayed = Media;
        PlayOffset = playOffset;
    }
}
