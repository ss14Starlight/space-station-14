using System.Numerics;
using Content.Shared.Random.Rules;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Sound;

/// <summary>
/// Ambience that used as short random sound effects that play once when rule is true. Used for things like maintenance hum, random station noises, etc.
/// </summary>
[Prototype]
public sealed partial class AmbientOneShotPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// The sound to play when the rule is true.
    /// </summary>
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    /// <summary>
    /// The rule that determines when this sound should be played.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<RulesPrototype> Rules = string.Empty;

    /// <summary>
    /// Seconds between two sounds, picked at random between these bounds.
    /// </summary>
    [DataField]
    public Vector2 Interval = new(30f, 90f);

    /// <summary>
    /// Distance in tiles from the player at which the sound plays, picked at random between these bounds.
    /// </summary>
    [DataField]
    public Vector2 Distance = new(3f, 7f);
}
