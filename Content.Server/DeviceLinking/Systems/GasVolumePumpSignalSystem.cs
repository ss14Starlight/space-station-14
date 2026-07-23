using Content.Server.Atmos.Piping.Binary.EntitySystems;
using Content.Server.DeviceLinking.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Binary.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using JetBrains.Annotations;

namespace Content.Server.DeviceLinking.Systems
{
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
            _signalSystem.EnsureSinkPorts(uid, component.OpenPort, component.ClosePort, component.TogglePort);
        }

        private void OnSignalReceived(EntityUid uid, GasVolumePumpSignalComponent component, ref SignalReceivedEvent args)
        {
            if(!TryComp(uid, out GasVolumePumpComponent? volumePump))
                return;

            var state = SignalState.Momentary;
            args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);

            if (args.Port == component.OpenPort)
            {
                if (state == SignalState.High || state == SignalState.Momentary)
                {
                    if (volumePump.Enabled == false)
                        _volumePumpSystem.SetEnable(uid, volumePump);
                }
            }
            else if (args.Port == component.ClosePort)
            {
                if (state == SignalState.High || state == SignalState.Momentary)
                    _volumePumpSystem.SetDisable(uid, volumePump);
            }
            else if (args.Port == component.TogglePort)
            {
                if (state == SignalState.High || state == SignalState.Momentary)
                    _volumePumpSystem.SetToggle(uid, volumePump);
            }

        }
    }
}
