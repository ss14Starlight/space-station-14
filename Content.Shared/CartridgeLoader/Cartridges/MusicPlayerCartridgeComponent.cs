using Robust.Shared.GameObjects;

namespace Content.Shared.CartridgeLoader.Cartridges;

/// <summary>
/// Component that indicates a PDA cartridge as containing the Music Player program
/// </summary>
[RegisterComponent]
public sealed partial class MusicPlayerCartridgeComponent : Component
{
    /// <summary>
    /// The currently playing track path
    /// </summary>
    [DataField]
    public string? CurrentTrack { get; set; }

    /// <summary>
    /// Whether music is currently playing
    /// </summary>
    [DataField]
    public bool IsPlaying { get; set; }

    /// <summary>
    /// Volume level (0.0 to 1.0)
    /// </summary>
    [DataField]
    public float Volume { get; set; } = 0.5f;

    /// <summary>
    /// Whether the current track should loop
    /// </summary>
    [DataField]
    public bool IsLooping { get; set; }
}
