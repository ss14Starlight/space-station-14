using Content.Server._Starlight.DeviceLinking.Components;
using Content.Server.Atmos.Piping.Trinary.Components;
using Content.Server.Atmos.Piping.Trinary.EntitySystems;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using JetBrains.Annotations;

namespace Content.Server._Starlight.DeviceLinking.Systems;

[UsedImplicitly]
public sealed partial class GasFilterSignalSystem : EntitySystem
{
    [Dependency] private GasFilterSystem _filterSystem = default!;
    [Dependency] private DeviceLinkSystem _signalSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GasFilterSignalComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<GasFilterSignalComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnInit(EntityUid uid, GasFilterSignalComponent component, ComponentInit args)
    {
        _signalSystem.EnsureSinkPorts(uid, component.OnPort, component.OffPort, component.TogglePort);
    }

    private void OnSignalReceived(EntityUid uid, GasFilterSignalComponent component, ref SignalReceivedEvent args)
    {
        if (!TryComp(uid, out GasFilterComponent? Filter))
            return;

        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);

        if (state is not (SignalState.High or SignalState.Momentary)) return;
        if (args.Port == component.OnPort)
            _filterSystem.Set(uid, Filter, true);
        else if (args.Port == component.OffPort)
            _filterSystem.Set(uid, Filter, false);
        else if (args.Port == component.TogglePort)
            _filterSystem.Set(uid, Filter, !Filter.Enabled);

    }
}

