using System.Numerics;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Doors.Systems;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Automatic paired-airlock sterilization chamber controlled by a floor vent entity.
/// Cycle: entrance closes -> both doors close -> bolt both -> fog -> fade -> sterilize -> open exit.
/// Both doors remain bolted for the entire sterilization sequence.
/// </summary>
public sealed class SterilizationAirlockSystem : EntitySystem
{
    public static readonly ProtoId<SinkPortPrototype> DoorAPort = "SterilizerDoorA";
    public static readonly ProtoId<SinkPortPrototype> DoorBPort = "SterilizerDoorB";
    public static readonly ProtoId<SinkPortPrototype> QuarantineLockPort = "SterilizerQuarantineLock";

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly GridPathogenAtmosphereSystem _gridPathogen = default!;
    [Dependency] private readonly SurgicalAsepsisSystem _asepsis = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly DoorSystem _doors = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SterilizationAirlockControllerComponent, ComponentInit>(OnControllerInit);
        SubscribeLocalEvent<SterilizationAirlockControllerComponent, ComponentStartup>(OnControllerStartup);
        SubscribeLocalEvent<SterilizationAirlockControllerComponent, ComponentShutdown>(OnControllerShutdown);
        SubscribeLocalEvent<SterilizationAirlockControllerComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<SterilizationAirlockControllerComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<SterilizationDoorLockComponent, BeforeDoorOpenedEvent>(OnBeforeLockedDoorOpened);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<SterilizationAirlockControllerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var controller, out var xform))
        {
            if (controller.Phase == SterilizationControllerPhase.Idle ||
                controller.Phase == SterilizationControllerPhase.Fault)
                continue;

            if (controller.RequiresPower && !_power.IsPowered(uid))
            {
                Interrupt((uid, controller), "sol-sterilizer-unpowered");
                continue;
            }

            switch (controller.Phase)
            {
                case SterilizationControllerPhase.Closing:
                    UpdateClosing((uid, controller));
                    break;
                case SterilizationControllerPhase.Fogging:
                    EnsureBothDoorsBolted((uid, controller));
                    if (_timing.CurTime >= controller.PhaseEndsAt)
                        BeginFading((uid, controller));
                    break;
                case SterilizationControllerPhase.Fading:
                    EnsureBothDoorsBolted((uid, controller));
                    if (_timing.CurTime >= controller.PhaseEndsAt)
                        CompleteSterilization((uid, controller), xform);
                    break;
                case SterilizationControllerPhase.OpeningExit:
                    UpdateOpeningExit((uid, controller));
                    break;
            }
        }
    }

    private void OnControllerInit(Entity<SterilizationAirlockControllerComponent> ent, ref ComponentInit args)
    {
        _deviceLink.EnsureSinkPorts(ent, DoorAPort, DoorBPort, QuarantineLockPort);
    }

    private void OnControllerStartup(Entity<SterilizationAirlockControllerComponent> ent, ref ComponentStartup args)
    {
        SetVisual(ent, SterilizationControllerVisualState.Off);
    }

    private void OnControllerShutdown(Entity<SterilizationAirlockControllerComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.QuarantineLocked)
            SetQuarantineLock(ent, false);

        Interrupt(ent, null, reopenEntrance: false);
    }

    private void OnPowerChanged(Entity<SterilizationAirlockControllerComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered && ent.Comp.Phase != SterilizationControllerPhase.Idle)
            Interrupt(ent, "sol-sterilizer-unpowered");
    }

    private void OnSignalReceived(Entity<SterilizationAirlockControllerComponent> ent, ref SignalReceivedEvent args)
    {
        if (args.Port == QuarantineLockPort)
        {
            var state = SignalState.Momentary;
            args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);
            SetQuarantineLock(ent, state == SignalState.Momentary
                ? !ent.Comp.QuarantineLocked
                : state == SignalState.High);
            return;
        }

        if (args.Trigger is not { } door || !HasComp<DoorComponent>(door))
            return;

        if (args.Port == DoorAPort)
            ent.Comp.DoorA = door;
        else if (args.Port == DoorBPort)
            ent.Comp.DoorB = door;
        else
            return;

        Dirty(ent);

        if (!TryComp<DoorComponent>(door, out var doorComp))
            return;

        var signalState = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out signalState);
        var isOpen = signalState == SignalState.Momentary
            ? doorComp.State != DoorState.Closed
            : signalState == SignalState.High;

        // Paired open/bolt interlock only applies outside an active sterilization sequence.
        if (ent.Comp.Phase is SterilizationControllerPhase.Idle or SterilizationControllerPhase.Fault
            or SterilizationControllerPhase.OpeningExit)
        {
            UpdateDoorInterlock(ent, door, isOpen);
        }

        if (ent.Comp.Phase != SterilizationControllerPhase.Idle)
            return;

        var innerDoor = GetInnerDoor(ent.Comp);

        // Track which door was opened as the entrance for outbound travel.
        // A person opening the inner door themselves is outbound, not a follow-up cleanse.
        if (doorComp.State is DoorState.Open or DoorState.Opening)
        {
            ent.Comp.EntranceDoor = door;
            if (innerDoor == door)
                ent.Comp.AwaitingInnerResterilize = false;
            Dirty(ent);
            return;
        }

        if (doorComp.State != DoorState.Closed)
            return;

        // Closing the inner door always starts a sterilization cycle. Opening the lab-side
        // door can contaminate the chamber; it must be sterilized before the outer door opens.
        if (innerDoor == door)
        {
            ent.Comp.EntranceDoor = door;
            // Follow-up cleanse after inbound entry: sterilize, but do not reopen the outer door.
            ent.Comp.OpenExitAfterSterilization = !ent.Comp.AwaitingInnerResterilize;
            ent.Comp.AwaitingInnerResterilize = false;
            Dirty(ent);
            TryBeginCycle(ent);
            return;
        }

        // Outer door still starts a cycle when it was the remembered entrance.
        if (ent.Comp.EntranceDoor == door)
        {
            ent.Comp.OpenExitAfterSterilization = true;
            Dirty(ent);
            TryBeginCycle(ent);
        }
    }

    private void OnBeforeLockedDoorOpened(Entity<SterilizationDoorLockComponent> ent, ref BeforeDoorOpenedEvent args)
    {
        args.Cancel();
    }

    public bool TryBeginCycle(Entity<SterilizationAirlockControllerComponent> ent)
    {
        if (ent.Comp.Phase != SterilizationControllerPhase.Idle)
            return false;

        ResolveLinkedDoors(ent);

        if (ent.Comp.DoorA is not { } doorA ||
            ent.Comp.DoorB is not { } doorB ||
            !Exists(doorA) ||
            !Exists(doorB))
        {
            _popup.PopupEntity(Loc.GetString("sol-sterilizer-not-linked"), ent);
            return false;
        }

        if (ent.Comp.RequiresPower && !_power.IsPowered(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("sol-sterilizer-unpowered"), ent);
            return false;
        }

        if (!ValidateChamberGeometry(ent, doorA, doorB, out _))
        {
            _popup.PopupEntity(Loc.GetString("sol-sterilizer-invalid-geometry"), ent);
            return false;
        }

        var entrance = ent.Comp.EntranceDoor is { } remembered &&
                       (remembered == doorA || remembered == doorB) &&
                       Exists(remembered)
            ? remembered
            : doorA;

        var exit = entrance == doorA ? doorB : doorA;
        ent.Comp.EntranceDoor = entrance;
        ent.Comp.ExitDoor = exit;
        ent.Comp.Phase = SterilizationControllerPhase.Closing;
        ent.Comp.PhaseEndsAt = _timing.CurTime + ent.Comp.ClosingTimeout;
        Dirty(ent);
        SetVisual(ent, SterilizationControllerVisualState.Closing);

        EnsureComp<SterilizationDoorLockComponent>(doorA);
        EnsureComp<SterilizationDoorLockComponent>(doorB);
        _doors.TryClose(doorA);
        _doors.TryClose(doorB);

        _popup.PopupEntity(Loc.GetString("sol-sterilizer-started"), ent);
        return true;
    }

    private void UpdateClosing(Entity<SterilizationAirlockControllerComponent> ent)
    {
        if (ent.Comp.DoorA is not { } doorA ||
            ent.Comp.DoorB is not { } doorB ||
            !TryComp<DoorComponent>(doorA, out var doorAComp) ||
            !TryComp<DoorComponent>(doorB, out var doorBComp))
        {
            Interrupt(ent, "sol-sterilizer-interrupted");
            return;
        }

        if (doorAComp.State == DoorState.Closed && doorBComp.State == DoorState.Closed)
        {
            // Cycle closed: bolt both doors, then sterilize under lock.
            if (!EnsureBothDoorsBolted(ent))
            {
                Interrupt(ent, "sol-sterilizer-interrupted");
                return;
            }

            BeginFogging(ent);
            return;
        }

        if (_timing.CurTime >= ent.Comp.PhaseEndsAt)
            Interrupt(ent, "sol-sterilizer-interrupted");
    }

    private void BeginFogging(Entity<SterilizationAirlockControllerComponent> ent)
    {
        if (!ValidateChamberGeometry(ent, ent.Comp.DoorA!.Value, ent.Comp.DoorB!.Value, out var tiles))
        {
            Interrupt(ent, "sol-sterilizer-invalid-geometry");
            return;
        }

        if (!EnsureBothDoorsBolted(ent))
        {
            Interrupt(ent, "sol-sterilizer-interrupted");
            return;
        }

        ClearFog(ent);
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            Interrupt(ent, "sol-sterilizer-interrupted");
            return;
        }

        foreach (var tile in tiles)
        {
            var coords = _map.ToCoordinates(gridUid, tile, grid);
            var fog = Spawn(ent.Comp.FogPrototype, coords);
            var fogComp = EnsureComp<SterilizationFogComponent>(fog);
            fogComp.FadeStartsAt = _timing.CurTime + ent.Comp.FogDuration;
            fogComp.FadeEndsAt = fogComp.FadeStartsAt + ent.Comp.FadeDuration;
            Dirty(fog, fogComp);
            ent.Comp.ActiveFog.Add(fog);
        }

        ent.Comp.Phase = SterilizationControllerPhase.Fogging;
        ent.Comp.PhaseEndsAt = _timing.CurTime + ent.Comp.FogDuration;
        Dirty(ent);
        SetVisual(ent, SterilizationControllerVisualState.Fogging);
    }

    private void BeginFading(Entity<SterilizationAirlockControllerComponent> ent)
    {
        var now = _timing.CurTime;
        foreach (var fog in ent.Comp.ActiveFog)
        {
            if (!Exists(fog) || !TryComp<SterilizationFogComponent>(fog, out var fogComp))
                continue;

            fogComp.FadeStartsAt = now;
            fogComp.FadeEndsAt = now + ent.Comp.FadeDuration;
            Dirty(fog, fogComp);
        }

        ent.Comp.Phase = SterilizationControllerPhase.Fading;
        ent.Comp.PhaseEndsAt = now + ent.Comp.FadeDuration;
        Dirty(ent);
    }

    private void CompleteSterilization(Entity<SterilizationAirlockControllerComponent> ent, TransformComponent xform)
    {
        if (!ValidateChamberGeometry(ent, ent.Comp.DoorA!.Value, ent.Comp.DoorB!.Value, out var tiles))
        {
            Interrupt(ent, "sol-sterilizer-invalid-geometry");
            return;
        }

        // Sterilization only runs while both doors are bolted shut.
        if (!EnsureBothDoorsBolted(ent))
        {
            Interrupt(ent, "sol-sterilizer-interrupted");
            return;
        }

        SterilizeChamber(ent, xform, tiles);
        ClearFog(ent);

        // Follow-up cleanse after inbound transit: keep both doors sealed; do not reopen outer.
        if (!ent.Comp.OpenExitAfterSterilization)
        {
            FinishSealedIdle(ent);
            _popup.PopupEntity(Loc.GetString("sol-sterilizer-complete"), ent);
            return;
        }

        if (ent.Comp.ExitDoor is not { } exit || !Exists(exit))
        {
            Interrupt(ent, "sol-sterilizer-interrupted");
            return;
        }

        RemComp<SterilizationDoorLockComponent>(exit);
        if (ent.Comp.EntranceDoor is { } entrance && Exists(entrance))
        {
            EnsureComp<SterilizationDoorLockComponent>(entrance);
            TrySetDoorBolted(entrance, true);
        }

        // Release only the exit bolt so the chamber can open after sterilization.
        TrySetDoorBolted(exit, false);

        // Opening the inner door as the cycle exit means the next inner close is a follow-up cleanse.
        if (exit == GetInnerDoor(ent.Comp))
            ent.Comp.AwaitingInnerResterilize = true;

        ent.Comp.Phase = SterilizationControllerPhase.OpeningExit;
        ent.Comp.PhaseEndsAt = _timing.CurTime + ent.Comp.ClosingTimeout;
        Dirty(ent);
        SetVisual(ent, SterilizationControllerVisualState.Closing);
        _doors.TryOpen(exit);
        _popup.PopupEntity(Loc.GetString("sol-sterilizer-complete"), ent);
    }

    private void UpdateOpeningExit(Entity<SterilizationAirlockControllerComponent> ent)
    {
        if (ent.Comp.ExitDoor is not { } exit || !TryComp<DoorComponent>(exit, out var door))
        {
            Interrupt(ent, "sol-sterilizer-interrupted");
            return;
        }

        if (door.State == DoorState.Open)
        {
            FinishIdle(ent);
            return;
        }

        if (_timing.CurTime >= ent.Comp.PhaseEndsAt)
        {
            ent.Comp.Phase = SterilizationControllerPhase.Fault;
            Dirty(ent);
            SetVisual(ent, SterilizationControllerVisualState.Fault);
            _popup.PopupEntity(Loc.GetString("sol-sterilizer-interrupted"), ent);
        }
    }

    private void SterilizeChamber(
        Entity<SterilizationAirlockControllerComponent> ent,
        TransformComponent xform,
        List<Vector2i> tiles)
    {
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var strength = 100f * ent.Comp.SterilizationStrength;
        foreach (var tile in tiles)
            _gridPathogen.RemoveAirborneLoad(gridUid, tile, strength);

        var seen = new HashSet<EntityUid>();
        foreach (var tile in tiles)
        {
            var coords = _map.ToCoordinates(gridUid, tile, grid);
            foreach (var entity in _lookup.GetEntitiesInRange(coords, 0.55f))
            {
                if (!seen.Add(entity))
                    continue;

                if (!TryComp<SurfaceContaminationComponent>(entity, out var surface))
                    continue;

                surface.Contaminants.Clear();
                surface.IsDirty = false;
                Dirty(entity, surface);

                if (TryComp<SurgicalToolSterilityComponent>(entity, out var sterility))
                    _asepsis.TryWash((entity, sterility), ent, sterilize: true);
            }
        }
    }

    private void ResolveLinkedDoors(Entity<SterilizationAirlockControllerComponent> ent)
    {
        var query = EntityQueryEnumerator<DoorComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            foreach (var (sourcePort, sinkPort) in _deviceLink.GetLinks(uid, ent.Owner))
            {
                if (sourcePort != "DoorStatus")
                    continue;

                if (sinkPort == DoorAPort)
                    ent.Comp.DoorA = uid;
                else if (sinkPort == DoorBPort)
                    ent.Comp.DoorB = uid;
            }
        }

        Dirty(ent);
    }

    private void UpdateDoorInterlock(
        Entity<SterilizationAirlockControllerComponent> ent,
        EntityUid changedDoor,
        bool isOpen)
    {
        // Never release bolts while a sterilization sequence is sealing the chamber.
        if (ent.Comp.Phase is SterilizationControllerPhase.Closing
            or SterilizationControllerPhase.Fogging
            or SterilizationControllerPhase.Fading)
        {
            EnsureBothDoorsBolted(ent);
            return;
        }

        var otherDoor = changedDoor == ent.Comp.DoorA
            ? ent.Comp.DoorB
            : changedDoor == ent.Comp.DoorB
                ? ent.Comp.DoorA
                : null;

        if (otherDoor is not { } other || !TryComp<DoorBoltComponent>(other, out var bolts))
            return;

        var quarantineRequiresBolt = ent.Comp.QuarantineLocked && other == GetQuarantineDoor(ent.Comp);
        var shouldBolt = isOpen || quarantineRequiresBolt;
        if (bolts.BoltsDown != shouldBolt)
            _doors.TrySetBoltDown((other, bolts), shouldBolt);
    }

    private bool EnsureBothDoorsBolted(Entity<SterilizationAirlockControllerComponent> ent)
    {
        var boltedA = ent.Comp.DoorA is { } doorA && Exists(doorA) && TrySetDoorBolted(doorA, true);
        var boltedB = ent.Comp.DoorB is { } doorB && Exists(doorB) && TrySetDoorBolted(doorB, true);
        return boltedA && boltedB;
    }

    private bool TrySetDoorBolted(EntityUid door, bool bolted)
    {
        if (!TryComp<DoorBoltComponent>(door, out var bolts))
            return false;

        if (bolts.BoltsDown == bolted)
            return true;

        return _doors.TrySetBoltDown((door, bolts), bolted);
    }

    private void SetQuarantineLock(Entity<SterilizationAirlockControllerComponent> ent, bool locked)
    {
        ResolveLinkedDoors(ent);
        var outerDoor = ent.Comp.QuarantineDoor == SterilizationControllerDoor.A
            ? ent.Comp.DoorA
            : ent.Comp.DoorB;

        if (outerDoor is not { } door || !TryComp<DoorBoltComponent>(door, out var bolts))
        {
            _popup.PopupEntity(Loc.GetString("sol-sterilizer-not-linked"), ent);
            return;
        }

        if (locked)
            _doors.TryClose(door);

        if (bolts.BoltsDown != locked && !_doors.TrySetBoltDown((door, bolts), locked))
        {
            _popup.PopupEntity(Loc.GetString("sol-sterilizer-quarantine-lock-failed"), ent);
            return;
        }

        ent.Comp.QuarantineLocked = locked;
        Dirty(ent);
    }

    private bool ValidateChamberGeometry(
        Entity<SterilizationAirlockControllerComponent> ent,
        EntityUid doorA,
        EntityUid doorB,
        out List<Vector2i> tiles)
    {
        tiles = new List<Vector2i>();
        var xform = Transform(ent);
        var xformA = Transform(doorA);
        var xformB = Transform(doorB);

        if (xform.GridUid is not { } gridUid ||
            xformA.GridUid != gridUid ||
            xformB.GridUid != gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return false;
        }

        var tileA = _map.GetTileRef(gridUid, grid, xformA.Coordinates).GridIndices;
        var tileB = _map.GetTileRef(gridUid, grid, xformB.Coordinates).GridIndices;
        var tileC = _map.GetTileRef(gridUid, grid, xform.Coordinates).GridIndices;

        if (tileA.X == tileB.X)
        {
            var minY = Math.Min(tileA.Y, tileB.Y);
            var maxY = Math.Max(tileA.Y, tileB.Y);
            if (maxY - minY is < 2 or > 5)
                return false;
            if (tileC.X != tileA.X || tileC.Y <= minY || tileC.Y >= maxY)
                return false;

            for (var y = minY + 1; y < maxY; y++)
                tiles.Add(new Vector2i(tileA.X, y));
            return true;
        }

        if (tileA.Y == tileB.Y)
        {
            var minX = Math.Min(tileA.X, tileB.X);
            var maxX = Math.Max(tileA.X, tileB.X);
            if (maxX - minX is < 2 or > 5)
                return false;
            if (tileC.Y != tileA.Y || tileC.X <= minX || tileC.X >= maxX)
                return false;

            for (var x = minX + 1; x < maxX; x++)
                tiles.Add(new Vector2i(x, tileA.Y));
            return true;
        }

        return false;
    }

    private void Interrupt(
        Entity<SterilizationAirlockControllerComponent> ent,
        string? locale,
        bool reopenEntrance = true)
    {
        ClearFog(ent);

        if (ent.Comp.DoorA is { } doorA && Exists(doorA))
            RemComp<SterilizationDoorLockComponent>(doorA);
        if (ent.Comp.DoorB is { } doorB && Exists(doorB))
            RemComp<SterilizationDoorLockComponent>(doorB);

        ReleaseCycleBolts(ent);

        var entrance = ent.Comp.EntranceDoor;
        ent.Comp.Phase = SterilizationControllerPhase.Idle;
        ent.Comp.PhaseEndsAt = TimeSpan.Zero;
        ent.Comp.ExitDoor = null;
        Dirty(ent);
        SetVisual(ent, SterilizationControllerVisualState.Off);

        if (locale != null)
            _popup.PopupEntity(Loc.GetString(locale), ent);

        if (reopenEntrance &&
            entrance is { } entranceDoor &&
            Exists(entranceDoor) &&
            (!ent.Comp.QuarantineLocked || entranceDoor != GetQuarantineDoor(ent.Comp)) &&
            (!ent.Comp.RequiresPower || _power.IsPowered(ent.Owner)))
        {
            TrySetDoorBolted(entranceDoor, false);
            _doors.TryOpen(entranceDoor);
        }
    }

    private void FinishIdle(Entity<SterilizationAirlockControllerComponent> ent)
    {
        if (ent.Comp.DoorA is { } doorA && Exists(doorA))
            RemComp<SterilizationDoorLockComponent>(doorA);
        if (ent.Comp.DoorB is { } doorB && Exists(doorB))
            RemComp<SterilizationDoorLockComponent>(doorB);

        // Exit stays open; keep the opposite door bolted via normal interlock behavior.
        if (ent.Comp.ExitDoor is { } exit && Exists(exit))
            UpdateDoorInterlock(ent, exit, isOpen: true);

        // Preserve quarantine bolting on the outer door if still engaged.
        if (ent.Comp.QuarantineLocked && GetQuarantineDoor(ent.Comp) is { } outer)
            TrySetDoorBolted(outer, true);

        ent.Comp.Phase = SterilizationControllerPhase.Idle;
        ent.Comp.PhaseEndsAt = TimeSpan.Zero;
        ent.Comp.EntranceDoor = null;
        ent.Comp.ExitDoor = null;
        ent.Comp.OpenExitAfterSterilization = true;
        Dirty(ent);
        SetVisual(ent, SterilizationControllerVisualState.Off);
    }

    /// <summary>
    /// Ends a follow-up cleanse with both doors closed and bolted so the outer door is not reopened.
    /// </summary>
    private void FinishSealedIdle(Entity<SterilizationAirlockControllerComponent> ent)
    {
        if (ent.Comp.DoorA is { } doorA && Exists(doorA))
            RemComp<SterilizationDoorLockComponent>(doorA);
        if (ent.Comp.DoorB is { } doorB && Exists(doorB))
            RemComp<SterilizationDoorLockComponent>(doorB);

        EnsureBothDoorsBolted(ent);

        ent.Comp.Phase = SterilizationControllerPhase.Idle;
        ent.Comp.PhaseEndsAt = TimeSpan.Zero;
        ent.Comp.EntranceDoor = null;
        ent.Comp.ExitDoor = null;
        ent.Comp.AwaitingInnerResterilize = false;
        ent.Comp.OpenExitAfterSterilization = true;
        Dirty(ent);
        SetVisual(ent, SterilizationControllerVisualState.Off);
    }

    private void ReleaseCycleBolts(Entity<SterilizationAirlockControllerComponent> ent)
    {
        if (ent.Comp.DoorA is { } doorA && Exists(doorA))
        {
            var keepBolted = ent.Comp.QuarantineLocked && doorA == GetQuarantineDoor(ent.Comp);
            TrySetDoorBolted(doorA, keepBolted);
        }

        if (ent.Comp.DoorB is { } doorB && Exists(doorB))
        {
            var keepBolted = ent.Comp.QuarantineLocked && doorB == GetQuarantineDoor(ent.Comp);
            TrySetDoorBolted(doorB, keepBolted);
        }
    }

    private static EntityUid? GetQuarantineDoor(SterilizationAirlockControllerComponent component)
    {
        return component.QuarantineDoor == SterilizationControllerDoor.A
            ? component.DoorA
            : component.DoorB;
    }

    private static EntityUid? GetInnerDoor(SterilizationAirlockControllerComponent component)
    {
        return component.QuarantineDoor == SterilizationControllerDoor.A
            ? component.DoorB
            : component.DoorA;
    }

    private void ClearFog(Entity<SterilizationAirlockControllerComponent> ent)
    {
        foreach (var fog in ent.Comp.ActiveFog)
        {
            if (Exists(fog))
                QueueDel(fog);
        }

        ent.Comp.ActiveFog.Clear();
    }

    private void SetVisual(EntityUid uid, SterilizationControllerVisualState state)
    {
        _appearance.SetData(uid, SterilizationControllerVisuals.State, state);
    }
}
