using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.StationRadio;

[Serializable, NetSerializable]
public record struct StationRadioTrack(SoundPathSpecifier Sound, TimeSpan StartTime, float Gain = 1f)
{
    public bool SamePlayback(StationRadioTrack other) =>
        Sound.Path == other.Sound.Path && StartTime == other.StartTime;
}
