using Robust.Shared.GameStates;
using Robust.Shared.Network;

namespace Content.Shared.Tabletop.Components;

/// <summary>
/// Allows an entity to be dragged around by the mouse. The position is updated for all player while dragging.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TabletopDraggableComponent : Component
{
    // The player dragging the piece
    [ViewVariables, AutoNetworkedField]
    public NetUserId? DraggingPlayer;

    #region Starlight

    /// <summary>
    /// Allows an entity to be dragged without the Tabletop UI being open.
    /// </summary>
    [DataField, AutoNetworkedField] public bool DraggableWithoutTabletop;

    /// <summary>
    /// Allows the entity to drag itself.
    /// </summary>
    [DataField, AutoNetworkedField] public bool AllowDragSelf;

    #endregion
}
