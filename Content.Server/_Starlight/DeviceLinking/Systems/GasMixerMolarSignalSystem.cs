using Content.Server._Moffstation.Atmos.Piping.Trinary.Components;
using Content.Server._Moffstation.Atmos.Piping.Trinary.EntitySystems;
using Content.Server._Starlight.DeviceLinking.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using JetBrains.Annotations;

namespace Content.Server._Starlight.DeviceLinking.Systems;

[UsedImplicitly]
public sealed partial class GasMixerMolarSignalSystem : EntitySystem
{
    [Dependency] private GasMixerMolarSystem _mixerMolarSystem = default!;
    [Dependency] private DeviceLinkSystem _signalSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GasMixerMolarSignalComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<GasMixerMolarSignalComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnInit(EntityUid uid, GasMixerMolarSignalComponent component, ComponentInit args)
    {
        _signalSystem.EnsureSinkPorts(uid, component.OnPort, component.OffPort, component.TogglePort);
    }

    private void OnSignalReceived(EntityUid uid, GasMixerMolarSignalComponent component, ref SignalReceivedEvent args)
    {
        if (!TryComp(uid, out GasMixerMolarComponent? mixerMolar))
            return;

        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);

        if (state is not (SignalState.High or SignalState.Momentary)) return;
        if (args.Port == component.OnPort)
            _mixerMolarSystem.Set(uid, mixerMolar, true);
        else if (args.Port == component.OffPort)
            _mixerMolarSystem.Set(uid, mixerMolar, false);
        else if (args.Port == component.TogglePort)
            _mixerMolarSystem.Set(uid, mixerMolar, !mixerMolar.Enabled);

    }
}

