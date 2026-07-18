using Content.Server.Engineering.Components;
using Content.Server.Stack;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DoAfter;
using Content.Shared.Engineering;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Engineering.EntitySystems
{
    [UsedImplicitly]
    public sealed partial class SpawnAfterInteractSystem : EntitySystem
    {
        [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private StackSystem _stackSystem = default!;
        [Dependency] private TurfSystem _turfSystem = default!;
        [Dependency] private SharedTransformSystem _transform = default!;
        [Dependency] private SharedMapSystem _maps = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SpawnAfterInteractComponent, AfterInteractEvent>(HandleAfterInteract);
            SubscribeLocalEvent<SpawnAfterInteractComponent, SpawnAfterInteractDoAfterEvent>(OnDoAfter);
        }

        private void HandleAfterInteract(EntityUid uid, SpawnAfterInteractComponent component, AfterInteractEvent args)
        {
            if (!args.CanReach && !component.IgnoreDistance)
                return;
            if (string.IsNullOrEmpty(component.Prototype))
                return;

            var gridUid = _transform.GetGrid(args.ClickLocation);
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                return;
            if (!_maps.TryGetTileRef(gridUid.Value, grid, args.ClickLocation, out var tileRef))
                return;

            if (tileRef.Tile.IsEmpty || _turfSystem.IsTileBlocked(tileRef, CollisionGroup.MobMask))
                return;

            if (component.DoAfterTime > 0)
            {
                var doAfterArgs = new DoAfterArgs(
                    EntityManager,
                    args.User,
                    component.DoAfterTime,
                    new SpawnAfterInteractDoAfterEvent(GetNetCoordinates(args.ClickLocation)),
                    uid)
                {
                    BreakOnMove = true,
                };

                _doAfterSystem.TryStartDoAfter(doAfterArgs);
                return;
            }

            TryCompleteSpawn(uid, component, args.ClickLocation);
        }

        private void OnDoAfter(EntityUid uid, SpawnAfterInteractComponent component, SpawnAfterInteractDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled)
                return;

            if (!TryCompleteSpawn(uid, component, GetCoordinates(args.ClickLocation)))
                return;

            args.Handled = true;
        }

        private bool TryCompleteSpawn(EntityUid uid, SpawnAfterInteractComponent component, EntityCoordinates clickLocation)
        {
            if (string.IsNullOrEmpty(component.Prototype))
                return false;

            var gridUid = _transform.GetGrid(clickLocation);
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                return false;
            if (!_maps.TryGetTileRef(gridUid.Value, grid, clickLocation, out var tileRef))
                return false;

            if (tileRef.Tile.IsEmpty || _turfSystem.IsTileBlocked(tileRef, CollisionGroup.MobMask))
                return false;

            if (TryComp<StackComponent>(uid, out var stackComp)
                && component.RemoveOnInteract && !_stackSystem.TryUse((uid, stackComp), 1))
            {
                return false;
            }

            Spawn(component.Prototype, clickLocation.SnapToGrid(grid));

            if (component.RemoveOnInteract && stackComp == null)
                TryQueueDel(uid);

            return true;
        }
    }
}
