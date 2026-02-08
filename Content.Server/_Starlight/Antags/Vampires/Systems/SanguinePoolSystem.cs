using Content.Server.Fluids.EntitySystems;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Antags.Vampires.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Maps;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Antags.Vampires.Systems;

public sealed class SanguinePoolSystem : SharedSanguinePoolSystem
{
    private static readonly ProtoId<ReagentPrototype> _bloodReagentId = "Blood";
    private const int MaxPoolsProcessedPerUpdate = 64;

    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var processed = 0;
        var query = EntityQueryEnumerator<SanguinePoolComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (processed++ >= MaxPoolsProcessedPerUpdate)
                break;

            if (ShouldForceRevert(uid, xform))
                continue;

            if (comp.TrailPrototype == null)
                continue;

            // Spawn more frequently: once per entered tile (but don't duplicate if the tile already has a blood puddle).
            if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? gridComp))
                continue;

            var tile = _map.CoordinatesToTile(gridUid, gridComp, xform.Coordinates);
            if (comp.LastTrail is { } last && last.Grid == gridUid && last.Tile == tile)
                continue;

            comp.LastTrail = (gridUid, tile);

            var tileCoords = _map.GridTileToLocal(gridUid, gridComp, tile);
            if (_puddle.TryGetPuddle(_map.GetTileRef((gridUid, gridComp), tileCoords), out var puddle))
            {
                var solution = new Solution { Contents = [new ReagentQuantity(_bloodReagentId, 30)] };
                _puddle.TryAddSolution(puddle, solution);
                continue;
            }

            Spawn(comp.TrailPrototype, tileCoords);
        }
    }

    private bool ShouldForceRevert(EntityUid uid, TransformComponent xform)
    {
        var gridUid = xform.GridUid;
        var inSpace = gridUid == null;

        if (!inSpace && gridUid != null)
        {
            if (!TryComp(gridUid.Value, out MapGridComponent? grid) ||
                !_map.TryGetTileRef(gridUid.Value, grid, xform.Coordinates, out var tileRef) ||
                _turf.IsSpace(tileRef))
            {
                inSpace = true;
            }
        }

        if (!inSpace)
            return false;

        if (TryComp<PolymorphedEntityComponent>(uid, out var polymorph))
            _polymorph.Revert((uid, polymorph));

        return true;
    }
}
