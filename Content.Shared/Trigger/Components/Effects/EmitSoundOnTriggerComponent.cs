using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Trigger.Components.Effects;

/// <summary>
/// Will play a sound in PVS range when triggered.
/// If TargetUser is true it will be played at their position.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EmitSoundOnTriggerComponent : BaseXOnTriggerComponent
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

    /// <summary>
    /// Should this sound be predicted for the User?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Predicted;
    #region Starlight
    /// <summary>
    /// Play the sound globally.
    /// Don't use unless you have a good reason.
    /// That said, if you do have that reason, then you can use a stereo sound for this.
    /// Need this here since this doesn't share the same stuff as BaseEmitSound
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Global;
    #endregion Starlight
}
