using System.Collections.Generic;
using System.Numerics;
using Content.Server.Power.Components;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Doors.Components;
using Content.Shared.Gravity;
using Content.Shared.Prying.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Functional.TutorialServer;

public sealed partial class TutorialPracticeRoomSystem
{
    private static readonly EntProtoId TutorialApcAlwaysOnProto = "TutorialApcAlwaysOn";
    private static readonly EntProtoId CableApcExtensionProto = "CableApcExtension";
    private static readonly EntProtoId TutorialGridSupportProto = "TutorialInvisibleGridSupport";
    private static readonly EntProtoId TutorialAirlockMaintProto = "TutorialAirlockMaint";

    [Dependency] private SharedBatterySystem _battery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialGateDoorComponent, MapInitEvent>(OnGateMapInit);
        SubscribeLocalEvent<TutorialToolOnlyPryComponent, BeforePryEvent>(OnBeforePry);
    }

    /// <summary>
    /// Refuses a bare-handed pry. <c>StrongPry</c> is false only when nothing with a
    /// <c>Prying</c> component was involved, so a crowbar still goes through.
    /// </summary>
    private void OnBeforePry(Entity<TutorialToolOnlyPryComponent> ent, ref BeforePryEvent args)
    {
        if (args.StrongPry)
            return;

        // No message: the bare-hand path in PryingSystem discards it, and the coach has already
        // told the player the door is dead and where the crowbar is.
        args.Cancelled = true;
    }

    /// <summary>
    /// Bolts a gate shut as it initialises. Runtime-stamped suites get this from
    /// <see cref="SpawnGateDoor"/>, but a hand-authored map places its gates directly, and an
    /// unbolted gate would leave every chamber open from the first second of the tutorial.
    /// </summary>
    private void OnGateMapInit(Entity<TutorialGateDoorComponent> ent, ref MapInitEvent args)
    {
        // Crowbar-practice gates stay closed but unbolted; already-unlocked ones stay open.
        if (ent.Comp.Unlocked || ent.Comp.RequirePry)
            return;

        SealGate(ent.Owner);
    }

    /// <summary>
    /// Spawns the invisible station-anchor + gravity-generator helper on a tutorial grid.
    /// </summary>
    public void EnsureGridSupport(EntityUid gridUid)
    {
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            if (MetaData(uid).EntityPrototype?.ID == TutorialGridSupportProto.Id)
                return;
        }

        var coords = new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f));
        if (TryComp<TutorialRoomLayoutComponent>(gridUid, out var layout) && layout.ChamberCenters.Count > 0)
            coords = new EntityCoordinates(gridUid, layout.ChamberCenters[0]);

        Spawn(TutorialGridSupportProto, coords);
    }

    /// <summary>
    /// Enables gravity and marks it inherent so generator refresh cannot turn it off.
    /// Must enable before setting Inherent — <see cref="GravitySystem.EnableGravity"/> no-ops when Inherent is already true.
    /// </summary>
    public void EnableInherentGravity(EntityUid gridUid)
    {
        var gravity = EnsureComp<GravityComponent>(gridUid);
        if (!gravity.Enabled)
            _gravity.EnableGravity(gridUid, gravity);

        gravity.Inherent = true;
        // EnableGravity returns early when Inherent was already set; force Enabled in that case.
        if (!gravity.Enabled)
        {
            gravity.Enabled = true;
            var ev = new GravityChangedEvent(gridUid, true);
            RaiseLocalEvent(gridUid, ref ev, true);
        }

        Dirty(gridUid, gravity);
    }

    /// <summary>
    /// Charges every APC on the grid and ensures self-recharge so crop APCs look/work powered.
    /// </summary>
    public void EnsureApcsCharged(EntityUid gridUid)
    {
        var query = EntityQueryEnumerator<ApcComponent, BatteryComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var battery, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            // Clamp to MaxCharge inside SetCharge; keep self-recharge topped up.
            _battery.SetCharge((uid, battery), float.MaxValue);
            var charger = EnsureComp<BatterySelfRechargerComponent>(uid);
            if (charger.AutoRechargeRate < 50000f)
                charger.AutoRechargeRate = 50000f;
        }
    }

    /// <summary>
    /// Spawns a bolted inter-chamber gate door used by both procedural rooms and template stamps.
    /// </summary>
    public EntityUid SpawnGateDoorPublic(EntProtoId proto, EntityUid gridUid, Vector2i tile, int unlockAtGoal)
        => SpawnGateDoor(proto, gridUid, tile, unlockAtGoal);

    private EntityUid SpawnGateDoor(EntProtoId proto, EntityUid gridUid, Vector2i tile, int unlockAtGoal)
    {
        var door = SpawnAnchored(proto, gridUid, tile);
        var pryGate = proto == TutorialAirlockMaintProto;

        var gate = EnsureComp<TutorialGateDoorComponent>(door);
        gate.UnlockAtGoalIndex = unlockAtGoal;
        gate.Unlocked = false;
        gate.RequirePry = pryGate;
        Dirty(door, gate);

        if (TryComp<DoorComponent>(door, out var doorComp) && doorComp.State != DoorState.Closed)
            _doors.TryClose(door, doorComp);

        if (pryGate)
        {
            // Crowbar practice: closed, unbolted, unpowered. LV cables light the rooms via nearby tiles
            // and crop APCs — never wire/power the door tile itself.
            if (TryComp<DoorBoltComponent>(door, out var pryBolt) && pryBolt.BoltsDown)
                _doors.SetBoltsDown((door, pryBolt), false);

            PlaceRoomLightCables(gridUid, tile);
            return door;
        }

        if (TryComp<DoorBoltComponent>(door, out var bolt))
            _doors.SetBoltsDown((door, bolt), true);

        // Always-on APC + LV cable so the door stays powered when unbolted.
        PowerDoorWithApc(gridUid, tile);

        return door;
    }

    /// <summary>
    /// Always-on APC + LV cable so a practice door stays powered until wires are pulsed/cut.
    /// </summary>
    public void PowerDoorWithApcPublic(EntityUid gridUid, Vector2i doorTile)
        => PowerDoorWithApc(gridUid, doorTile);

    /// <summary>
    /// Clears leftover vault/crop doors on the tile, recenters the hack door, and powers it.
    /// </summary>
    public void PrepareHackPracticeDoor(EntityUid gridUid, EntityUid door, Vector2 localPos)
    {
        var tile = new Vector2i(
            (int) MathF.Floor(localPos.X),
            (int) MathF.Floor(localPos.Y));

        var toDelete = new List<EntityUid>();
        var query = EntityQueryEnumerator<DoorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != gridUid || uid == door)
                continue;

            var otherTile = new Vector2i(
                (int) MathF.Floor(xform.LocalPosition.X),
                (int) MathF.Floor(xform.LocalPosition.Y));
            if (otherTile != tile)
                continue;

            toDelete.Add(uid);
        }

        foreach (var uid in toDelete)
            Del(uid);

        var centered = new EntityCoordinates(gridUid, tile.X + 0.5f, tile.Y + 0.5f);
        _xform.SetCoordinates(door, centered);
        var doorXform = Transform(door);
        if (!doorXform.Anchored && TryComp<MapGridComponent>(gridUid, out var grid))
        {
            _xform.AnchorEntity((door, doorXform), (gridUid, grid));
        }

        if (TryComp<DoorComponent>(door, out var doorComp) && doorComp.State != DoorState.Closed)
            _doors.TryClose(door, doorComp);

        if (TryComp<DoorBoltComponent>(door, out var bolt) && bolt.BoltsDown)
            _doors.SetBoltsDown((door, bolt), false);

        PowerDoorWithApc(gridUid, tile);
    }

    private void PowerDoorWithApc(EntityUid gridUid, Vector2i doorTile)
    {
        // Wallmount APC on the divider wall above the door when possible.
        var apcTile = doorTile + new Vector2i(0, 1);
        SpawnAnchored(TutorialApcAlwaysOnProto, gridUid, apcTile);

        SpawnAnchored(CableApcExtensionProto, gridUid, doorTile);
        SpawnAnchored(CableApcExtensionProto, gridUid, doorTile + new Vector2i(-1, 0));
        SpawnAnchored(CableApcExtensionProto, gridUid, apcTile);
    }

    /// <summary>
    /// Places LV cable on tiles beside the divider (not the door tile) so room lights stay on-network
    /// without feeding the unpowered pry airlock.
    /// </summary>
    private void PlaceRoomLightCables(EntityUid gridUid, Vector2i doorTile)
    {
        var apcTile = doorTile + new Vector2i(0, 1);
        SpawnAnchored(TutorialApcAlwaysOnProto, gridUid, apcTile);
        SpawnAnchored(CableApcExtensionProto, gridUid, doorTile + new Vector2i(-1, 0));
        SpawnAnchored(CableApcExtensionProto, gridUid, doorTile + new Vector2i(1, 0));
        SpawnAnchored(CableApcExtensionProto, gridUid, apcTile);
    }
}
