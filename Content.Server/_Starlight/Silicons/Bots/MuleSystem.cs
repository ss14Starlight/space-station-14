using System.Linq;
using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Pathfinding;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Starlight.Silicons.Bots;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Buckle;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.Emp;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Pinpointer;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Standing;
using Content.Shared.StationRecords;
using Content.Shared.Stunnable;
using Content.Shared._Starlight.Medical.Body.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Silicons.Bots;

public sealed partial class MuleSystem : SharedMuleSystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private HTNSystem _htn = default!;
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
    [Dependency] private IPrototypeManager _prototype = default!;

    private static readonly TimeSpan CheckDelay = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan StuckDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RouteRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FlashLength = TimeSpan.FromSeconds(0.4);
    private const int MaxStuck = 10;
    private static readonly StationRecordKey[] NoKeys = [];
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    /// <summary>
    /// How long before it can knock down or run over the same person again.
    /// </summary>
    private static readonly TimeSpan HitCooldown = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// How close someone has to be to be bumped into or driven over.
    /// </summary>
    private const float HitRange = 0.55f;

    /// <summary>
    /// Doors further apart than this along the route aren't the same entrance.
    /// </summary>
    private const float EntranceLength = 2.5f;

    /// <summary>
    /// Tiles between the points it steers to along a route.
    /// </summary>
    private const int WaypointSpacing = 6;

    /// <summary>
    /// Longest route it will take, in tiles.
    /// </summary>
    private const int MaxRouteLength = 150;

    /// <summary>
    /// How many tiles the route search may look at.
    /// </summary>
    private const int RouteSearchLimit = 4000;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<MuleComponent> ent, ref MapInitEvent args)
    {
        SetOn(ent, ent.Comp.On);
        ent.Comp.Home ??= NearestBeacon(ent);
    }

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

    [SubscribeLocalEvent]
    private void OnCellRemoved(Entity<MuleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (TryComp<PowerCellSlotComponent>(ent, out var slot) && args.Container.ID == slot.CellSlotId)
            TurnOff(ent);
    }

    [SubscribeLocalEvent]
    private void OnEmagged(Entity<MuleComponent> ent, ref GotEmaggedEvent args)
    {
        // The swipe that unlocks its panel is handled by its access reader. With the panel open, the next one gets to its brain.
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || _emag.CheckFlag(ent, EmagType.Interaction))
            return;

        if (!Wires.IsPanelOpen(ent.Owner))
            return;

        args.Handled = true;
        Audio.PlayPvs(ent.Comp.EmagSound, ent);
        ent.Comp.FlashUntil = Timing.CurTime + FlashLength;
        Appearance.SetData(ent, MuleVisuals.Emagged, true);
    }

    [SubscribeLocalEvent]
    private void OnEmp(Entity<MuleComponent> ent, ref EmpPulseEvent args)
    {
        if (!ent.Comp.On)
            return;

        args.Affected = true;
        TurnOff(ent);
    }

    /// <summary>
    /// Carries out an order from its control panel.
    /// </summary>
    public void Command(Entity<MuleComponent> ent, MuleCommand command, EntityUid? user = null)
    {
        var comp = ent.Comp;
        switch (command)
        {
            case MuleCommand.Power when comp.On:
                TurnOff(ent);
                break;
            case MuleCommand.Power:
                if (Wires.IsPanelOpen(ent.Owner))
                {
                    if (user != null)
                        _popup.PopupEntity(Loc.GetString("mule-panel-open"), ent, user.Value);
                    break;
                }

                if (!HasPower(ent))
                {
                    if (user != null)
                        _popup.PopupEntity(Loc.GetString("mule-no-power"), ent, user.Value);
                    break;
                }

                SetOn(ent, true);
                UpdateLights(ent);
                comp.Home ??= NearestBeacon(ent);
                break;
            case MuleCommand.Stop:
                if (comp.Mode != MuleMode.Idle)
                    GoIdle(ent);
                break;
            case MuleCommand.Go:
                if (comp.On && comp.Mode == MuleMode.Idle && comp.Destination != null)
                    SetMode(ent, comp.Destination == comp.Home ? MuleMode.GoingHome : MuleMode.Delivering);
                break;
            case MuleCommand.Home:
                if (comp.On && comp.Mode is MuleMode.Idle or MuleMode.Delivering && comp.Home != null)
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

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<MuleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (now < comp.NextCheck)
                continue;

            comp.NextCheck = now + CheckDelay;
            if (comp.FlashUntil is { } until && now >= until)
            {
                comp.FlashUntil = null;
                UpdateLights((uid, comp));
                Appearance.SetData(uid, MuleVisuals.Emagged, false);
            }

            if (comp.CarefulUntil is { } careful && now >= careful)
            {
                comp.CarefulUntil = null;
                Dirty(uid, comp);
                Movement.RefreshMovementSpeedModifiers(uid);
            }

            if (!IsRunning(comp))
                continue;

            OpenDoors((uid, comp), xform);
            CheckHazards((uid, comp), xform, now);
            CheckProgress((uid, comp), xform, now);
        }
    }

    /// <summary>
    /// Whether it's switched on and out on a run.
    /// </summary>
    public static bool IsRunning(MuleComponent comp)
    {
        return comp.On && comp.Mode is MuleMode.Delivering or MuleMode.GoingHome or MuleMode.Blocked;
    }

    /// <summary>
    /// Where the current run is headed.
    /// </summary>
    private static EntityUid? Target(MuleComponent comp)
    {
        return comp.Mode == MuleMode.GoingHome ? comp.Home : comp.Destination;
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
        comp.Travelled += (position - comp.LastPosition).Length();
        comp.LastPosition = position;

        if (comp.Travelled >= 1f)
        {
            var tiles = MathF.Floor(comp.Travelled);
            comp.Travelled -= tiles;
            if (!_powerCell.TryUseCharge(ent.Owner, comp.ChargePerTile * tiles))
            {
                TurnOff(ent);
                return;
            }
        }

        if (now < comp.LastMoved + StuckDelay)
            return;

        // Every couple of seconds it checks whether it got anywhere.
        var moved = (position - comp.StuckPosition).Length() >= 0.5f;
        comp.StuckPosition = position;
        comp.LastMoved = now;

        if (moved)
        {
            if (comp.Mode == MuleMode.Blocked)
                SetMode(ent, comp.Destination == comp.Home ? MuleMode.GoingHome : MuleMode.Delivering, replan: false);
            return;
        }

        comp.Stuck++;
        comp.Mode = MuleMode.Blocked;
        Dirty(ent);
        UpdateUi(ent);

        if (comp.Stuck < MaxStuck)
        {
            Buzz(ent, comp.SighSound, "mule-buzz-sigh");
            return;
        }

        // Fed up: try another way round.
        comp.Stuck = 0;
        comp.Waypoints.Clear();
        Buzz(ent, comp.AnnoyedSound, "mule-buzz-annoyed");
        if (TryComp<HTNComponent>(ent, out var htn))
            _htn.Replan(htn);
    }

    /// <summary>
    /// Plans the drive to where it's going: up to the beacon, or to the last tile before a door it can't get through.
    /// Fills <see cref="MuleComponent.Waypoints"/> and returns false if there's no way there right now.
    /// </summary>
    public bool PlanRoute(Entity<MuleComponent> ent)
    {
        var comp = ent.Comp;
        if (Target(comp) is not { } beacon || TerminatingOrDeleted(beacon))
            return false;

        // With its beacon receiver cut it can't find where anything is.
        if (HasWireCut(ent, MuleWires.Beacon))
            return false;

        // Still on the way along a route planned for this run.
        if (comp.Waypoints.Count > 0)
            return true;

        var now = Timing.CurTime;
        if (now < comp.NextRouteTry)
            return false;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || Transform(beacon).GridUid != gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var ownAccess = _accessReader.FindAccessTags(ent);
        var entranceAccess = ownAccess.Concat(comp.EntranceAccess).ToHashSet();
        var collision = _physics.GetHardCollision(ent);
        var costs = new Dictionary<Vector2i, float>();
        var doors = new Dictionary<Vector2i, (EntityUid Door, DoorAccess Access)>();

        float TileCost(Vector2i tile)
        {
            if (costs.TryGetValue(tile, out var known))
                return known;

            var cost = TileCostUncached(gridUid, grid, tile, collision, ownAccess, entranceAccess, doors);
            costs[tile] = cost;
            return cost;
        }

        var start = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        var end = _map.CoordinatesToTile(gridUid, grid, Transform(beacon).Coordinates);

        // A beacon under a crate or in a wall would send the search round the whole station; aim beside it instead.
        if (TileCost(end).Equals(0f))
        {
            var open = new[] { new Vector2i(0, 1), new Vector2i(0, -1), new Vector2i(1, 0), new Vector2i(-1, 0) }
                .Select(offset => end + offset)
                .Where(next => !TileCost(next).Equals(0f))
                .Select(next => (Vector2i?) next)
                .FirstOrDefault();

            if (open is not { } beside)
            {
                comp.NextRouteTry = now + RouteRetryDelay;
                return false;
            }

            end = beside;
        }

        var path = _pathfinding.GetPath(new PathfindingSystem.SimplePathArgs
        {
            Start = start,
            End = end,
            Limit = RouteSearchLimit,
            TileCost = TileCost,
        }).Path;

        if (path == null || path.Count == 0 || path.Count > MaxRouteLength)
        {
            // Try again in a bit; a door or firelock may open.
            comp.NextRouteTry = now + RouteRetryDelay;
            return false;
        }

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
        (int Layer, int Mask) collision,
        ICollection<ProtoId<AccessLevelPrototype>> ownAccess,
        ICollection<ProtoId<AccessLevelPrototype>> entranceAccess,
        Dictionary<Vector2i, (EntityUid Door, DoorAccess Access)> doors)
    {
        // The pathfinder already knows what blocks each tile; walls and furniture end here without a lookup.
        if (_pathfinding.GetPoly(_map.GridTileToLocal(gridUid, grid, tile)) is not { } poly || (poly.Data.Flags & PathfindingBreadcrumbFlag.Space) != 0)
            return 0f;

        var blocked = (poly.Data.CollisionLayer & collision.Mask) != 0 || (poly.Data.CollisionMask & collision.Layer) != 0;
        if (blocked && (poly.Data.Flags & PathfindingBreadcrumbFlag.Door) == 0)
            return 0f;

        // Doors still need a look, open ones too, since access decides whether it may go through.
        EntityUid? doorUid = null;
        DoorComponent? door = null;
        foreach (var uid in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (TryComp<DoorComponent>(uid, out var found))
            {
                doorUid = uid;
                door = found;
                continue;
            }

            // A windoor on a desk still leaves the desk in the way.
            if (blocked && TryComp<PhysicsComponent>(uid, out var body) && body.CanCollide && body.Hard
                && (body.CollisionLayer & collision.Mask) != 0)
            {
                var bounds = _lookup.GetAABBNoContainer(uid, Vector2.Zero, Angle.Zero);
                if (bounds.Width >= 0.5f && bounds.Height >= 0.5f)
                    return 0f;
            }
        }

        if (doorUid is not { } doorEnt || door == null)
            return blocked ? 0f : 1f;

        // Firelocks stand open unless something's wrong, and nobody gets through a shut one.
        if (HasComp<FirelockComponent>(doorEnt))
            return door.State == DoorState.Closed ? 0f : 1f;

        var access = DoorAccessFor(doorEnt, ownAccess, entranceAccess);
        doors[tile] = (doorEnt, access);
        return access switch
        {
            DoorAccess.Own => 1.2f,
            DoorAccess.Entrance => 4f,
            _ => 12f,
        };
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
        var target = Target(comp);
        var place = target is { } beacon ? BeaconName(beacon) : string.Empty;

        if (TryGetLoad(ent, out var load))
        {
            if (comp.Report)
            {
                Buzz(ent, comp.ArrivedSound, "mule-chime");
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

        if (comp.AutoReturn && comp.Home != null && target != comp.Home)
            SetMode(ent, MuleMode.GoingHome);
        else
            GoIdle(ent);
    }

    /// <summary>
    /// Switches it off, ending any run.
    /// </summary>
    private void TurnOff(Entity<MuleComponent> ent)
    {
        SetOn(ent, false);
        UpdateLights(ent);
        GoIdle(ent);
    }

    /// <summary>
    /// Ends the current run and forgets where it was sent.
    /// </summary>
    private void GoIdle(Entity<MuleComponent> ent)
    {
        ent.Comp.Destination = null;
        SetMode(ent, MuleMode.Idle);
    }

    private void SetMode(Entity<MuleComponent> ent, MuleMode mode, bool replan = true)
    {
        var comp = ent.Comp;

        // A new run, or the end of one.
        if (replan)
        {
            comp.EntranceDoors.Clear();
            comp.Waypoints.Clear();
            comp.NextRouteTry = TimeSpan.Zero;
            comp.Stuck = 0;
        }

        comp.Mode = mode;
        comp.LastPosition = comp.StuckPosition = _transform.GetWorldPosition(ent);
        comp.LastMoved = Timing.CurTime;
        Dirty(ent);
        UpdateUi(ent);

        if (replan && TryComp<HTNComponent>(ent, out var htn))
            _htn.Replan(htn);
    }

    private void Buzz(Entity<MuleComponent> ent, SoundSpecifier sound, string message)
    {
        Audio.PlayPvs(sound, ent);
        _popup.PopupEntity(Loc.GetString(message, ("mule", ent.Owner)), ent);
        ent.Comp.FlashUntil = Timing.CurTime + FlashLength;
        Appearance.SetData(ent, MuleVisuals.Flash, true);
    }

    /// <summary>
    /// Its lights blink while it runs with the safety wire cut, and otherwise only flash when it buzzes.
    /// </summary>
    private void UpdateLights(Entity<MuleComponent> ent)
    {
        Appearance.SetData(ent, MuleVisuals.Flash, ent.Comp.On && HasWireCut(ent, MuleWires.Avoidance));
    }

    private void Say(Entity<MuleComponent> ent, string message)
    {
        _chat.TrySendInGameICMessage(ent, message, InGameICChatType.Speak, ChatTransmitRange.Normal, ignoreActionBlocker: true);
        _radio.SendRadioMessage(ent, message, ent.Comp.Channel, ent);
    }

    /// <summary>
    /// Whether it has a charged cell and both power wires intact.
    /// </summary>
    public bool HasPower(Entity<MuleComponent> ent)
    {
        return !HasWireCut(ent, MuleWires.Power1) && !HasWireCut(ent, MuleWires.Power2)
            && _powerCell.HasCharge(ent.Owner, ent.Comp.ChargePerTile);
    }

    /// <summary>
    /// Cuts or mends one of its wires.
    /// </summary>
    public void SetWireCut(Entity<MuleComponent> ent, MuleWires wire, bool cut, EntityUid? user = null)
    {
        if (HasWireCut(ent, wire) == cut)
            return;

        if (cut)
            ent.Comp.CutWires |= wire;
        else
            ent.Comp.CutWires &= ~wire;

        Dirty(ent);

        switch (wire)
        {
            case MuleWires.Power1 or MuleWires.Power2:
                if (cut)
                    TurnOff(ent);
                break;
            case MuleWires.Motor1 or MuleWires.Motor2:
                Movement.RefreshMovementSpeedModifiers(ent);
                var motor = (HasWireCut(ent, MuleWires.Motor1), HasWireCut(ent, MuleWires.Motor2)) switch
                {
                    (true, true) => "mule-motor-stopped",
                    (true, false) => "mule-motor-fast",
                    (false, true) => "mule-motor-medium",
                    _ => "mule-motor-slow",
                };
                _popup.PopupEntity(Loc.GetString(motor, ("mule", ent.Owner)), ent);
                break;
            case MuleWires.Avoidance:
                UpdateLights(ent);
                if (cut)
                {
                    _popup.PopupEntity(Loc.GetString("mule-avoidance-cut", ("mule", ent.Owner)), ent);
                    if (user != null)
                        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user.Value):user} cut the safety wire of {ToPrettyString(ent):mule}");
                }
                break;
            case MuleWires.LoadCheck:
                Buckle.StrapSetEnabled(ent, cut);
                break;
        }
    }

    /// <summary>
    /// Shows what a pulsed wire does, if it has the power to show anything.
    /// </summary>
    public void PulseWire(Entity<MuleComponent> ent, MuleWires wire)
    {
        if (!HasPower(ent))
            return;

        var message = wire switch
        {
            MuleWires.Power1 or MuleWires.Power2 => "mule-pulse-power",
            MuleWires.Avoidance => "mule-pulse-avoidance",
            MuleWires.LoadCheck => "mule-pulse-load",
            MuleWires.Motor1 or MuleWires.Motor2 => "mule-pulse-motor",
            _ => "mule-pulse-radio",
        };

        _popup.PopupEntity(Loc.GetString(message, ("mule", ent.Owner)), ent);
        if (wire == MuleWires.Avoidance)
        {
            ent.Comp.FlashUntil = Timing.CurTime + FlashLength;
            Appearance.SetData(ent, MuleVisuals.Flash, true);
        }
    }

    /// <summary>
    /// Looks at who's in its way. Normally it slows right down for anyone lying there. Hacked or emagged, it doesn't.
    /// </summary>
    private void CheckHazards(Entity<MuleComponent> ent, TransformComponent xform, TimeSpan now)
    {
        var comp = ent.Comp;
        var avoidanceCut = HasWireCut(ent, MuleWires.Avoidance);
        var dangerous = avoidanceCut || _emag.CheckFlag(ent, EmagType.Interaction);
        TryGetRider(ent, out var rider);

        foreach (var expired in comp.RecentHits.Where(hit => hit.Value <= now).Select(hit => hit.Key).ToList())
        {
            comp.RecentHits.Remove(expired);
        }

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, HitRange))
        {
            if (mob.Owner == ent.Owner || mob.Owner == rider || comp.RecentHits.ContainsKey(mob))
                continue;

            if (_standing.IsDown(mob.Owner))
            {
                if (!HasComp<HumanoidAppearanceComponent>(mob))
                    continue;

                if (dangerous)
                    RunOver(ent, mob, now);
                else
                    DriveCarefully(ent, mob, now);
            }
            else if (avoidanceCut && !HasComp<BorgChassisComponent>(mob))
            {
                comp.RecentHits[mob] = now + HitCooldown;
                if (_stun.TryKnockdown(mob.Owner, comp.KnockdownTime))
                {
                    _popup.PopupEntity(Loc.GetString("mule-knocks-over", ("mule", ent.Owner), ("target", mob.Owner)), ent, PopupType.MediumCaution);
                    _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ent):mule} knocked over {ToPrettyString(mob):target}");
                }
            }
        }
    }

    private void DriveCarefully(Entity<MuleComponent> ent, EntityUid target, TimeSpan now)
    {
        if (ent.Comp.CarefulUntil == null)
            _popup.PopupEntity(Loc.GetString("mule-careful", ("mule", ent.Owner), ("target", target)), ent);

        ent.Comp.CarefulUntil = now + ent.Comp.CarefulTime;
        Dirty(ent);
        Movement.RefreshMovementSpeedModifiers(ent);
    }

    private void RunOver(Entity<MuleComponent> ent, EntityUid target, TimeSpan now)
    {
        var comp = ent.Comp;
        comp.RecentHits[target] = now + HitCooldown;

        var amount = _random.NextFloat(comp.RunOverMin, comp.RunOverMax) * comp.RunOverMultiplier;
        _damageable.TryChangeDamage(target, new DamageSpecifier(_prototype.Index(Blunt), amount), origin: ent);
        _bloodstream.TryBleedOut(target, comp.RunOverBlood);
        Audio.PlayPvs(comp.RunOverSound, target);
        _popup.PopupEntity(Loc.GetString("mule-runs-over", ("mule", ent.Owner), ("target", target)), target, PopupType.LargeCaution);
        _adminLogger.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(ent):mule} ran over {ToPrettyString(target):target} for {amount:F0} blunt");
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
