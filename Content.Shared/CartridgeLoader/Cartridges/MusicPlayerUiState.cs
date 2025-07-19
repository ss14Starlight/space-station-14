using Robust.Shared.Serialization;
using Robust.Shared.GameStates;
using Robust.Shared.GameObjects;

namespace Content.Shared.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class MusicPlayerUiState : BoundUserInterfaceState
{
    public string CurrentTrack { get; }
    public float Position { get; }
    public float Duration { get; }
    public bool IsPlaying { get; }
    public float Volume { get; }
    public bool ShuffleEnabled { get; }

    public MusicPlayerUiState(string currentTrack, float position, float duration, bool isPlaying, float volume, bool shuffleEnabled)
    {
        CurrentTrack = currentTrack;
        Position = position;
        Duration = duration;
        IsPlaying = isPlaying;
        Volume = volume;
        ShuffleEnabled = shuffleEnabled;
    }
}