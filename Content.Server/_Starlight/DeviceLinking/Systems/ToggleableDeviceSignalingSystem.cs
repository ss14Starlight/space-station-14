using Content.Server._Starlight.Atmos;
using Content.Server._Starlight.DeviceLinking.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Shared._Starlight.DeviceLinking.Components;
using Content.Shared._Starlight.DeviceLinking.EntitySystems;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using JetBrains.Annotations;

namespace Content.Server._Starlight.DeviceLinking.Systems;

/// <summary>
/// Signalling interface for the <see cref="ToggleableDeviceSystem"/>.
/// </summary>
[UsedImplicitly]
public sealed partial class ToggleableDeviceSignalingSystem : EntitySystem
{
    [Dependency] private DeviceLinkSystem _signalSystem = null!;
    [Dependency] private ToggleableDeviceSystem _toggleableDevice = null!;

    [SubscribeLocalEvent]
    private void OnInit(Entity<ToggleableDeviceSignalingComponent> entity, ref ComponentInit args)
    {
        _signalSystem.EnsureSinkPorts(entity, entity.Comp.OnPort, entity.Comp.OffPort,
            entity.Comp.TogglePort, entity.Comp.StatePort);
        _signalSystem.EnsureSourcePorts(entity, entity.Comp.StatusPort);
    }

    /// <summary>
    /// When the state changes, update the outputted State signal.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnStateChanged(Entity<ToggleableDeviceSignalingComponent> entity, ref ToggleableStateEvent ev)
    {
        _signalSystem.SendSignal(entity, entity.Comp.StatusPort, ev.State);
    }

    /// <summary>
    /// On signal receive, update the toggleable device.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnSignalReceived(Entity<ToggleableDeviceSignalingComponent> entity, ref SignalReceivedEvent args)
    {
        if (!TryComp<ToggleableDeviceComponent>(entity, out var device))
            return;

        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);
        var high = state is SignalState.High or SignalState.Momentary;

        if (args.Port == entity.Comp.OnPort && high)
            _toggleableDevice.TurnOn((entity, device));
        else if (args.Port == entity.Comp.OffPort && high)
            _toggleableDevice.TurnOff((entity, device));
        else if (args.Port == entity.Comp.TogglePort && high)
            _toggleableDevice.Toggle((entity, device));
        else if (args.Port == entity.Comp.StatePort)
            _toggleableDevice.SetState((entity, device), high);
    }

}
