using Content.Server._Starlight.DeviceLinking.Components;
using Content.Server.Atmos.Piping.Binary.EntitySystems;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.Atmos.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using JetBrains.Annotations;

namespace Content.Server._Starlight.DeviceLinking.Systems;

[UsedImplicitly]
public sealed partial class GasPressurePumpSignalSystem : EntitySystem
{
    [Dependency] private GasPressurePumpSystem _pressurePumpSystem = default!;
    [Dependency] private DeviceLinkSystem _signalSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GasPressurePumpSignalComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<GasPressurePumpSignalComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnInit(EntityUid uid, GasPressurePumpSignalComponent component, ComponentInit args)
    {
        _signalSystem.EnsureSinkPorts(uid, component.OnPort, component.OffPort, component.TogglePort);
    }

    private void OnSignalReceived(EntityUid uid, GasPressurePumpSignalComponent component, ref SignalReceivedEvent args)
    {
        if (!TryComp(uid, out GasPressurePumpComponent? pressurePump))
            return;

        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);

        if (state is not (SignalState.High or SignalState.Momentary)) return;
        if (args.Port == component.OnPort)
            _pressurePumpSystem.Set(uid, pressurePump, true);
        else if (args.Port == component.OffPort)
            _pressurePumpSystem.Set(uid, pressurePump, false);
        else if (args.Port == component.TogglePort)
            _pressurePumpSystem.Set(uid, pressurePump, !pressurePump.Enabled);

    }
}

