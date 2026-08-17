using System.Numerics;
using Content.Server.Actions;
using Content.Server.Objectives.Systems;
using Content.Server.Popups;
using Content.Server._Starlight.CosmicCult.Components;
using Content.Server.Chat.Systems;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Warps;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.CosmicCult;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using Content.Server.Pinpointer;
using Content.Shared.Anomaly.Components;
using Content.Server.Nuke;
using Content.Server.Station.Systems;
using Content.Shared.Station.Components;

namespace Content.Server._Starlight.CosmicCult.Abilities.Colossus;

public sealed partial class CosmicEffigySystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private CodeConditionSystem _codeCondition = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private StationSystem _station = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CosmicColossusComponent, EventCosmicColossusEffigy>(OnColossusEffigy);
        SubscribeLocalEvent<CosmicEffigyComponent, EntityTerminatingEvent>(OnEffigyTerminating);
        SubscribeLocalEvent<CosmicEffigyComponent, CosmicEffigyDestroyedEvent>(OnEffigyDestroyed);
        SubscribeLocalEvent<CosmicEffigyComponent, AnomalyShutdownEvent>(OnEffigyAnomalyShutdown);
    }

    private void OnColossusEffigy(Entity<CosmicColossusComponent> ent, ref EventCosmicColossusEffigy args)
    {
        if (ent.Comp.CurrentEffigy != null && Exists(ent.Comp.CurrentEffigy))
        {
            _popup.PopupEntity(Loc.GetString("cosmiccult-silicon-effigy-exists"), ent, ent);
            return;
        }
        if (!VerifyPlacement(ent, out var pos))
            return;

        var effigy = Spawn(ent.Comp.EffigyPrototype, pos);

        // Give the Colossus an objective if they spawned on a station.
        if (_mind.TryGetObjectiveComp<CosmicEffigyConditionComponent>(ent, out var obj))
        {
            var station = _station.GetOwningStation(ent.Owner);

            // If we are on a grid without a station, use the grid containing the nuke.
            if (station == null && Transform(ent).GridUid is { } currentGrid)
            {
                var nukeQuery = EntityQueryEnumerator<NukeComponent, TransformComponent>();
                while (nukeQuery.MoveNext(out var nukeUid, out _, out var nukeTransform))
                {
                    if (nukeTransform.GridUid == null || nukeTransform.GridUid == currentGrid)
                        continue;

                    station = _station.GetOwningStation(nukeUid);
                    if (station != null)
                        break;
                }
            }

            // No station found means the Colossus gets no effigy objective.
            if (station != null)
            {
                obj.EffigyTarget = station;
                _codeCondition.SetCompleted(ent.Owner, ent.Comp.EffigyObjective);
            }
        }

        // Free the Colossus of the location
        ent.Comp.CurrentEffigy = effigy;
        if (ent.Comp.EffigyPlaceActionEntity is { } action && TryComp<LimitedChargesComponent>(action, out var charges))
        {
            _charges.SetCharges((action, charges), 0);
            Dirty(action, charges);
        }
        ent.Comp.Timed = false; // Flag for midround spawn; prevents death timer.
        Dirty(ent);
    }

    private void OnEffigyAnomalyShutdown(Entity<CosmicEffigyComponent> ent, ref AnomalyShutdownEvent args)
    {
        if (args.Anomaly != ent.Owner || !args.Supercritical)
            return;

        if (ent.Comp.Colossus is not { } colossusUid ||
            !TryComp<CosmicColossusComponent>(colossusUid, out var colossus))
            return;

        if (colossus.EffigyCrits != 0)
            {
            var xform = Transform(colossusUid);
            var location = FormattedMessage.RemoveMarkupOrThrow(
                _navMap.GetNearestBeaconString((colossusUid, xform)));

            _chatSystem.DispatchStationAnnouncement(colossusUid, Loc.GetString("cosmiccult-effigy-critical", ("location", location)), null, false, null, Color.FromHex("#cae8e8"));
            }
        colossus.EffigyCrits++;
        Dirty(colossusUid, colossus);
    }

    private void OnEffigyTerminating(Entity<CosmicEffigyComponent> ent, ref EntityTerminatingEvent args)
    {
        RaiseLocalEvent(ent.Owner, new CosmicEffigyDestroyedEvent());
    }

    /// Detect when the linked effigy gets decayed, crit, or otherwise deleted.
    private void OnEffigyDestroyed(Entity<CosmicEffigyComponent> ent, ref CosmicEffigyDestroyedEvent args)
    {
        if (ent.Comp.Colossus is not { } colossusUid)
            return;

        if (!TryComp<CosmicColossusComponent>(colossusUid, out var colossus))
            return;

        //Remove reference to the destroyed effigy
        if (colossus.CurrentEffigy == ent.Owner)
            colossus.CurrentEffigy = null;

        _popup.PopupEntity(Loc.GetString("ghost-role-colossus-effigy-lost"), colossusUid, colossusUid, PopupType.LargeCaution);

        // Start the recharge timer, vanishing of effigy + time
        colossus.EffigyRechargeTimer = _timing.CurTime + colossus.EffigyRechargeTime;
        // Update networked component state
        Dirty(colossusUid, colossus);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CosmicColossusComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.EffigyRechargeTimer is null || _timing.CurTime < comp.EffigyRechargeTimer)
                continue;

            comp.EffigyRechargeTimer = null;
            Dirty(uid, comp);

            if (comp.EffigyPlaceActionEntity is not { } action)
                continue;

            // No LimitedCharges component
            if (!TryComp<LimitedChargesComponent>(action, out var charges))
                continue;

            // Already charged
            if (_charges.GetCurrentCharges((action, charges, null)) >= charges.MaxCharges)
                continue;

            // Restore ability
            _charges.SetCharges((action, charges), charges.MaxCharges);
            _popup.PopupEntity(Loc.GetString("ghost-role-colossus-effigy-ready"), uid, uid,
            PopupType.LargeCaution);
        }
    }

    private bool VerifyPlacement(Entity<CosmicColossusComponent> ent, out EntityCoordinates outPos)
    {
        // MAKE SURE WE'RE STANDING ON A GRID
        var xform = Transform(ent);
        outPos = new EntityCoordinates();

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            _popup.PopupEntity(Loc.GetString("ghost-role-colossus-effigy-error-grid"), ent, ent);
            return false;
        }

        var localTile = _map.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);
        var targetIndices = localTile.GridIndices + new Vector2i(0, 2);
        var pos = _map.ToCenterCoordinates(xform.GridUid.Value, targetIndices, grid);
        outPos = pos;
        var box = new Box2(pos.Position + new Vector2(-1.4f, -0.4f), pos.Position + new Vector2(1.4f, 0.4f));

        // CHECK IF IT'S BEING PLACED CHEESILY CLOSE TO SPACE
        var spaceDistance = 2;
        for (var x = -spaceDistance; x <= spaceDistance; x++)
        {
            for (var y = -spaceDistance; y <= spaceDistance; y++)
            {
                var checkTile = _map.GetTileRef(xform.GridUid.Value, grid, targetIndices + new Vector2i(x, y));
                if (_turf.IsSpace(checkTile))
                {
                    _popup.PopupEntity(Loc.GetString("ghost-role-colossus-effigy-error-space", ("DISTANCE", spaceDistance)), ent, ent);
                    return false;
                }
            }
        }

        // CHECK FOR ENTITY AND ENVIRONMENTAL INTERSECTIONS
        if (_lookup.AnyLocalEntitiesIntersecting(xform.GridUid.Value, box, LookupFlags.Dynamic | LookupFlags.Static, ent))
        {
            _popup.PopupEntity(Loc.GetString("ghost-role-colossus-effigy-error-intersection"), ent, ent);
            return false;
        }

        // IF THE OBJECTIVE OR LOCATION IS MISSING, PLACE IT ANYWHERE
        if (!_mind.TryGetObjectiveComp<CosmicEffigyConditionComponent>(ent, out var obj) || obj.EffigyTarget == null)
            return true;

        var targetXform = Transform(obj.EffigyTarget.Value);
        if (xform.MapID != targetXform.MapID || (_transform.GetWorldPosition(xform) - _transform.GetWorldPosition(targetXform)).LengthSquared() > 15 * 15)
        {
            if (TryComp<WarpPointComponent>(obj.EffigyTarget, out var warp) && warp.Location is not null)
                _popup.PopupEntity(Loc.GetString("ghost-role-colossus-effigy-error-location", ("LOCATION", warp.Location)), ent, ent);
            return false;
        }

        return true;
    }
}
