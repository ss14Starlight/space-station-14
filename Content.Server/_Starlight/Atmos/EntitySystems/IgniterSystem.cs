using Content.Server.Administration.Logs;
using Content.Shared._Starlight.Atmos.Components;
using Content.Shared.Database;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using Content.Shared.IgnitionSource;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Starlight.Atmos.EntitySystems;

public sealed partial class IgniterSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedIgnitionSourceSystem _ignition = default!;
    [Dependency] private SharedPowerReceiverSystem _receiver = default!;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<IgniterComponent> ent, ref MapInitEvent _)
        => UpdateState(ent.AsNullable());

    [SubscribeLocalEvent]
    private void OnSignalReceived(Entity<IgniterComponent> ent, ref SignalReceivedEvent args)
    {
        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);
        if (state is not (SignalState.High or SignalState.Momentary))
            return;

        if (args.Port == ent.Comp.TogglePort)
            SetEnabled(ent.AsNullable(), !ent.Comp.Enabled, args.Trigger);
        else if (args.Port == ent.Comp.OnPort)
            SetEnabled(ent.AsNullable(), true, args.Trigger);
        else if (args.Port == ent.Comp.OffPort)
            SetEnabled(ent.AsNullable(), false, args.Trigger);
    }

    [SubscribeLocalEvent]
    private void OnPowerChanged(Entity<IgniterComponent> ent, ref PowerChangedEvent _)
        => UpdateState(ent.AsNullable());

    [SubscribeLocalEvent]
    private void OnActivate(Entity<IgniterComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        SetEnabled(ent.AsNullable(), !ent.Comp.Enabled, args.User);
        args.Handled = true;
    }

    public void SetEnabled(Entity<IgniterComponent?> ent, bool enabled, EntityUid? user = null)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        _audio.PlayPvs(ent.Comp.ToggleSound, ent);

        if (user != null)
        {
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(user.Value):entity} turned {(enabled ? "on" : "off")} igniter {ToPrettyString(ent):target}");
        }

        UpdateState(ent);
    }

    private void UpdateState(Entity<IgniterComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var active = ent.Comp.Enabled && _receiver.IsPowered(ent.Owner);
        _ignition.SetIgnited(ent.Owner, active);
        _appearance.SetData(ent, IgniterVisuals.Active, active);
    }
}
