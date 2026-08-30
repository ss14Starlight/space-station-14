// ReSharper disable CheckNamespace

using Content.Shared._Starlight.Maps;
using Robust.Shared.Map.Components;

namespace Content.Shared.Silicons.StationAi;

public abstract partial class SharedStationAiSystem
{
    [Dependency] private SharedGridAccessSystem _gridAccess = default!;

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
