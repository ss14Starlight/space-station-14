using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Marks a grid as a single-room tutorial template. When loaded for a role,
/// the grid is stamped into N identical copies with gated doors between them.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialRoomTemplateComponent : Component
{
    /// <summary>
    /// Inter-copy gate door prototype. When unset, the
    /// <see cref="TutorialRoomTemplatePrototype.GateDoor"/> is used.
    /// </summary>
    [DataField]
    public EntProtoId GateDoor;

    /// <summary>
    /// Optional override for <see cref="TutorialRoomTemplatePrototype.StampDirection"/>.
    /// Null keeps the prototype default.
    /// </summary>
    [DataField]
    public TutorialRoomDoorSide? StampDirection;

    /// <summary>
    /// Fill stamped grids with breathable atmosphere.
    /// </summary>
    [DataField]
    public bool FillAtmosphere = true;
}
