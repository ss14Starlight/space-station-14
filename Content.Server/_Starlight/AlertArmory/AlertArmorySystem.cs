using System.Diagnostics.CodeAnalysis;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Server.Pinpointer;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Utility;
using Content.Shared.Station.Components;
using Robust.Shared.Physics.Components;
using System.Numerics;
using System.Linq;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Content.Shared.Mobs.Components;
using Robust.Shared.Player;

namespace Content.Server.Starlight.AlertArmory;

public sealed class AlertArmorySystem : EntitySystem
{
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly ShuttleSystem _shuttles = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly NavMapSystem _nav = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ActorSystem _actor = default!;

    private EntityQuery<PendingClockInComponent> _pendingQuery;
    private EntityQuery<ArrivalsBlacklistComponent> _blacklistQuery;
    private EntityQuery<MobStateComponent> _mobQuery;

    public override void Initialize()
    {
        SubscribeLocalEvent<AlertArmoryStationComponent, StationPostInitEvent>(InitializeAlertArmoryStation);
        SubscribeLocalEvent<AlertArmoryShuttleComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AlertArmoryShuttleComponent, FTLStartedEvent>(OnFTLStart);
        SubscribeLocalEvent<AlertArmoryShuttleComponent, FTLTagEvent>(SetShuttleTag);
        SubscribeLocalEvent<AlertArmoryShuttleComponent, FTLCompletedEvent>(AnnounceShuttleDocking);

        _pendingQuery = GetEntityQuery<PendingClockInComponent>();
        _blacklistQuery = GetEntityQuery<ArrivalsBlacklistComponent>();
        _mobQuery = GetEntityQuery<MobStateComponent>();
    }

    ///<summary>
    /// Initialize station with armories and preload all armories.
    ///</summary>
    private void InitializeAlertArmoryStation(EntityUid uid, AlertArmoryStationComponent comp, StationPostInitEvent ev)
    {
        var map = _map.CreateMap(out var mapId);
        _meta.SetEntityName(map, $"AlertArmories {uid}");

        var xOffset = 0f;
        foreach (var (alert, armory) in comp.Shuttles)
        {
            if (!_loader.TryLoadGrid(mapId, armory.Shuttle, out var grid))
            {
                Log.Error($"Failed to load {alert} armory {armory.Shuttle}");
                continue;
            }

            var (gridUid, mapGrid) = grid.Value;

            if (!TryComp<PhysicsComponent>(gridUid, out var physics))
                continue;

            xOffset += mapGrid.LocalAABB.Width / 2;

            var coords = new Vector2(-physics.LocalCenter.X + xOffset, -physics.LocalCenter.Y);
            var eCoords = new EntityCoordinates(map, coords);
            _transform.SetCoordinates(gridUid, eCoords);

            xOffset += (mapGrid.LocalAABB.Width / 2) + 1;

            var armoryComp = EnsureComp<AlertArmoryShuttleComponent>(gridUid);
            armoryComp.Station = uid;
            armoryComp.Announcement = armory.Announcement;
            armoryComp.AnnouncementColor = armory.AnnouncementColor;
            armoryComp.RecallAnnouncement = armory.RecallAnnouncement;
            armoryComp.RecallAnnouncementColor = armory.RecallAnnouncementColor;
            armoryComp.CoordsCache = eCoords;
            armoryComp.ArmorySpaceUid = map;

            comp.Grids[alert] = gridUid;
        }
    }

    ///<summary>
    /// Adds component so you can't pilot alert armory and fly off on it
    ///</summary>
    private void OnStartup(EntityUid uid, AlertArmoryShuttleComponent comp, ComponentStartup ev) => EnsureComp<PreventPilotComponent>(uid);

    private void OnFTLStart(Entity<AlertArmoryShuttleComponent> ent, ref FTLStartedEvent ev)
    {
        if (ev.FromMapUid != ent.Comp.ArmorySpaceUid) //if we are not coming from armory space. drop people. this allows including eg: ERT on a armory if you want.
        {
            DumpChildren(ent.Owner, ref ev);

            // Announce recall at the start of FTL back to armory space
            var xform = Transform(ent.Owner);
            var location = FormattedMessage.RemoveMarkupPermissive(_nav.GetNearestBeaconString((ent.Owner, xform)));

            if (ent.Comp.RecallAnnouncement != null)
            {
                _chat.DispatchGlobalAnnouncement(
                    Loc.GetString(ent.Comp.RecallAnnouncement, ("location", location)),
                    colorOverride: ent.Comp.RecallAnnouncementColor ?? Color.PaleVioletRed);
            }
        }

        // Mark as in transit
        ent.Comp.InTransit = true;
    }

    ///<summary>
    /// Sets shuttle dock tag, so it try dock to correct place
    ///</summary>
    private void SetShuttleTag(EntityUid uid, AlertArmoryShuttleComponent comp, ref FTLTagEvent ev)
    {
        if (ev.Handled || comp.DockTag == null)
            return;

        ev.Handled = true;
        ev.Tag = comp.DockTag;
    }

    ///<summary>
    /// Sends announcement message about shuttle docking.
    ///</summary>
    private void AnnounceShuttleDocking(EntityUid uid, AlertArmoryShuttleComponent comp, ref FTLCompletedEvent ev)
    {
        comp.InTransit = false;

        var xform = Transform(uid);
        var location = FormattedMessage.RemoveMarkupPermissive(_nav.GetNearestBeaconString((uid, xform)));

        // Announce arrival at station
        if (ev.MapUid != comp.ArmorySpaceUid && comp.Announcement != null)
        {
            _chat.DispatchGlobalAnnouncement(
                Loc.GetString(comp.Announcement, ("location", location)),
                colorOverride: comp.AnnouncementColor ?? Color.PaleVioletRed);
        }

        comp.InTransit = false;
    }

    ///<summary>
    /// Sends an armory shuttle to the station by its armory key.
    ///</summary>
    public bool SendArmory(EntityUid stationUid, string armoryKey)
    {
        if (!TryComp<AlertArmoryStationComponent>(stationUid, out var comp))
            return false;

        if (!comp.Grids.TryGetValue(armoryKey, out var shuttle))
            return false;

        var targetGrid = _station.GetLargestGrid((stationUid, Comp<StationDataComponent>(stationUid)));
        if (targetGrid == null)
            return false;

        _shuttles.FTLToDock(
            shuttle,
            Comp<ShuttleComponent>(shuttle),
            targetGrid.Value,
            priorityTag: Comp<AlertArmoryShuttleComponent>(shuttle).DockTag);

        return true;
    }

    ///<summary>
    /// Recalls an armory shuttle back to armory space by its armory key.
    ///</summary>
    public bool RecallArmory(EntityUid stationUid, string armoryKey)
    {
        if (!TryComp<AlertArmoryStationComponent>(stationUid, out var comp))
            return false;

        if (!comp.Grids.TryGetValue(armoryKey, out var shuttle))
            return false;

        var shuttleComp = Comp<AlertArmoryShuttleComponent>(shuttle);
        var xform = Transform(shuttle);

        // Check if already in armory space
        if (xform.MapUid == shuttleComp.ArmorySpaceUid)
            return false;

        _shuttles.FTLToCoordinates(
            shuttle,
            Comp<ShuttleComponent>(shuttle),
            shuttleComp.CoordsCache,
            0);

        return true;
    }

    private void DumpChildren(EntityUid uid, ref FTLStartedEvent args)
    {
        var toDump = new List<Entity<TransformComponent>>();
        FindDumpChildren(uid, toDump);
        foreach (var (ent, xform) in toDump)
        {
            var rotation = xform.LocalRotation;
            _transform.SetCoordinates(ent, new EntityCoordinates(args.FromMapUid!.Value, Vector2.Transform(xform.LocalPosition, args.FTLFrom)));
            _transform.SetWorldRotation(ent, args.FromRotation + rotation);
            _popup.PopupEntity(Loc.GetString("latejoin-arrivals-dumped-from-shuttle"), ent);
        }
    }

    private void FindDumpChildren(EntityUid uid, List<Entity<TransformComponent>> toDump)
    {
        if (_pendingQuery.HasComponent(uid))
            return;

        var xform = Transform(uid);

        if (_mobQuery.HasComponent(uid) || _blacklistQuery.HasComponent(uid))
        {
            toDump.Add((uid, xform));
            return;
        }

        var children = xform.ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            FindDumpChildren(child, toDump);
        }
    }
}