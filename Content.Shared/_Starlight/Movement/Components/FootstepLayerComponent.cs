using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Movement.Components;

/// <summary>
/// Plays additional "layered" footstep sound effect.
/// Used in mag boots for metal sound as example.
/// </summary>
[RegisterComponent]
public sealed partial class FootstepLayerComponent : Component
{
    /// <summary>
    /// Sound effect which will be played.
    /// </summary>
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    /// <summary>
    /// Sound should be active to play additional sound effect.
    /// </summary>
    [DataField]
    public bool RequireActive;
}
