using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Admeme.Jumpscare;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedJumpscareSystem))]
public sealed partial class JumpscaredComponent : Component
{
    /// <summary>
    /// Currently playing jumpscare.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public JumpscareData Jumpscare = null!;

    /// <summary>
    /// Flag to track if the jumpscare started.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public bool DidJumpscare;

    /// <summary>
    /// Time to do jumpscare at.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public TimeSpan JumpscareAt = TimeSpan.Zero;

    /// <summary>
    /// Time that the component will remove itself. This acts as immunity to jumpscares
    /// if <see cref="DidJumpscare"/> is true.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public TimeSpan RemoveSelfAt = TimeSpan.Zero;

    /// <summary>
    /// Whether the target is currently frozen by <see cref="JumpscareData.FreezeTarget"/>.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public bool Frozen;

    /// <summary>
    /// Time that the target stops being frozen, at the end of the hold.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public TimeSpan UnfreezeAt = TimeSpan.Zero;

    /// <summary>
    /// Ref to the entity that did the jumpscare.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public EntityUid Jumpscarer;
}

