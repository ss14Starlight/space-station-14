using System.Numerics;
using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Scripted HoP-line visitor that speaks a job request and drops an ID when activated.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialHoPVisitorComponent : Component
{
    /// <summary>
    /// Sub-goal id that triggers this visitor (speak + drop ID).
    /// </summary>
    [DataField(required: true)]
    public string ActivateOnSubGoal = string.Empty;

    /// <summary>
    /// Job the visitor asks for (player must write this on the ID).
    /// </summary>
    [DataField(required: true)]
    public ProtoId<JobPrototype> RequestedJob;

    /// <summary>
    /// Locale id spoken when activated.
    /// </summary>
    [DataField(required: true)]
    public LocId Dialogue = default!;

    /// <summary>
    /// ID card prototype dropped on the desk.
    /// </summary>
    [DataField]
    public EntProtoId IdCardProto = "PassengerIDCard";

    /// <summary>
    /// Offset from this visitor toward the desk for the dropped ID.
    /// </summary>
    [DataField]
    public Vector2 DeskDropOffset = new(0f, 1.5f);

    [DataField]
    public bool Activated;
}