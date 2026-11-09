// ReSharper disable once CheckNamespace
namespace Content.Shared.Audio;

public sealed partial class AmbientMusicPrototype
{
    /// <summary>
    /// Extra silence after one of this ambience's tracks finishes, on top of the usual gap. Keeps fallback ambience
    /// such as the general hallway tracks from playing as often as the area-specific ones.
    /// </summary>
    [DataField]
    public TimeSpan ExtraDelay = TimeSpan.Zero;
}
