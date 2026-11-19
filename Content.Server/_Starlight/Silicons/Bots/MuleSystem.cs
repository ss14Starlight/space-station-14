using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Pathfinding;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Starlight.Silicons.Bots;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Pinpointer;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.StationRecords;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Silicons.Bots;

public sealed partial class MuleSystem : SharedMuleSystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private PathfindingSystem _pathfinding = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly TimeSpan CheckDelay = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan StuckDelay = TimeSpan.FromSeconds(2);
    private const int MaxStuck = 10;
    private static readonly StationRecordKey[] NoKeys = [];

    /// <summary>
    /// Doors further apart than this along the route aren't the same entrance.
    /// </summary>
    private const float EntranceLength = 2.5f;

    /// <summary>
    /// Tiles between the points it steers to along a route.
    /// </summary>
    private const int WaypointSpacing = 6;

    /// <summary>
    /// How hard to look for a route, in tiles searched. Enough for most of a station.
    /// </summary>
    private const int RouteLimit = 20000;

    [SubscribeLocalEvent]
    private void OnUiOpened(Entity<MuleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    [SubscribeLocalEvent]
    private void OnCommand(Entity<MuleComponent> ent, ref MuleCommandMessage args)
    {
        Command(ent, args.Command, args.Actor);
    }

    [SubscribeLocalEvent]
    private void OnSetDestination(Entity<MuleComponent> ent, ref MuleSetDestinationMessage args)
    {
        SetBeacon(ent, GetEntity(args.Beacon), args.Home);
    }

    /// <summary>
    /// Carries out an order from its control panel.
    /// </summary>
    public void Command(Entity<MuleComponent> ent, MuleCommand command, EntityUid? user = null)
    {
        var comp = ent.Comp;
        switch (command)
        {
            case MuleCommand.Power:
                if (!comp.On && !_powerCell.HasCharge(ent.Owner, comp.ChargePerTile))
                {
                    if (user != null)
                        _popup.PopupEntity(Loc.GetString("mule-no-power"), ent, user.Value);
                    break;
                }

                SetOn(ent, !comp.On);
                comp.Home ??= NearestBeacon(ent);
                SetMode(ent, MuleMode.Idle);
                break;
            case MuleCommand.Stop:
                SetMode(ent, MuleMode.Idle);
                break;
            case MuleCommand.Go:
                if (comp.On && comp.Mode is MuleMode.Idle or MuleMode.NoRoute && comp.Destination != null)
                    SetMode(ent, comp.Destination == comp.Home ? MuleMode.GoingHome : MuleMode.Delivering);
                break;
            case MuleCommand.Home:
                if (comp.On && comp.Mode is MuleMode.Idle or MuleMode.NoRoute or MuleMode.Delivering && comp.Home != null)
                {
                    comp.Destination = comp.Home;
                    SetMode(ent, MuleMode.GoingHome);
                }
                break;
            case MuleCommand.Unload:
                Unload(ent);
                break;
            case MuleCommand.AutoReturn:
                comp.AutoReturn = !comp.AutoReturn;
                break;
            case MuleCommand.AutoPickup:
                comp.AutoPickup = !comp.AutoPickup;
                break;
            case MuleCommand.Report:
                comp.Report = !comp.Report;
                break;
        }

        Dirty(ent);
        UpdateUi(ent);
    }

    /// <summary>
    /// Sets its destination or home to one of the station beacons on its grid.
    /// </summary>
    public void SetBeacon(Entity<MuleComponent> ent, EntityUid beacon, bool home)
    {
        if (!GetBeacons(ent).ContainsKey(GetNetEntity(beacon)))
            return;

        if (home)
            ent.Comp.Home = beacon;
        else
            ent.Comp.Destination = beacon;

        Dirty(ent);
        UpdateUi(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MuleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!IsMoving(comp) || now < comp.NextCheck)
                continue;

            comp.NextCheck = now + CheckDelay;
            OpenDoors((uid, comp), xform);
            CheckProgress((uid, comp), xform, now);
        }
    }

    private static bool IsMoving(MuleComponent comp)
    {
        return comp.On && comp.Mode is MuleMode.Delivering or MuleMode.GoingHome or MuleMode.Blocked;
    }

    /// <summary>
    /// Opens the doors ahead that it has access to, or that are the entrance it's allowed through.
    /// </summary>
    private void OpenDoors(Entity<MuleComponent> ent, TransformComponent xform)
    {
        foreach (var door in _lookup.GetEntitiesInRange<DoorComponent>(xform.Coordinates, 1.5f))
        {
            if (door.Comp.State != DoorState.Closed)
                continue;

            if (_door.CanOpen(door, door.Comp, ent))
                _door.TryOpen(door, door.Comp, ent);
            else if (ent.Comp.EntranceDoors.Contains(door))
                _door.StartOpening(door, door.Comp);
        }
    }

    /// <summary>
    /// Uses charge for the distance driven, and buzzes when it's stuck.
    /// </summary>
    private void CheckProgress(Entity<MuleComponent> ent, TransformComponent xform, TimeSpan now)
    {
        var comp = ent.Comp;
        var position = _transform.GetWorldPosition(xform);
        var moved = (position - comp.LastPosition).Length();

        if (moved >= 1f)
        {
            comp.LastPosition = position;
            comp.LastMoved = now;
            if (!_powerCell.TryUseCharge(ent.Owner, comp.ChargePerTile * MathF.Floor(moved)))
            {
                SetOn(ent, false);
                SetMode(ent, MuleMode.Idle);
                return;
            }

            if (comp.Mode == MuleMode.Blocked)
                SetMode(ent, comp.Destination == comp.Home ? MuleMode.GoingHome : MuleMode.Delivering, replan: false);

            comp.Stuck = 0;
            return;
        }

        if (now < comp.LastMoved + StuckDelay)
            return;

        comp.LastMoved = now;
        comp.Stuck++;
        comp.Mode = MuleMode.Blocked;
        Dirty(ent);
        UpdateUi(ent);

        if (comp.Stuck >= MaxStuck)
        {
            comp.Stuck = 0;
            Buzz(ent, comp.AnnoyedSound, "mule-buzz-annoyed");
        }
        else
        {
            Buzz(ent, comp.SighSound, "mule-buzz-sigh");
        }
    }

    /// <summary>
    /// Plans the drive to the destination: up to the beacon, or to the last tile before a door it can't get through.
    /// Fills <see cref="MuleComponent.Waypoints"/> and returns false if there's no way there at all.
    /// </summary>
    public bool PlanRoute(Entity<MuleComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.Destination is not { } beacon || TerminatingOrDeleted(beacon))
            return false;

        // Still on the way along a route planned for this run.
        if (comp.Waypoints.Count > 0)
            return true;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || Transform(beacon).GridUid != gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var ownAccess = _accessReader.FindAccessTags(ent);
        var entranceAccess = ownAccess.Concat(comp.EntranceAccess).ToHashSet();
        var (_, mask) = _physics.GetHardCollision(ent);
        var costs = new Dictionary<Vector2i, float>();
        var doors = new Dictionary<Vector2i, (EntityUid Door, DoorAccess Access)>();

        float TileCost(Vector2i tile)
        {
            if (costs.TryGetValue(tile, out var known))
                return known;

            var cost = TileCostUncached(gridUid, grid, tile, mask, ownAccess, entranceAccess, doors);
            costs[tile] = cost;
            return cost;
        }

        var start = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        var end = _map.CoordinatesToTile(gridUid, grid, Transform(beacon).Coordinates);
        var path = _pathfinding.GetPath(new PathfindingSystem.SimplePathArgs
        {
            Start = start,
            End = end,
            Limit = RouteLimit,
            TileCost = TileCost,
        }).Path;
        if (path == null || path.Count == 0)
            return false;

        // Walk it, cutting it short at a door it may not open. Routes get replanned on the way, so an entrance
        // already picked this run stays the only one.
        var entranceChosen = comp.EntranceDoors.Count > 0;
        var entranceStarted = false;
        var entranceDone = false;
        var entranceLast = Vector2i.Zero;
        var route = new List<Vector2i>();
        foreach (var tile in path)
        {
            if (entranceStarted && (tile - entranceLast).Length > EntranceLength)
                entranceDone = true;

            if (doors.TryGetValue(tile, out var door) && door.Access != DoorAccess.Own && !comp.EntranceDoors.Contains(door.Door))
            {
                // Only the first entrance on the way. Past it, it stops at the next door it can't open itself.
                if (door.Access != DoorAccess.Entrance || entranceChosen || entranceDone)
                    break;

                comp.EntranceDoors.Add(door.Door);
                entranceStarted = true;
                entranceLast = tile;
            }

            if (tile != start)
                route.Add(tile);
        }

        for (var i = WaypointSpacing - 1; i < route.Count - 1; i += WaypointSpacing)
        {
            comp.Waypoints.Add(_map.GridTileToLocal(gridUid, grid, route[i]));
        }

        comp.Waypoints.Add(route.Count > 0 ? _map.GridTileToLocal(gridUid, grid, route[^1]) : xform.Coordinates);
        return true;
    }

    private float TileCostUncached(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        int mask,
        ICollection<ProtoId<AccessLevelPrototype>> ownAccess,
        ICollection<ProtoId<AccessLevelPrototype>> entranceAccess,
        Dictionary<Vector2i, (EntityUid Door, DoorAccess Access)> doors)
    {
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return 0f;

        var cost = 1f;
        foreach (var uid in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (TryComp<DoorComponent>(uid, out var doorComp))
            {
                // Firelocks stand open unless something's wrong, and nobody gets through a shut one.
                if (HasComp<FirelockComponent>(uid))
                {
                    if (doorComp.State == DoorState.Closed)
                        return 0f;

                    continue;
                }

                var access = DoorAccessFor(uid, ownAccess, entranceAccess);
                doors[tile] = (uid, access);
                cost = Math.Max(cost, access switch
                {
                    DoorAccess.Own => 1.5f,
                    DoorAccess.Entrance => 8f,
                    _ => 40f,
                });
                continue;
            }

            if (!TryComp<PhysicsComponent>(uid, out var body) || !body.CanCollide || !body.Hard || (body.CollisionLayer & mask) == 0)
                continue;

            // Thin things on a tile edge, like directional windows, leave room to get past.
            var bounds = _lookup.GetAABBNoContainer(uid, Vector2.Zero, Angle.Zero);
            if (bounds.Width < 0.5f || bounds.Height < 0.5f)
                continue;

            return 0f;
        }

        return cost;
    }

    private DoorAccess DoorAccessFor(EntityUid door, ICollection<ProtoId<AccessLevelPrototype>> ownAccess, ICollection<ProtoId<AccessLevelPrototype>> entranceAccess)
    {
        if (!TryComp<AccessReaderComponent>(door, out var reader) || _accessReader.IsAllowed(ownAccess, NoKeys, door, reader))
            return DoorAccess.Own;

        return _accessReader.IsAllowed(entranceAccess, NoKeys, door, reader) ? DoorAccess.Entrance : DoorAccess.None;
    }

    private enum DoorAccess : byte
    {
        Own,
        Entrance,
        None,
    }

    /// <summary>
    /// How close it needs to get to a point on its route. It stops short of the last one if something is in the way.
    /// </summary>
    public float WaypointRange(Entity<MuleComponent> ent, EntityCoordinates point)
    {
        if (ent.Comp.Waypoints.Count > 1)
            return 0.8f;

        var tile = _turf.GetTileRef(point);
        return tile != null && _turf.IsTileBlocked(tile.Value, CollisionGroup.MidImpassable) ? 1.2f : 0.4f;
    }

    /// <summary>
    /// Called when it gets where it was going: unloads or picks something up, then heads home or waits.
    /// </summary>
    public void Arrive(Entity<MuleComponent> ent)
    {
        var comp = ent.Comp;
        var place = comp.Destination is { } beacon ? BeaconName(beacon) : string.Empty;

        if (TryGetLoad(ent, out var load))
        {
            if (comp.Report)
            {
                Audio.PlayPvs(comp.ArrivedSound, ent);
                Say(ent, Loc.GetString("mule-say-unloading", ("place", place), ("load", load)));
            }

            Unload(ent);
        }
        else if (comp.AutoPickup)
        {
            foreach (var near in _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 1f, LookupFlags.Dynamic))
            {
                if (!TryLoad(ent, near))
                    continue;

                if (comp.Report)
                    Say(ent, Loc.GetString("mule-say-loading", ("place", place), ("load", near)));
                break;
            }
        }

        if (comp.AutoReturn && comp.Home != null && comp.Destination != comp.Home)
        {
            comp.Destination = comp.Home;
            SetMode(ent, MuleMode.GoingHome);
        }
        else
        {
            SetMode(ent, MuleMode.Idle);
        }
    }

    /// <summary>
    /// Called when there's no way to the destination at all.
    /// </summary>
    public void NoRoute(Entity<MuleComponent> ent)
    {
        Buzz(ent, ent.Comp.AnnoyedSound, "mule-buzz-annoyed");
        SetMode(ent, MuleMode.NoRoute, replan: false);
    }

    private void SetMode(Entity<MuleComponent> ent, MuleMode mode, bool replan = true)
    {
        // A new run, or the end of one.
        if (replan)
        {
            ent.Comp.EntranceDoors.Clear();
            ent.Comp.Waypoints.Clear();
        }

        ent.Comp.Mode = mode;
        ent.Comp.Stuck = 0;
        ent.Comp.LastPosition = _transform.GetWorldPosition(ent);
        ent.Comp.LastMoved = _timing.CurTime;
        Dirty(ent);
        UpdateUi(ent);

        if (replan && TryComp<HTNComponent>(ent, out var htn))
            _htn.Replan(htn);
    }

    private void Buzz(EntityUid uid, Robust.Shared.Audio.SoundSpecifier sound, string message)
    {
        Audio.PlayPvs(sound, uid);
        _popup.PopupEntity(Loc.GetString(message, ("mule", uid)), uid);
    }

    private void Say(Entity<MuleComponent> ent, string message)
    {
        _chat.TrySendInGameICMessage(ent, message, InGameICChatType.Speak, ChatTransmitRange.Normal, ignoreActionBlocker: true);
        _radio.SendRadioMessage(ent, message, ent.Comp.Channel, ent);
    }

    /// <summary>
    /// The station beacons on the same grid, by name.
    /// </summary>
    private Dictionary<NetEntity, string> GetBeacons(EntityUid uid)
    {
        var beacons = new Dictionary<NetEntity, string>();
        var grid = Transform(uid).GridUid;
        var query = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var beacon, out var comp, out var xform))
        {
            if (comp.Enabled && xform.GridUid == grid && grid != null && !string.IsNullOrEmpty(comp.Text))
                beacons[GetNetEntity(beacon)] = comp.Text;
        }

        return beacons;
    }

    private EntityUid? NearestBeacon(EntityUid uid)
    {
        var position = _transform.GetWorldPosition(uid);
        return GetBeacons(uid).Keys
            .Select(GetEntity)
            .OrderBy(beacon => (_transform.GetWorldPosition(beacon) - position).LengthSquared())
            .Select(beacon => (EntityUid?) beacon)
            .FirstOrDefault();
    }

    private string BeaconName(EntityUid beacon)
    {
        return CompOrNull<NavMapBeaconComponent>(beacon)?.Text ?? Name(beacon);
    }

    private void UpdateUi(Entity<MuleComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, MuleUiKey.Key))
            return;

        var comp = ent.Comp;
        float? charge = _powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery) ? _battery.GetChargeLevel(battery.Value.AsNullable()) : null;
        string? load = TryGetLoad(ent, out var loaded) ? Name(loaded.Value) : null;

        _ui.SetUiState(ent.Owner, MuleUiKey.Key, new MuleBoundUserInterfaceState(
            comp.On,
            comp.Mode,
            load,
            GetNetEntity(comp.Destination),
            GetNetEntity(comp.Home),
            GetBeacons(ent),
            comp.AutoReturn,
            comp.AutoPickup,
            comp.Report,
            charge));
    }
}
