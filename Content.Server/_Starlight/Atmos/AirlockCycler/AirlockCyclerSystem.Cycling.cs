using Content.Shared._Starlight.Atmos.AirlockCycler;
using Content.Shared.DeviceLinking;
using Content.Shared.Doors.Components;

namespace Content.Server._Starlight.Atmos.AirlockCycler;

public sealed partial class AirlockCyclerSystem
{
    /// <summary>
    /// Ask the controller to cycle towards <paramref name="side"/>. While another cycle runs the request is queued
    /// and starts once that cycle is done. Asking again for a side whose cycle is stuck opens it anyway.
    /// </summary>
    /// <param name="popupAt">Where refusals pop up: the pressed button, so they are seen even with the panel out of view.</param>
    public void RequestCycle(Entity<AirlockCyclerComponent?> ent, int side, EntityUid? user, EntityUid? popupAt = null)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        var comp = ent.Comp;
        if (side < 0 || side >= comp.Sides.Count)
            return;

        if (comp.State == AirlockCyclerState.Off)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("airlock-cycler-no-power"), popupAt ?? ent, user.Value);
            return;
        }

        if (!HasSideAccess((Entity<AirlockCyclerComponent>)ent!, side, user))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("airlock-cycler-access-denied"), popupAt ?? ent, user.Value);
            return;
        }

        var cycling = IsCycling(comp.State);
        if (cycling && comp.TargetSide == side)
        {
            if (comp.Problem == AirlockCyclerProblem.Stalled)
                comp.Force = true;

            comp.NextUpdate = _timing.CurTime;
            return;
        }

        // Busy: someone is cycling (inside the chamber, or about to come in), or others are already waiting. A
        // stuck cycle is not worth waiting for, so a new request replaces it.
        if ((cycling && !IsStuck(comp)) || (comp.State == AirlockCyclerState.Idle && comp.Queue.Count > 0))
        {
            if (!comp.Queue.Exists(r => r.Side == side))
                comp.Queue.Add(new AirlockCyclerRequest { Side = side, Requester = user });

            if (user != null)
                _popup.PopupEntity(Loc.GetString("airlock-cycler-queued"), popupAt ?? ent, user.Value);

            comp.NextUpdate = _timing.CurTime;
            return;
        }

        StartCycle((Entity<AirlockCyclerComponent>)ent!, side, user);
    }

    /// <summary>
    /// The running cycle waits on something only a person can fix (or a forced open).
    /// </summary>
    private static bool IsStuck(AirlockCyclerComponent comp)
        => comp.Problem is AirlockCyclerProblem.Stalled
            or AirlockCyclerProblem.NoReading
            or AirlockCyclerProblem.IncompatiblePressure
            or AirlockCyclerProblem.NoInlet
            or AirlockCyclerProblem.NoOutlet
            or AirlockCyclerProblem.DoorUnsecured;

    private void StartCycle(Entity<AirlockCyclerComponent> ent, int side, EntityUid? user)
    {
        var comp = ent.Comp;
        comp.Queue.RemoveAll(r => r.Side == side);
        comp.Lockdown = false;
        comp.TargetSide = side;
        comp.Requester = user;
        comp.Force = false;
        comp.SettleUntil = null;
        comp.Problem = AirlockCyclerProblem.None;
        SetState(ent, AirlockCyclerState.Closing);

        // SetState only refreshes the lights when cycling starts or stops; a redirect (A -> B while cycling) keeps
        // the state, so the lights must follow the new target here.
        UpdateButtons(ent);
        comp.NextUpdate = _timing.CurTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AirlockCyclerComponent, DeviceLinkSourceComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var source, out var xform))
        {
            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + comp.UpdateInterval;
            var ent = (uid, comp);

            if (!comp.Reconciled)
                Reconcile(ent, source);

            ProcessPendingReleases(ent, source);

            if (!_power.IsPowered(uid))
            {
                if (comp.State != AirlockCyclerState.Off)
                    TurnOff(ent, source);
                continue;
            }

            if (comp.State == AirlockCyclerState.Off)
            {
                SetState(ent, comp.Interrupted ? AirlockCyclerState.Closing : AirlockCyclerState.Idle);
                comp.Interrupted = false;
            }

            // The opened side had its few seconds: on to the next waiting request.
            if (comp.State == AirlockCyclerState.Idle && comp.Queue.Count > 0 && now >= comp.QueueReadyAt)
            {
                var next = comp.Queue[0];
                comp.Queue.RemoveAt(0);
                if (next.Side < comp.Sides.Count)
                    StartCycle(ent, next.Side, next.Requester);
            }

            // Every update, not only on power-up or relink: a non-target door that was open then (or got opened by
            // hand since) must be shut and bolted, or an authorized user could open both sides at once.
            if (comp.State == AirlockCyclerState.Idle)
                SecureIdleDoors(ent, source);

            var uiOpen = _ui.IsUiOpen(uid, AirlockCyclerUiKey.Key);
            if (comp.State == AirlockCyclerState.Idle && !uiOpen)
                continue;

            var layout = BuildLayout(ent, source, xform);

            if (comp.State != AirlockCyclerState.Idle)
                Process(ent, source, layout, now);

            if (uiOpen)
                UpdateUi(ent, layout);
        }
    }

    private void Process(Entity<AirlockCyclerComponent> ent, DeviceLinkSourceComponent source, Layout layout, TimeSpan now)
    {
        var comp = ent.Comp;
        if (comp.TargetSide is not { } target || target >= layout.Sides.Count)
        {
            Finish(ent, source, layout);
            return;
        }

        // Shut and bolt every door that is not on the target side.
        var othersShut = true;
        var unsecured = false;
        var targetOpen = false;
        var targetSettling = false;
        for (var i = 0; i < layout.Sides.Count; i++)
        {
            foreach (var doorInfo in layout.Sides[i].Doors)
            {
                var door = doorInfo.Door;
                if (!_doorQuery.TryComp(door, out var doorComp))
                    continue;

                switch (doorComp.State)
                {
                    case DoorState.Closed:
                    case DoorState.Welded:
                        Bolt(door, true);
                        if (i != target && !IsSecured(door, doorComp))
                            unsecured = true;
                        break;
                    case DoorState.Open or DoorState.Opening when i == target:
                        targetOpen = true;
                        break;
                    case DoorState.Open:
                        othersShut = false;
                        Bolt(door, false); // bolted open doors refuse to close
                        if (!_doors.TryClose(door, doorComp))
                            comp.Problem = AirlockCyclerProblem.DoorObstructed;
                        break;
                    default:
                        // Closing, opening, denying: wait for it to settle. A closing target door still lets air
                        // through, so pumping now would waste gas into (or pull it from) the next room.
                        if (i != target)
                            othersShut = false;
                        else
                            targetSettling = true;
                        break;
                }
            }
        }

        if (!othersShut)
        {
            StopDevices(ent, source);
            SetState(ent, AirlockCyclerState.Closing);
            return;
        }

        if (unsecured)
        {
            Hold(ent, source, AirlockCyclerProblem.DoorUnsecured);
            return;
        }

        if (comp.Problem is AirlockCyclerProblem.DoorObstructed or AirlockCyclerProblem.DoorUnsecured)
            comp.Problem = AirlockCyclerProblem.None;

        // Forced, or no vent (a plain interlock, nothing to pump).
        if (comp.Force || layout.NoDevices)
        {
            Finish(ent, source, layout);
            return;
        }

        if (targetSettling)
        {
            StopDevices(ent, source);
            SetState(ent, AirlockCyclerState.Closing);
            return;
        }

        // Never open on a guess: without a reading for every target door, or with target doors leading to different
        // pressures, keep everything shut until someone forces it from the console. This holds for the rest of the
        // target doors too when one of them is already open.
        var side = layout.Sides[target];
        if (side.MissingReading || side.OutsidePressure is not { } wanted)
        {
            Hold(ent, source, AirlockCyclerProblem.NoReading);
            return;
        }

        if (side.OutsideMax - side.OutsideMin > comp.IncompatiblePressure)
        {
            Hold(ent, source, AirlockCyclerProblem.IncompatiblePressure);
            return;
        }

        if (targetOpen)
        {
            Finish(ent, source, layout);
            return;
        }

        if (layout.ChamberPressure is not { } chamber || layout.ChamberLeaks)
        {
            Hold(ent, source, AirlockCyclerProblem.NoReading);
            return;
        }

        if (comp.Problem is AirlockCyclerProblem.NoReading or AirlockCyclerProblem.IncompatiblePressure)
            comp.Problem = AirlockCyclerProblem.None;

        // Done only when every chamber tile matches: the vent's own tile settles first.
        var worst = MathF.Max(MathF.Abs(layout.ChamberMin - wanted), MathF.Abs(layout.ChamberMax - wanted));
        if (worst <= comp.PressureTolerance)
        {
            comp.SettleUntil ??= now + comp.SettleTime;
            if (worst <= comp.MatchedPressure || now >= comp.SettleUntil)
            {
                Finish(ent, source, layout);
                return;
            }
        }
        else
        {
            comp.SettleUntil = null;
        }

        var state = chamber < wanted ? AirlockCyclerState.Pressurizing : AirlockCyclerState.Depressurizing;
        var pressurizing = state == AirlockCyclerState.Pressurizing;

        // Nothing can move the gas this way: hold, like a missing reading, until forced. Within tolerance nothing
        // needs moving, so the settle timer above opens the doors instead.
        if (worst > comp.PressureTolerance && (pressurizing ? layout.Inlets : layout.Outlets).Count == 0)
        {
            Hold(ent, source, pressurizing ? AirlockCyclerProblem.NoInlet : AirlockCyclerProblem.NoOutlet);
            return;
        }

        if (comp.Problem is AirlockCyclerProblem.NoInlet or AirlockCyclerProblem.NoOutlet)
            comp.Problem = AirlockCyclerProblem.None;

        if (comp.State != state)
        {
            SetState(ent, state);
            comp.LastProgress = now;
            comp.LastProgressPressure = chamber;
        }

        DriveDevices(layout, pressurizing, wanted);

        if (MathF.Abs(chamber - comp.LastProgressPressure) >= comp.StallPressure)
        {
            comp.LastProgress = now;
            comp.LastProgressPressure = chamber;
            if (comp.Problem == AirlockCyclerProblem.Stalled)
                comp.Problem = AirlockCyclerProblem.None;
        }
        else if (now - comp.LastProgress >= comp.StallTime)
        {
            comp.Problem = AirlockCyclerProblem.Stalled;
        }
    }

    private void Hold(Entity<AirlockCyclerComponent> ent, DeviceLinkSourceComponent source, AirlockCyclerProblem problem)
    {
        StopDevices(ent, source);
        ent.Comp.SettleUntil = null;
        ent.Comp.Problem = problem;
        SetState(ent, AirlockCyclerState.Closing);
    }

    private void Finish(Entity<AirlockCyclerComponent> ent, DeviceLinkSourceComponent source, Layout layout)
    {
        var comp = ent.Comp;
        StopDevices(ent, source);

        if (comp.TargetSide is { } target && target < layout.Sides.Count)
        {
            foreach (var doorInfo in layout.Sides[target].Doors)
            {
                Bolt(doorInfo.Door, false);
                if (_doorQuery.TryComp(doorInfo.Door, out var door) && door.State == DoorState.Closed)
                    _doors.TryOpen(doorInfo.Door, door, comp.Requester, quiet: true);
            }
        }

        comp.Force = false;
        comp.Interrupted = false;
        comp.Requester = null;
        comp.SettleUntil = null;
        comp.Problem = layout.NoDevices ? AirlockCyclerProblem.NoVent : AirlockCyclerProblem.None;
        comp.QueueReadyAt = _timing.CurTime + comp.QueueDelay;
        SetState(ent, AirlockCyclerState.Idle);
    }

    private void TurnOff(Entity<AirlockCyclerComponent> ent, DeviceLinkSourceComponent source)
    {
        // Without power the chamber is just a pair of doors: let people through by hand. The doors are likely
        // unpowered too, so the bolts are released without the usual power check.
        for (var i = 0; i < ent.Comp.Sides.Count; i++)
            foreach (var linked in GetOwnedSinks(ent, source, ent.Comp.Sides[i]))
                ReleaseBolts(linked);

        StopDevices(ent, source);
        ent.Comp.Interrupted |= IsCycling(ent.Comp.State);
        ent.Comp.Force = false;
        ent.Comp.SettleUntil = null;
        ent.Comp.Problem = AirlockCyclerProblem.None;
        SetState(ent, AirlockCyclerState.Off);
    }

    /// <summary>
    /// Used at spawn/load(map init) to reset controller state to Idle
    /// </summary>
    private void Reconcile(Entity<AirlockCyclerComponent> ent, DeviceLinkSourceComponent source)
    {
        var comp = ent.Comp;
        comp.Reconciled = true;
        comp.SettleUntil = null;
        StopDevices(ent, source);

        if (IsCycling(comp.State))
            comp.State = AirlockCyclerState.Closing;

        _appearance.SetData(ent, AirlockCyclerVisuals.State, comp.State);
        UpdateButtons(ent);
    }

    private void SecureIdleDoors(Entity<AirlockCyclerComponent> ent, DeviceLinkSourceComponent source)
    {
        var obstructed = false;
        var unsecured = false;
        for (var i = 0; i < ent.Comp.Sides.Count; i++)
        {
            if (i == ent.Comp.TargetSide)
                continue;

            foreach (var linked in GetOwnedSinks(ent, source, ent.Comp.Sides[i]))
            {
                if (!_doorQuery.TryComp(linked, out var door))
                    continue;

                switch (door.State)
                {
                    case DoorState.Closed:
                    case DoorState.Welded:
                        Bolt(linked, true);
                        if (!IsSecured(linked, door))
                            unsecured = true;
                        break;
                    case DoorState.Open:
                        Bolt(linked, false);
                        if (!_doors.TryClose(linked, door))
                            obstructed = true;
                        break;
                }
            }
        }

        if (obstructed)
            ent.Comp.Problem = AirlockCyclerProblem.DoorObstructed;
        else if (unsecured)
            ent.Comp.Problem = AirlockCyclerProblem.DoorUnsecured;
        else if (ent.Comp.Problem is AirlockCyclerProblem.DoorObstructed or AirlockCyclerProblem.DoorUnsecured)
            ent.Comp.Problem = AirlockCyclerProblem.None;
    }

    private bool IsSecured(EntityUid door, DoorComponent comp)
        => comp.State == DoorState.Welded || (_boltQuery.TryComp(door, out var bolt) && bolt.BoltsDown);

    private void Bolt(EntityUid door, bool bolted)
    {
        if (_boltQuery.TryComp(door, out var bolt) && bolt.BoltsDown != bolted)
            _doors.SetBoltsDown((door, bolt), bolted);
    }

    private void ReleaseBolts(EntityUid door)
    {
        if (_boltQuery.TryComp(door, out var bolt))
            _doors.ForceSetBoltsDown((door, bolt), false);
    }

    private void SetState(Entity<AirlockCyclerComponent> ent, AirlockCyclerState state)
    {
        if (ent.Comp.State == state)
            return;

        var wasCycling = IsCycling(ent.Comp.State);
        ent.Comp.State = state;
        _appearance.SetData(ent, AirlockCyclerVisuals.State, state);

        if (wasCycling != IsCycling(state) || state is AirlockCyclerState.Off or AirlockCyclerState.Idle)
            UpdateButtons(ent);
    }

    private static bool IsCycling(AirlockCyclerState state)
        => state is AirlockCyclerState.Closing or AirlockCyclerState.Pressurizing or AirlockCyclerState.Depressurizing;

    private void UpdateButtons(Entity<AirlockCyclerComponent> ent)
    {
        if (!TryComp<DeviceLinkSourceComponent>(ent, out var source))
            return;

        for (var i = 0; i < ent.Comp.Sides.Count; i++)
        {
            var state = ent.Comp.State switch
            {
                AirlockCyclerState.Off => AirlockCyclerButtonState.Off,
                _ when IsCycling(ent.Comp.State) && ent.Comp.TargetSide == i => AirlockCyclerButtonState.Cycling,
                _ => AirlockCyclerButtonState.Standby,
            };

            foreach (var linked in GetOwnedSinks(ent, source, ent.Comp.Sides[i]))
            {
                if (_buttonQuery.HasComp(linked))
                    _appearance.SetData(linked, AirlockCyclerButtonVisuals.State, state);
            }
        }
    }

    private void StartLockdown(Entity<AirlockCyclerComponent> ent)
    {
        var comp = ent.Comp;
        if (TryComp<DeviceLinkSourceComponent>(ent, out var source))
            StopDevices(ent, source);

        comp.Lockdown = true;
        comp.Queue.Clear();
        comp.TargetSide = null;
        comp.Requester = null;
        comp.Force = false;
        comp.Interrupted = false;
        comp.SettleUntil = null;
        comp.Problem = AirlockCyclerProblem.None;
        SetState(ent, AirlockCyclerState.Idle);
        UpdateButtons(ent);
        comp.NextUpdate = _timing.CurTime;
    }

    private bool HasAnySideAccess(Entity<AirlockCyclerComponent> ent, EntityUid user)
    {
        if (!TryComp<DeviceLinkSourceComponent>(ent, out var source))
            return true;

        var anyDoor = false;
        for (var i = 0; i < ent.Comp.Sides.Count; i++)
        {
            foreach (var linked in GetOwnedSinks(ent, source, ent.Comp.Sides[i]))
            {
                if (!_doorQuery.HasComp(linked))
                    continue;

                anyDoor = true;
                if (_access.IsAllowed(user, linked))
                    return true;
            }
        }

        return !anyDoor;
    }

    private bool HasSideAccess(Entity<AirlockCyclerComponent> ent, int side, EntityUid? user)
    {
        if (user is not { } actor || !TryComp<DeviceLinkSourceComponent>(ent, out var source))
            return true;

        var anyDoor = false;
        foreach (var linked in GetOwnedSinks(ent, source, ent.Comp.Sides[side]))
        {
            if (!_doorQuery.HasComp(linked))
                continue;

            anyDoor = true;
            if (_access.IsAllowed(actor, linked))
                return true;
        }

        return !anyDoor;
    }

    private bool TryGetController(EntityUid button, out Entity<AirlockCyclerComponent> controller, out int side)
    {
        controller = default;
        side = -1;

        if (!TryComp<DeviceLinkSinkComponent>(button, out var sink))
            return false;

        foreach (var sourceUid in sink.LinkedSources)
        {
            if (!TryComp<AirlockCyclerComponent>(sourceUid, out var comp)
                || !TryComp<DeviceLinkSourceComponent>(sourceUid, out var source)
                || !OwnsDevice(sourceUid, button))
                continue;

            foreach (var (port, _) in _deviceLink.GetLinks(sourceUid, button, source))
            {
                var index = comp.Sides.IndexOf(port);
                if (index < 0)
                    continue;

                controller = (sourceUid, comp);
                side = index;
                return true;
            }
        }

        return false;
    }
}
