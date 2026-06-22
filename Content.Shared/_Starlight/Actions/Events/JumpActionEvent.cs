using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Actions.Events;

[Virtual]
public partial class JumpActionEvent : WorldTargetActionEvent
{
    /// <summary>
    /// Distance in tiles to jump. Depending on speed, the distance will be slightly longer than expected
    /// due to friction taking time to slow you down after landing.
    /// </summary>
    [DataField]
    public float Distance = 5f;

    /// <summary>
    /// When true, the target will jump to the pointer's position precisely, including avoiding overshooting the pointer.
    /// When false, the target will jump in the direction of the pointer, but can overshoot or undershoot the pointer.
    /// </summary>
    [DataField]
    public bool ToPointer = true;

    /// <summary>
    /// Flag that determines if the user needs to be standing on a grid to have this jump actually move them.
    /// Effectively prevents using jumping in space when set to true.
    /// </summary>
    [DataField]
    public bool FromGrid = true;

    /// <summary>
    /// Speed of the jump.
    /// </summary>
    [DataField]
    public float Speed = 15F;

    /// <summary>
    /// Sound effect to play when starting the jump.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound = default;

    /// <summary>
    /// Flag that determines if this jump is from a cybernetic implant.
    /// </summary>
    [DataField]
    public bool IsCybernetic = false;

    /// <summary>
    /// Message shown to the user if a jump fails.
    /// </summary>
    [DataField]
    public string JumpFailPopup = "jump-failed-because-off-grid";
}

public sealed partial class JetJumpActionEvent : JumpActionEvent
{
    /// <summary>
    /// Gas usage of one jump.
    /// </summary>
    [DataField]
    public float MoleUsage = 0.24f; // 20x
}
