// ReSharper disable CheckNamespace

using Content.Shared._Starlight.Maps;
using Robust.Shared.Map.Components;

namespace Content.Shared.Silicons.StationAi;

public abstract partial class SharedStationAiSystem
{
    [Dependency] private SharedGridAccessSystem _gridAccess = default!;

    /// <summary>
    ///     Returns whether <paramref name="targetGrid"/> is accessible from the grid that contains the AI core for <paramref name="user"/>.
    ///     Returns false when <paramref name="targetGrid"/> is not a grid, <paramref name="user"/> is not a held Station AI, no core exists, or the core has no grid.
    /// </summary>
    /// <param name="user">The entity that holds the Station AI.</param>
    /// <param name="targetGrid">The grid to check.</param>
    /// <returns>True when the target grid is accessible; otherwise, false.</returns>
    public bool CanAccessGrid(Entity<StationAiHeldComponent?> user, EntityUid? targetGrid)
    {
        Resolve(user, ref user.Comp);

        if (targetGrid is not { } target || !HasComp<MapGridComponent>(target))
            return false;

        if (user.Comp is null || !TryGetCore(user.Owner, out var core) || core.Comp is null)
            return false;

        var sourceGrid = Transform(core.Owner).GridUid;
        return sourceGrid is { } source && _gridAccess.CanAccess((source, null), (target, null));
    }
}
