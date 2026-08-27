using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.Maps;

/// <summary>
///     Manages access from one map grid to other map grids.
/// </summary>
public sealed class SharedGridAccessSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GridInitializeEvent>(OnGridInitialize);
    }

    /// <summary>
    ///     Returns whether <paramref name="targetGrid"/> can be accessed from <paramref name="sourceGrid"/>.
    /// </summary>
    public bool CanAccess(EntityUid sourceGrid, EntityUid targetGrid) =>
        TryComp<GridAccessComponent>(sourceGrid, out var access) &&
        access.AccessibleGrids.Contains(targetGrid);

    /// <summary>
    ///     Adds a grid to the access list of another grid.
    /// </summary>
    public bool AddAccessibleGrid(EntityUid sourceGrid, EntityUid targetGrid)
    {
        if (!HasComp<MapGridComponent>(sourceGrid) || !HasComp<MapGridComponent>(targetGrid))
            return false;

        var access = EnsureComp<GridAccessComponent>(sourceGrid);
        if (!access.AccessibleGrids.Add(targetGrid))
            return false;

        Dirty(sourceGrid, access);
        return true;
    }

    /// <summary>
    ///     Removes a grid from the access list of another grid. A grid always retains access to itself.
    /// </summary>
    public bool RemoveAccessibleGrid(EntityUid sourceGrid, EntityUid targetGrid)
    {
        if (sourceGrid == targetGrid || !TryComp<GridAccessComponent>(sourceGrid, out var access))
            return false;

        if (!access.AccessibleGrids.Remove(targetGrid))
            return false;

        Dirty(sourceGrid, access);
        return true;
    }

    private void OnGridInitialize(GridInitializeEvent ev)
    {
        if (!HasComp<MapGridComponent>(ev.EntityUid))
            return;

        var access = EnsureComp<GridAccessComponent>(ev.EntityUid);
        if (access.AccessibleGrids.Add(ev.EntityUid))
            Dirty(ev.EntityUid, access);
    }
}
