using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Sound;

/// <summary>
/// Volume categories the client can scale world sounds by, each with its own options slider.
/// </summary>
public enum SoundCategory : byte
{
    Effects,
    Footsteps,
    Handling,
    Combat,
    Voice,
    Announcement,

    /// <summary>
    /// Left alone: positional music (jukebox, radio) and anything another slider already covers.
    /// </summary>
    Ignore,
}

[Prototype]
public sealed partial class SoundCategoryPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// The sound category this prototype represents.
    /// </summary>
    [DataField(required: true)]
    public SoundCategory Category;

    /// <summary>
    /// Path prefixes, e.g. <c>/Audio/Weapons/</c>.
    /// </summary>
    [DataField]
    public List<string> Paths = new();

    /// <summary>
    /// Every file of these sound collections.
    /// </summary>
    [DataField]
    public List<ProtoId<SoundCollectionPrototype>> Collections = new();
}
