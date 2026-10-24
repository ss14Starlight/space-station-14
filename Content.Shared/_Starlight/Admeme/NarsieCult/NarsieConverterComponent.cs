using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Admeme.NarsieCult;

/// <summary>
///     Put on an item. Using it on a mob with a mind converts that mind into a Nar'Sie cultist.
/// </summary>
[RegisterComponent]
public sealed partial class NarsieConverterComponent : Component
{
    /// <summary>
    ///     Path used when no sound is supplied (admin verb) and the default for the item.
    /// </summary>
    public const string DefaultSoundPath = "/Audio/_Starlight/Admeme/NarsieCult/convert.ogg";

    /// <summary>
    ///     Played to the converted player (and only them) when conversion happens.
    /// </summary>
    [DataField]
    public SoundSpecifier ConversionSound = new SoundPathSpecifier(DefaultSoundPath);

    /// <summary>
    ///     If true the target must be alive to be converted.
    /// </summary>
    [DataField]
    public bool RequireAlive = true;
}
