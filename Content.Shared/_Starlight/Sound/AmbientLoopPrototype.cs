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

    [DataField]
    public int Priority;

    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    [DataField(required: true)]
    public ProtoId<RulesPrototype> Rules = string.Empty;

    /// <summary>
    /// Seconds to fade this loop in and out.
    /// </summary>
    [DataField]
    public float FadeTime = 4f;
}
