using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Maps;

/// <summary>
///     Describes which grids can be accessed from this grid.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GridAccessComponent : Component
{
    /// <summary>
    ///     Grids accessible from this grid. The owning grid is always included.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> AccessibleGrids = new();
}
