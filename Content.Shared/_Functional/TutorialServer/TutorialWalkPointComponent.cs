using Robust.Shared.GameStates;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// A spot a <see cref="TutorialMentorMode.Lead"/> mentor walks to and waits at while the
/// curriculum is in that room, so the player has somebody to follow rather than somebody
/// following them.
/// </summary>
/// <remarks>
/// The walking counterpart of <see cref="TutorialHoloPointComponent"/>: one per room, addressed by
/// room index, and placed wherever that room's beat actually happens rather than at its centre.
/// </remarks>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialWalkPointComponent : Component
{
    /// <summary>
    /// Chamber index this point serves (0 = spawn room). Matched against the curriculum's current
    /// <see cref="TutorialGoalData.EnterRoom"/>.
    /// </summary>
    [DataField]
    public int Room;
}
