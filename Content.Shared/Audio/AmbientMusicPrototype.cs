using Content.Shared.Random;
using Content.Shared.Random.Rules;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Audio;

/// <summary>
/// Attaches a rules prototype to sound files to play ambience.
/// </summary>
[Prototype]
public sealed partial class AmbientMusicPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Traditionally you'd prioritise most rules to least as priority but in our case we'll just be explicit.
    /// </summary>
    [DataField("priority")]
    public int Priority = 0;

    /// <summary>
    /// Can we interrupt this ambience for a better prototype if possible?
    /// </summary>
    [DataField("interruptable")]
    public bool Interruptable = false;

    /// <summary>
    /// Do we fade-in. Useful for songs.
    /// </summary>
    [DataField("fadeIn")]
    public bool FadeIn;

    [DataField("sound", required: true)]
    public SoundSpecifier Sound = default!;

    [DataField("rules", required: true)]
    public ProtoId<RulesPrototype> Rules = string.Empty;
}
