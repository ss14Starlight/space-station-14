using Content.Shared._Starlight.Atmos.AirlockCycler;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.Doors.Components;

namespace Content.Server._Starlight.Atmos.AirlockCycler;

public sealed partial class AirlockCyclerSystem
{
    /// <summary>
    /// Ask the controller to cycle towards <paramref name="side"/>. Asking again for a side whose cycle is stuck opens it anyway.
    /// </summary>
    public void RequestCycle(Entity<AirlockCyclerComponent> ent, int side, EntityUid? user)
    {
        var comp = ent.Comp;
        if (side < 0 || side >= comp.Sides.Count)
            return;

        if (comp.State == AirlockCyclerState.Off)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("airlock-cycler-no-power"), ent, user.Value);
            return;
        }

        if (!HasSideAccess(ent, side, user))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("airlock-cycler-access-denied"), ent, user.Value);
            return;
        }

        if (comp.State != AirlockCyclerState.Idle && comp.TargetSide == side)
        {
            if (comp.Problem == AirlockCyclerProblem.Stalled)
                comp.Force = true;

            comp.NextUpdate = _timing.CurTime;
            return;
        }

        comp.TargetSide = side;
        comp.Requester = user;
        comp.Force = false;
        comp.SettleUntil = null;
        comp.Problem = AirlockCyclerProblem.None;
        SetState(ent, AirlockCyclerState.Closing);
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

            if (!_power.IsPowered(uid))
            {
                if (comp.State != AirlockCyclerState.Off)
                    TurnOff(ent, source);
                continue;
            }

            if (comp.State == AirlockCyclerState.Off)
            {
                SetState(ent, AirlockCyclerState.Idle);
                LockIdleDoors(ent, source);
            }
            else if (comp.LockPending && comp.State == AirlockCyclerState.Idle)
            {
                LockIdleDoors(ent, source);
            }

            comp.LockPending = false;

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
        var targetOpen = false;
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
                        break;
                    case DoorState.Open when i == target:
                        targetOpen = true;
                        break;
                    case DoorState.Open:
                        othersShut = false;
                        Bolt(door, false); // bolted open doors refuse to close
                        if (!_doors.TryClose(door, doorComp))
                            comp.Problem = AirlockCyclerProblem.DoorObstructed;
                        break;
                    case DoorState.Opening when i == target:
                        targetOpen = true;
                        break;
                    default:
                        // Closing, opening, denying: wait for it to settle.
                        if (i != target)
                            othersShut = false;
                        break;
                }
            }
        }

        if (!othersShut)
        {
            SetState(ent, AirlockCyclerState.Closing);
            return;
        }

        if (comp.Problem == AirlockCyclerProblem.DoorObstructed)
            comp.Problem = AirlockCyclerProblem.None;

        var targetPressure = layout.Sides[target].OutsidePressure;
        if (comp.Force || targetOpen || layout.Vents.Count == 0 || targetPressure == null || layout.ChamberPressure == null)
        {
            Finish(ent, source, layout);
            return;
        }

        var chamber = layout.ChamberPressure.Value;
        var wanted = targetPressure.Value;
        var diff = MathF.Abs(chamber - wanted);
        if (diff <= comp.PressureTolerance)
        {
            comp.SettleUntil ??= now + comp.SettleTime;
            if (diff <= comp.MatchedPressure || now >= comp.SettleUntil)
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
        if (comp.State != state)
        {
            SetState(ent, state);
            comp.LastProgress = now;
            comp.LastProgressPressure = chamber;
        }

        var direction = state == AirlockCyclerState.Pressurizing ? VentPumpDirection.Releasing : VentPumpDirection.Siphoning;
        foreach (var vent in layout.Vents)
        {
            _vents.SetAirlockCyclerMode(vent, true, direction, wanted);
        }

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

    private void Finish(Entity<AirlockCyclerComponent> ent, DeviceLinkSourceComponent source, Layout layout)
    {
        var comp = ent.Comp;
        StopVents(ent, source);

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
        comp.Requester = null;
        comp.SettleUntil = null;
        comp.Problem = layout.Vents.Count == 0 ? AirlockCyclerProblem.NoVent : AirlockCyclerProblem.None;
        SetState(ent, AirlockCyclerState.Idle);
    }

    private void TurnOff(Entity<AirlockCyclerComponent> ent, DeviceLinkSourceComponent source)
    {
        // Without power the chamber is just a pair of doors: let people through by hand.
        for (var i = 0; i < ent.Comp.Sides.Count; i++)
            foreach (var linked in _deviceLink.GetLinkedSinks((ent, source), ent.Comp.Sides[i]))
                Bolt(linked, false);

        StopVents(ent, source);
        ent.Comp.Force = false;
        ent.Comp.Problem = AirlockCyclerProblem.None;
        SetState(ent, AirlockCyclerState.Off);
    }

    private void LockIdleDoors(Entity<AirlockCyclerComponent> ent, DeviceLinkSourceComponent source)
    {
        for (var i = 0; i < ent.Comp.Sides.Count; i++)
        {
            if (i == ent.Comp.TargetSide)
                continue;

            foreach (var linked in _deviceLink.GetLinkedSinks((ent, source), ent.Comp.Sides[i]))
            {
                if (_doorQuery.TryComp(linked, out var door) && door.State is DoorState.Closed or DoorState.Welded)
                    Bolt(linked, true);
            }
        }
    }

    private void StopVents(Entity<AirlockCyclerComponent> ent, DeviceLinkSourceComponent source)
    {
        foreach (var linked in _deviceLink.GetLinkedSinks((ent, source), ent.Comp.VentPort))
        {
            if (_ventQuery.TryComp(linked, out var vent))
                _vents.SetAirlockCyclerMode((linked, vent), false, vent.PumpDirection, vent.ExternalPressureBound);
        }
    }

    private void Bolt(EntityUid door, bool bolted)
    {
        if (_boltQuery.TryComp(door, out var bolt) && bolt.BoltsDown != bolted)
            _doors.SetBoltsDown((door, bolt), bolted);
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

            foreach (var linked in _deviceLink.GetLinkedSinks((ent, source), ent.Comp.Sides[i]))
            {
                if (_buttonQuery.HasComp(linked))
                    _appearance.SetData(linked, AirlockCyclerButtonVisuals.State, state);
            }
        }
    }

    private bool HasSideAccess(Entity<AirlockCyclerComponent> ent, int side, EntityUid? user)
    {
        if (user is not { } actor || !TryComp<DeviceLinkSourceComponent>(ent, out var source))
            return true;

        var anyDoor = false;
        foreach (var linked in _deviceLink.GetLinkedSinks((ent, source), ent.Comp.Sides[side]))
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
                || !TryComp<DeviceLinkSourceComponent>(sourceUid, out var source))
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
