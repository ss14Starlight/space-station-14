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
public sealed partial class GasMixerSignalSystem : EntitySystem
{
    [Dependency] private GasMixerSystem _filterSystem = default!;
    [Dependency] private DeviceLinkSystem _signalSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GasMixerSignalComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<GasMixerSignalComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnInit(EntityUid uid, GasMixerSignalComponent component, ComponentInit args)
    {
        _signalSystem.EnsureSinkPorts(uid, component.OnPort, component.OffPort, component.TogglePort);
    }

    private void OnSignalReceived(EntityUid uid, GasMixerSignalComponent component, ref SignalReceivedEvent args)
    {
        if (!TryComp(uid, out GasMixerComponent? Mixer))
            return;

        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);

        if (state is not (SignalState.High or SignalState.Momentary)) return;
        if (args.Port == component.OnPort)
            _filterSystem.Set(uid, Mixer, true);
        else if (args.Port == component.OffPort)
            _filterSystem.Set(uid, Mixer, false);
        else if (args.Port == component.TogglePort)
            _filterSystem.Set(uid, Mixer, !Mixer.Enabled);

    }
}

