using Robust.Shared.Map.Components;

namespace Content.Shared._Starlight.Maps;

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
    public bool CanAccess(Entity<MapGridComponent?> sourceGrid, Entity<MapGridComponent?> targetGrid)
    {
        Resolve(sourceGrid, ref sourceGrid.Comp);
        Resolve(targetGrid, ref targetGrid.Comp);

        return TryComp<GridAccessComponent>(sourceGrid.Owner, out var access) && access.AccessibleGrids.Contains(targetGrid.Owner);
    }

    /// <summary>
    ///     Adds a grid to the access list of another grid.
    /// </summary>
    public bool AddAccessibleGrid(Entity<MapGridComponent?> sourceGrid, Entity<MapGridComponent?> targetGrid)
    {
        Resolve(sourceGrid, ref sourceGrid.Comp);
        Resolve(targetGrid, ref targetGrid.Comp);

        if (sourceGrid.Comp is null || targetGrid.Comp is null)
            return false;

        var access = EnsureComp<GridAccessComponent>(sourceGrid.Owner);
        if (!access.AccessibleGrids.Add(targetGrid.Owner))
            return false;

        Dirty(sourceGrid.Owner, access);
        return true;
    }

    /// <summary>
    ///     Removes a grid from the access list of another grid. A grid always retains access to itself.
    /// </summary>
    public bool RemoveAccessibleGrid(Entity<MapGridComponent?> sourceGrid, Entity<MapGridComponent?> targetGrid)
    {
        Resolve(sourceGrid, ref sourceGrid.Comp);
        Resolve(targetGrid, ref targetGrid.Comp);

        if (sourceGrid.Owner == targetGrid.Owner || sourceGrid.Comp is null || targetGrid.Comp is null ||
            !TryComp<GridAccessComponent>(sourceGrid.Owner, out var access))
            return false;

        if (!access.AccessibleGrids.Remove(targetGrid.Owner))
            return false;

        Dirty(sourceGrid.Owner, access);
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
