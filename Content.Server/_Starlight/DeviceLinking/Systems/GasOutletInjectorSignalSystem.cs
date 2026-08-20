using Content.Server._Starlight.DeviceLinking.Components;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Atmos.Piping.Unary.EntitySystems;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using JetBrains.Annotations;

namespace Content.Server._Starlight.DeviceLinking.Systems;

[UsedImplicitly]
public sealed partial class GasOutletInjectorSignalSystem : EntitySystem
{
    [Dependency] private GasOutletInjectorSystem _outletInjectorSystem = default!;
    [Dependency] private DeviceLinkSystem _signalSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GasOutletInjectorSignalComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<GasOutletInjectorSignalComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnInit(EntityUid uid, GasOutletInjectorSignalComponent component, ComponentInit args)
    {
        _signalSystem.EnsureSinkPorts(uid, component.OnPort, component.OffPort, component.TogglePort);
    }

    private void OnSignalReceived(EntityUid uid, GasOutletInjectorSignalComponent component, ref SignalReceivedEvent args)
    {
        if (!TryComp(uid, out GasOutletInjectorComponent? outletInjector))
            return;

        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);

        if (state is not (SignalState.High or SignalState.Momentary)) return;
        if (args.Port == component.OnPort)
            _outletInjectorSystem.Set(uid, outletInjector, true);
        else if (args.Port == component.OffPort)
            _outletInjectorSystem.Set(uid, outletInjector, false);
        else if (args.Port == component.TogglePort)
            _outletInjectorSystem.Set(uid, outletInjector, !outletInjector.Enabled);

    }
}

