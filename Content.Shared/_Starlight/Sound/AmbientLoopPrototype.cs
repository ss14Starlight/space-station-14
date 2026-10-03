using Content.Shared.Random.Rules;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Sound;

/// <summary>
/// Basically just same as Ambient but it's looped audio while rule is true. Used for station hum, maintenance hum, etc.
/// </summary>
[Prototype]
public sealed partial class AmbientLoopPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Priority of this loop. Higher priority loops will override lower priority loops.
    /// </summary>
    [DataField]
    public int Priority;

    /// <summary>
    /// The sound to play while the rule is true.
    /// </summary>
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    /// <summary>
    /// The rule that determines when this loop should be played.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<RulesPrototype> Rules = string.Empty;

    /// <summary>
    /// Seconds to fade this loop in and out.
    /// </summary>
    [DataField]
    public float FadeTime = 4f;
}
