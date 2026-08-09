using Content.Server.Construction.Components;
using Content.Shared._Omu.Common.Construction;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Omu.Construction
{
    public sealed partial class OmuConstructionBlockerSystem : EntitySystem
    {
        [Dependency] private readonly SharedMapSystem _map = default!;
        [Dependency] private readonly EntityLookupSystem _lookupSystem = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<ConstructionComponent, BigBuildAttemptEvent>(CanBigBuild);
        }

        private void CanBigBuild(EntityUid uid, ConstructionComponent component, ref BigBuildAttemptEvent args)
        {
            var xform = Transform(uid);

            if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            {
                args.Cancelled = true;
                return;
            }
            var buildPos = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);

            var positions = new List<EntityCoordinates> // todo this is shit and manually makes a 3x3 square to check. Probably could be smarter.
            {
                _map.ToCenterCoordinates(grid, buildPos + new Vector2i(-1,  1)),
                _map.ToCenterCoordinates(grid, buildPos + new Vector2i( 0,  1)),
                _map.ToCenterCoordinates(grid, buildPos + new Vector2i( 1,  1)),
                _map.ToCenterCoordinates(grid, buildPos + new Vector2i(-1,  0)),
                // _map.ToCenterCoordinates(grid, buildPos), // This is the center. Don't actually check the machine to intersect with... itself.
                _map.ToCenterCoordinates(grid, buildPos + new Vector2i( 1,  0)),
                _map.ToCenterCoordinates(grid, buildPos + new Vector2i(-1, -1)),
                _map.ToCenterCoordinates(grid, buildPos + new Vector2i( 0, -1)),
                _map.ToCenterCoordinates(grid, buildPos + new Vector2i( 1, -1)),
            };
            var intersecting = false;
            foreach (var coords in positions)
            {
                if (_lookupSystem.AnyEntitiesIntersecting(coords, LookupFlags.Static))
                    intersecting = true;
            }

            // if anything intersects in the 3x3 space cancel construction push markup that there's not enough space.
            if (intersecting && args.User != null)
            {
                _popup.PopupEntity(Loc.GetString("big-machine-build-no-room"), uid, args.User.Value);
                args.Cancelled = true;
            }
            // not intersecting
        }

    }
}
