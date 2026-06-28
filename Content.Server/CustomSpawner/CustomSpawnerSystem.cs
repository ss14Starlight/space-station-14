using Content.Server.DeviceLinking.Systems;
using Content.Shared._Starlight.CustomSpawner;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;

namespace Content.Server.CustomSpawner;

/// Majority of code is in <see cref="SharedCustomSpawnerSystem"/>.
public sealed partial class CustomSpawnerSystem : SharedCustomSpawnerSystem
{
    [Dependency] private DeviceLinkSystem _link = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CustomSpawnerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CustomSpawnerComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    protected override void DoSpawn(EntityUid uid, CustomSpawnerComponent comp)
    {
        base.DoSpawn(uid, comp);
        _link.SendSignal(uid, comp.SpawnTriggeredPort, true);
    }

    private void OnInit(Entity<CustomSpawnerComponent> ent, ref ComponentInit args)
    {
        _link.EnsureSinkPorts(ent, ent.Comp.OnPort, ent.Comp.OffPort, ent.Comp.TogglePort, ent.Comp.TriggerSpawnPort);
        _link.EnsureSourcePorts(ent, ent.Comp.EnabledPort, ent.Comp.DisabledPort, ent.Comp.SpawnTriggeredPort);
    }

    private void OnSignalReceived(Entity<CustomSpawnerComponent> ent, ref SignalReceivedEvent args)
    {
        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);

        if (args.Port == ent.Comp.OnPort)
            if (state is SignalState.High or SignalState.Momentary)
            {
                if (!ent.Comp.Enabled) _link.SendSignal(ent, ent.Comp.EnabledPort, true);
                ent.Comp.Enabled = true;
            }
        if (args.Port == ent.Comp.OffPort)
            if (state is SignalState.High or SignalState.Momentary)
            {
                if (ent.Comp.Enabled) _link.SendSignal(ent, ent.Comp.DisabledPort, true);
                ent.Comp.Enabled = false;
            }
        if (args.Port == ent.Comp.TogglePort)
            if (state is SignalState.High or SignalState.Momentary)
            {
                _link.SendSignal(ent, !ent.Comp.Enabled ? ent.Comp.EnabledPort : ent.Comp.DisabledPort, true);
                ent.Comp.Enabled = !ent.Comp.Enabled;
            }
        if (args.Port == ent.Comp.TriggerSpawnPort)
            if (state is SignalState.High or SignalState.Momentary)
                if(ent.Comp.Enabled)
                    DoSpawn(ent, ent.Comp);
    }
}
