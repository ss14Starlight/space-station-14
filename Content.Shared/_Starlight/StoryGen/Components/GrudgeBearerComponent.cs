using Robust.Shared.Audio;

namespace Content.Shared._Starlight.StoryGen;

[RegisterComponent]

/// <summary>
/// The owner of a Book of Grudges
/// </summary>
public sealed partial class GrudgeBearerComponent : Component
{
    public EntityUid? Book;

    [DataField]
    public float judginess = 1.0f;

    [DataField(required: true)]
    public SoundSpecifier? GrudgeSound;

    [DataField(required: true)]
    public SoundSpecifier? ErrorSound;
}
