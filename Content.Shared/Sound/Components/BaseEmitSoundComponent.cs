using Robust.Shared.Audio;

namespace Content.Shared.Sound.Components;

/// <summary>
/// Base sound emitter which defines most of the data fields.
/// Accepts both single sounds and sound collections.
/// </summary>
public abstract partial class BaseEmitSoundComponent : Component
{
    /// <summary>
    /// The <see cref="SoundSpecifier"/> to play.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public SoundSpecifier? Sound;

    /// <summary>
    /// Play the sound at the position instead of parented to the source entity.
    /// Useful if the entity is deleted after.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Positional;
    #region Starlight
    /// <summary>
    /// Play the sound globally.
    /// Don't use unless you have a good reason.
    /// That said, if you do have that reason, then you can use a stereo sound for this.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Global;

    /// <summary>
    /// Global sound volume. Defaults to -5f, to not destroy your ears.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float GlobalSound = -5f;
    #endregion Starlight
}
