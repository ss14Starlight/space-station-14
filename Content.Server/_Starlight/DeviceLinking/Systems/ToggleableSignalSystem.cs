using Content.Server._Starlight.Atmos;
using Content.Server._Starlight.DeviceLinking.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using JetBrains.Annotations;

namespace Content.Server._Starlight.DeviceLinking.Systems;

[UsedImplicitly]
public sealed partial class ToggleableSignalSystem : EntitySystem
{
    [Dependency] private DeviceLinkSystem _signalSystem = default!;
    [Dependency] private ToggleableAtmosDeviceSystem _toggleableAtmosDeviceSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ToggleableSignalComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ToggleableSignalComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnInit(EntityUid uid, ToggleableSignalComponent component, ComponentInit args) =>
        _signalSystem.EnsureSinkPorts(uid, component.OnPort, component.OffPort, component.TogglePort);

    private void OnSignalReceived(EntityUid uid, ToggleableSignalComponent component, ref SignalReceivedEvent args)
    {
        if (!TryComp<ToggleableAtmosDeviceComponent>(uid, out _))
            return;

        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);

        if (state is not (SignalState.High or SignalState.Momentary)) return;
        if (args.Port == component.OnPort)
            _toggleableAtmosDeviceSystem.Set(uid, true);
        else if (args.Port == component.OffPort)
            _toggleableAtmosDeviceSystem.Set(uid, false);
        else if (args.Port == component.TogglePort)
            _toggleableAtmosDeviceSystem.Toggle(uid);
    }
}
