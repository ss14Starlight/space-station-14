using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Marks a holopad on a tutorial map that a <see cref="TutorialMentorMode.Holopad"/> mentor can project from.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialHoloPointComponent : Component
{
    /// <summary>
    /// Chamber index this pad serves (0 = spawn room). Matched against the curriculum's
    /// current <c>EnterRoom</c>.
    /// </summary>
    [DataField]
    public int Room;

    /// <summary>
    /// Hologram a <see cref="TutorialCueEffect.Project"/> cue put on this pad. The pad owns it, so
    /// going dark takes it with it: a coach the holopad system does not know about (one a walking
    /// curriculum staged for a single beat) has nothing else that would ever clean her up.
    /// </summary>
    [ViewVariables]
    public EntityUid? Projection;
}

/// <summary>
/// Appearance key for whether a tutorial holopad is currently projecting the coach.
/// </summary>
[Serializable, NetSerializable]
public enum TutorialHoloPointVisuals : byte
{
    Active,
}
