using System.Runtime.CompilerServices;
using Dependency = Robust.Shared.IoC.DependencyAttribute;
using Content.Server.Atmos.Piping.Binary.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.Atmos.Piping.Binary.Components;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Atmos.Components;
using Content.Shared.EntityEffects.Effects.Body;

namespace Content.Server.Atmos.Piping.Binary.EntitySystems;

public sealed partial class SignalControlledValveSystem : EntitySystem
{
    [Dependency] private DeviceLinkSystem _signal = default!;
    [Dependency] private GasValveSystem _valve = default!;
    [Dependency] private GasVolumePumpSystem _volume = default!;
    [Dependency] private GasPressurePumpSystem _pressure = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SignalControlledValveComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SignalControlledValveComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnInit(EntityUid uid, SignalControlledValveComponent comp, ComponentInit args)
    {
        _signal.EnsureSinkPorts(uid, comp.OpenPort, comp.ClosePort, comp.TogglePort);
    }

    private void OnSignalReceived(EntityUid uid, SignalControlledValveComponent comp, ref SignalReceivedEvent args)
    {
        if (TryComp<GasValveComponent>(uid, out var valve))
        {
            if (args.Port == comp.OpenPort)
            {
                _valve.Set(uid, valve, true);
            }
            else if (args.Port == comp.ClosePort)
            {
                _valve.Set(uid, valve, false);
            }
            else if (args.Port == comp.TogglePort)
            {
                _valve.Toggle(uid, valve);
            }
        }

        if (TryComp<GasPressurePumpComponent>(uid, out var pressurePump))
        {
            if (args.Port == comp.OpenPort)
            {
                _pressure.Set(uid, pressurePump, true);
            }
            else if (args.Port == comp.ClosePort)
            {
                _pressure.Set(uid, pressurePump, false);
            }
            else if (args.Port == comp.TogglePort)
            {
                _pressure.Toggle(uid, pressurePump);
            }
        }

        if (TryComp<GasVolumePumpComponent>(uid, out var volumePump))
        {
            if (args.Port == comp.OpenPort)
            {
                _volume.Set(uid, volumePump, true);
            }
            else if (args.Port == comp.ClosePort)
            {
                _volume.Set(uid, volumePump, false);
            }
            else if (args.Port == comp.TogglePort)
            {
                _volume.Toggle(uid, volumePump);
            }
        }

        return;
    }
}
