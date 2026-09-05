using Robust.Shared.Audio;

namespace Content.Shared._Starlight.StoryGen;

[RegisterComponent]

/// <summary>
/// The owner of a Book of Grudges
/// </summary>
public sealed partial class GrudgeBearerComponent : Component
{
    public EntityUid? Book;

    /// <summary>
    /// Probability (0.0-1.0) that the bearer will decide to begrudge someone.
    /// 0.0 = no grudges ever, 1.0 = everyone gets a grudgin'.
    /// </summary>
    [DataField]
    public float judginess = 1.0f;

    [DataField(required: true)]
    public SoundSpecifier? GrudgeSound;

    [DataField(required: true)]
    public SoundSpecifier? ErrorSound;
}
