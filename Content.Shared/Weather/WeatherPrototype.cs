using Content.Shared.EntityEffects; // SL
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Weather;

[Prototype]
public sealed partial class WeatherPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [ViewVariables(VVAccess.ReadWrite), DataField("sprite")] // SL - No "Required" anymore.
    public SpriteSpecifier? Sprite = default!;

    [ViewVariables(VVAccess.ReadWrite), DataField("color")]
    public Color? Color;

    /// <summary>
    /// Sound to play on the affected areas.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("sound")]
    public SoundSpecifier? Sound;

    /// <summary>
    /// Starlight
    /// List of effects that should be applied.
    /// </summary>
    [DataField]
    public List<EntityEffect> Effects = default!;

    /// <summary>
    /// Starlight
    /// Will affect only SpaceTiles.
    /// </summary>
    [DataField]
    public bool OnlySpace = false;

    /// <summary>
    /// Starlight
    /// Should weather check weather tile checks?
    /// </summary>
    [DataField]
    public bool CheckTileWeather = true;

    /// <summary>
    /// Starlight
    /// Put an Overlay on the Parallax.
    /// </summary>
    [DataField]
    public string? Parallax;
}
