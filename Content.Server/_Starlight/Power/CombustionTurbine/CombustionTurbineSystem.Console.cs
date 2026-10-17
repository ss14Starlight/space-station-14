using Content.Server.Power.Components;
using Content.Shared._Starlight.Power.CombustionTurbine;
using Content.Shared.Database;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Robust.Shared.Audio;

namespace Content.Server._Starlight.Power.CombustionTurbine;

public sealed partial class CombustionTurbineSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CombustionTurbineConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (now < console.NextUpdate)
                continue;

            console.NextUpdate = now + console.UpdateInterval;

            if (_ui.IsUiOpen(uid, CombustionTurbineConsoleUiKey.Key))
                UpdateConsoleUi((uid, console));
        }
    }

    [SubscribeLocalEvent]
    private void OnConsoleNewLink(Entity<CombustionTurbineConsoleComponent> ent, ref NewLinkEvent args)
    {
        if (args.SourcePort != ent.Comp.LinkingPort)
            return;

        UpdateConsoleUi(ent);
    }

    [SubscribeLocalEvent]
    private void OnConsoleOpened(Entity<CombustionTurbineConsoleComponent> ent, ref BoundUIOpenedEvent _)
        => UpdateConsoleUi(ent);

    [SubscribeLocalEvent]
    private void OnConsoleToggle(Entity<CombustionTurbineConsoleComponent> ent, ref CombustionTurbineToggleMessage args)
    {
        if (GetLinkedCompressor(ent) is not { } compressor || compressor.Comp.Enabled == args.Enabled)
            return;

        compressor.Comp.Enabled = args.Enabled;
        _audio.PlayPvs(new SoundPathSpecifier(args.Enabled
                ? "/Audio/Machines/button.ogg"
                : "/Audio/Machines/machine_switch.ogg"),
            ent);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} turned combustion turbine {ToPrettyString(compressor):entity} {(args.Enabled ? "on" : "off")}");

        UpdateConsoleUi(ent);
    }

    [SubscribeLocalEvent]
    private void OnConsoleIntake(Entity<CombustionTurbineConsoleComponent> ent, ref CombustionTurbineIntakeMessage args)
    {
        if (GetLinkedCompressor(ent) is not { } compressor || !float.IsFinite(args.Ratio))
            return;

        compressor.Comp.IntakeRatio = Math.Clamp(args.Ratio, compressor.Comp.MinIntakeRatio, 1f);

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} set combustion turbine {ToPrettyString(compressor):entity} intake to {compressor.Comp.IntakeRatio:P0}");

        UpdateConsoleUi(ent);
    }

    public Entity<CombustionTurbineCompressorComponent>? GetLinkedCompressor(Entity<CombustionTurbineConsoleComponent> ent)
    {
        if (!TryComp<DeviceLinkSourceComponent>(ent, out var source))
            return null;

        foreach (var (sink, ports) in source.LinkedPorts)
        {
            var linked = false;
            foreach (var (sourcePort, _) in ports)
            {
                if (sourcePort == ent.Comp.LinkingPort)
                {
                    linked = true;
                    break;
                }
            }

            if (!linked || TerminatingOrDeleted(sink))
                continue;

            if (TryComp<CombustionTurbineCompressorComponent>(sink, out var compressor))
                return (sink, compressor);

            if (TryComp<CombustionTurbineGeneratorComponent>(sink, out var generator)
                && generator.Compressor is { } compressorUid
                && TryComp(compressorUid, out compressor))
            {
                return (compressorUid, compressor);
            }
        }

        return null;
    }

    private void UpdateConsoleUi(Entity<CombustionTurbineConsoleComponent> ent)
    {
        var state = new CombustionTurbineConsoleState();

        if (GetLinkedCompressor(ent) is { } compressor)
        {
            var comp = compressor.Comp;
            var generator = comp.Generator;

            state.Connected = generator != null;
            state.GeneratorMissing = generator == null;
            state.Enabled = comp.Enabled;
            state.StarterRunning = comp.StarterRunning;
            state.Governor = comp.GovernorFactor;
            state.Overheating = comp.Overheating;
            state.Rpm = comp.Rpm;
            state.MaxRpm = comp.MaxRpm;
            state.Integrity = comp.Integrity;
            state.InletTemperature = comp.InletTemperature;
            state.InletPressure = comp.InletPressure;
            state.ChamberPressure = comp.Air.Pressure;
            state.OutletPressure = comp.OutletPressure;
            state.MaxTemperature = comp.MaxTemperature;
            state.MolesPerSecond = comp.MolesPerSecond;
            state.IntakeRatio = comp.IntakeRatio;
            state.IntakeVolume = comp.IntakeVolume;

            if (generator is { } gen && TryComp<PowerSupplierComponent>(gen, out var supplier))
                state.Power = supplier.CurrentSupply;
        }

        _ui.SetUiState(ent.Owner, CombustionTurbineConsoleUiKey.Key, state);
    }
}
