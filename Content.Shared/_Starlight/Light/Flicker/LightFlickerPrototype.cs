using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Light.Flicker;

/// <summary>
/// Describes flicker preset
/// </summary>
[Prototype]
public sealed partial class LightFlickerPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Shortest quiet spell between bursts, in seconds.
    /// </summary>
    [DataField]
    public float MinDelay = 10f;

    /// <summary>
    /// Longest quiet spell between bursts, in seconds.
    /// </summary>
    [DataField]
    public float MaxDelay = 30f;

    /// <summary>
    /// Fewest times the light drops out in one burst.
    /// </summary>
    [DataField]
    public int MinBlips = 1;

    /// <summary>
    /// Most times the light drops out in one burst.
    /// </summary>
    [DataField]
    public int MaxBlips = 3;

    /// <summary>
    /// Shortest drop out, or gap between drop outs, in seconds.
    /// </summary>
    [DataField]
    public float MinBlipTime = 0.05f;

    /// <summary>
    /// Longest drop out, or gap between drop outs, in seconds.
    /// </summary>
    [DataField]
    public float MaxBlipTime = 0.2f;

    /// <summary>
    /// Played from the fixture when a burst starts.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound;

    /// <summary>
    /// Chance for a burst to play <see cref="Sound"/>.
    /// </summary>
    [DataField]
    public float SoundChance = 1f;

    /// <summary>
    /// Scales the fixture's light radius while a blip has it flipped on.
    /// </summary>
    [DataField]
    public float RadiusMultiplier = 1f;

    /// <summary>
    /// Scales the fixture's light energy while a blip has it flipped on.
    /// </summary>
    [DataField]
    public float EnergyMultiplier = 1f;
}
