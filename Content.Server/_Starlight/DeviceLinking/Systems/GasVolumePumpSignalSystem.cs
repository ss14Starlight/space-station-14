using Content.Server._Starlight.DeviceLinking.Components;
using Content.Server.Atmos.Piping.Binary.EntitySystems;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.Atmos.Piping.Binary.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using JetBrains.Annotations;

namespace Content.Server._Starlight.DeviceLinking.Systems;

[UsedImplicitly]
public sealed partial class GasVolumePumpSignalSystem : EntitySystem
{
    [Dependency] private GasVolumePumpSystem _volumePumpSystem = default!;
    [Dependency] private DeviceLinkSystem _signalSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GasVolumePumpSignalComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<GasVolumePumpSignalComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnInit(EntityUid uid, GasVolumePumpSignalComponent component, ComponentInit args)
    {
        _signalSystem.EnsureSinkPorts(uid, component.OnPort, component.OffPort, component.TogglePort);
    }

    private void OnSignalReceived(EntityUid uid, GasVolumePumpSignalComponent component, ref SignalReceivedEvent args)
    {
        if (!TryComp(uid, out GasVolumePumpComponent? volumePump))
            return;

        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);

        if (state is not (SignalState.High or SignalState.Momentary)) return;
        if (args.Port == component.OnPort)
            _volumePumpSystem.Set(uid, volumePump, true);
        else if (args.Port == component.OffPort)
            _volumePumpSystem.Set(uid, volumePump, false);
        else if (args.Port == component.TogglePort)
            _volumePumpSystem.Set(uid, volumePump, !volumePump.Enabled);

    }
}

