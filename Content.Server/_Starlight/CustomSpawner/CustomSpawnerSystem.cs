using Content.Server.DeviceLinking.Systems;
using Content.Shared._Starlight.CustomSpawner;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;

namespace Content.Server._Starlight.CustomSpawner;

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
        var comp = ent.Comp;
        _link.EnsureSinkPorts(ent, comp.OnPort, comp.OffPort, comp.TogglePort, comp.TriggerSpawnPort);
        _link.EnsureSourcePorts(ent, comp.EnabledPort, comp.DisabledPort, comp.SpawnTriggeredPort);
    }

    private void OnSignalReceived(Entity<CustomSpawnerComponent> ent, ref SignalReceivedEvent args)
    {
        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);
        if (state is not (SignalState.High or SignalState.Momentary)) return;

        if (args.Port == ent.Comp.OnPort && !ent.Comp.Enabled)
        {
            _link.SendSignal(ent, ent.Comp.EnabledPort, true);
            _link.SendSignal(ent, ent.Comp.DisabledPort, false);
            ent.Comp.Enabled = true;
            Dirty(ent, ent.Comp);
        }
        if (args.Port == ent.Comp.OffPort && ent.Comp.Enabled)
        {
            _link.SendSignal(ent, ent.Comp.DisabledPort, true);
            _link.SendSignal(ent, ent.Comp.EnabledPort, false);
            ent.Comp.Enabled = false;
            Dirty(ent, ent.Comp);
        }
        if (args.Port == ent.Comp.TogglePort)
        {
            _link.SendSignal(ent, !ent.Comp.Enabled ? ent.Comp.EnabledPort : ent.Comp.DisabledPort, true);
            _link.SendSignal(ent, !ent.Comp.Enabled ? ent.Comp.DisabledPort : ent.Comp.EnabledPort, false);
            ent.Comp.Enabled = !ent.Comp.Enabled;
            Dirty(ent, ent.Comp);
        }
        if (args.Port == ent.Comp.TriggerSpawnPort)
            if(ent.Comp.Enabled)
                DoSpawn(ent, ent.Comp);
    }
}
