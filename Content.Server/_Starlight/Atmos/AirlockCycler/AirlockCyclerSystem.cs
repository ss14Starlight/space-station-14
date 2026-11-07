using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Atmos.Piping.Unary.EntitySystems;
using Content.Server.DeviceLinking.Systems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Shared._Starlight.Atmos.AirlockCycler;
using Content.Shared.Access.Systems;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Atmos.AirlockCycler;

public sealed partial class AirlockCyclerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private DeviceLinkSystem _deviceLink = default!;
    [Dependency] private GasVentPumpSystem _vents = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private NodeContainerSystem _nodes = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoorSystem _doors = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    [Dependency] private EntityQuery<DoorComponent> _doorQuery = default!;
    [Dependency] private EntityQuery<DoorBoltComponent> _boltQuery = default!;
    [Dependency] private EntityQuery<GasVentPumpComponent> _ventQuery = default!;
    [Dependency] private EntityQuery<AirlockCyclerButtonComponent> _buttonQuery = default!;

    [SubscribeLocalEvent]
    private void OnInit(Entity<AirlockCyclerComponent> ent, ref ComponentInit args)
    {
        _deviceLink.EnsureSourcePorts(ent, [.. ent.Comp.Sides]);
        _deviceLink.EnsureSourcePorts(ent, ent.Comp.InletPort, ent.Comp.OutletPort);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<AirlockCyclerComponent> ent, ref ComponentShutdown args)
    {
        // Never leave anybody bolted in a chamber nobody controls.
        if (!TryComp<DeviceLinkSourceComponent>(ent, out var source))
            return;

        for (var i = 0; i < ent.Comp.Sides.Count; i++)
        {
            foreach (var linked in GetOwnedSinks(ent, source, ent.Comp.Sides[i]))
            {
                if (_boltQuery.HasComp(linked))
                    ReleaseBolts(linked);
                else if (_buttonQuery.HasComp(linked))
                    _appearance.SetData(linked, AirlockCyclerButtonVisuals.State, AirlockCyclerButtonState.Off);
            }
        }

        // Hand the devices back with the settings they had before the controller took them.
        foreach (var linked in GetOwnedSinks(ent, source, ent.Comp.InletPort))
            ReleaseDevice(linked);

        foreach (var linked in GetOwnedSinks(ent, source, ent.Comp.OutletPort))
            ReleaseDevice(linked);
    }

    [SubscribeLocalEvent]
    private void OnNewLink(Entity<AirlockCyclerComponent> ent, ref NewLinkEvent args)
        => OnLinksChanged(ent);

    [SubscribeLocalEvent]
    private void OnPortDisconnected(Entity<AirlockCyclerComponent> ent, ref PortDisconnectedEvent args)
    {
        // The disconnected sink is no longer in the outputs, so turn its light off here.
        if (_buttonQuery.HasComp(args.Sink))
            _appearance.SetData(args.Sink, AirlockCyclerButtonVisuals.State, AirlockCyclerButtonState.Off);

        // Only the owner may hand a device back: a second controller unlinking must not undo the owner's cycle.
        if (OwnsDevice(ent, args.Sink))
        {
            // A vent on both ports stays driven until its last device link goes.
            if (IsDevicePort(ent.Comp, args.Port) && !HasOtherDeviceLink(ent, args.Sink, args.Port))
                ReleaseDevice(args.Sink);

            if (ent.Comp.Sides.Contains(args.Port) && _doorQuery.HasComp(args.Sink))
                ReleaseBolts(args.Sink);
        }

        OnLinksChanged(ent);
    }

    private bool HasOtherDeviceLink(Entity<AirlockCyclerComponent> ent, EntityUid sink, string port)
    {
        if (!TryComp<DeviceLinkSourceComponent>(ent, out var source))
            return false;

        foreach (var (linked, _) in _deviceLink.GetLinks(ent, sink, source))
        {
            if (linked != port && IsDevicePort(ent.Comp, linked))
                return true;
        }

        return false;
    }

    private void OnLinksChanged(Entity<AirlockCyclerComponent> ent)
    {
        UpdateButtons(ent);
        ent.Comp.NextUpdate = _timing.CurTime;
    }

    [SubscribeLocalEvent]
    private void OnButtonNewLink(Entity<AirlockCyclerButtonComponent> ent, ref NewLinkEvent args)
    {
        if (TryGetController(ent, out var controller, out _))
            UpdateButtons(controller);
    }

    [SubscribeLocalEvent]
    private void OnUiOpened(Entity<AirlockCyclerComponent> ent, ref BoundUIOpenedEvent args)
        => ent.Comp.NextUpdate = _timing.CurTime;

    [SubscribeLocalEvent]
    private void OnCycleMessage(Entity<AirlockCyclerComponent> ent, ref AirlockCyclerCycleMessage args)
        => RequestCycle(ent.AsNullable(), args.Side, args.Actor);

    [SubscribeLocalEvent]
    private void OnForceMessage(Entity<AirlockCyclerComponent> ent, ref AirlockCyclerForceMessage args)
    {
        if (ent.Comp.State is AirlockCyclerState.Off or AirlockCyclerState.Idle || ent.Comp.TargetSide is not { } side)
            return;

        if (!HasSideAccess(ent, side, args.Actor))
            return;

        ent.Comp.Force = true;
        ent.Comp.NextUpdate = _timing.CurTime;
    }

    [SubscribeLocalEvent]
    private void OnButtonActivate(Entity<AirlockCyclerButtonComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;

        if (TryComp<UseDelayComponent>(ent, out var delay) && !_useDelay.TryResetDelay((ent, delay), true))
            return;

        _audio.PlayPvs(ent.Comp.ClickSound, ent, AudioParams.Default.WithVolume(-2f));

        if (!TryGetController(ent, out var controller, out var side))
        {
            _popup.PopupEntity(Loc.GetString("airlock-cycler-button-unlinked"), ent, args.User);
            return;
        }

        RequestCycle(controller.AsNullable(), side, args.User, ent);
    }

    private void UpdateUi(Entity<AirlockCyclerComponent> ent, Layout layout)
    {
        var state = new AirlockCyclerBoundUserInterfaceState
        {
            State = ent.Comp.State,
            Problem = ent.Comp.Problem,
            NoVent = false,
            TargetSide = ent.Comp.TargetSide,
            ChamberPressure = layout.ChamberPressure,
            TankPressure = layout.TankPressure,
        };

        if (layout.NoDevices && ent.Comp.State != AirlockCyclerState.Off)
            state.NoVent = true;

        if (ent.Comp.TargetSide is { } target && target < layout.Sides.Count)
            state.TargetPressure = layout.Sides[target].OutsidePressure;

        for (var i = 0; i < layout.Sides.Count; i++)
        {
            var side = layout.Sides[i];
            if (side.Doors.Count == 0 && side.Buttons == 0)
                continue;

            var open = 0;
            foreach (var door in side.Doors)
            {
                if (_doorQuery.TryComp(door.Door, out var doorComp) && doorComp.State != DoorState.Closed && doorComp.State != DoorState.Welded)
                    open++;
            }

            state.Sides.Add(new AirlockCyclerSideData
            {
                Index = i,
                Name = $"airlock-cycler-side-{(char) ('a' + i)}",
                OutsidePressure = side.OutsidePressure,
                Doors = side.Doors.Count,
                OpenDoors = open,
            });
        }

        _ui.SetUiState(ent.Owner, AirlockCyclerUiKey.Key, state);
    }
}
